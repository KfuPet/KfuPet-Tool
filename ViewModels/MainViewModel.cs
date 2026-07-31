using System.Collections.ObjectModel;
using System.Text.Json;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KfuPet.Ipc.Client;
using KfuPet_Tool.Helpers;
using KfuPet_Tool.Models;

namespace KfuPet_Tool.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private SkeletonPipeClient _pipeClient;
        private System.Timers.Timer? _heartbeatTimer;

        [ObservableProperty]
        private ObservableCollection<BoneInfo> _rootBones = new();

        [ObservableProperty]
        private BoneInfo? _selectedBone;

        [ObservableProperty]
        private AttachmentInfo? _selectedAttachment;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isDebugSkeleton;

        [ObservableProperty]
        private string _statusMessage = "未连接";

        [ObservableProperty]
        private ObservableCollection<string> _availablePipes = new();

        [ObservableProperty]
        private string? _selectedPipe;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _canvasWidth = 400;

        [ObservableProperty]
        private double _canvasHeight = 400;

        /// <summary>
        /// 日志输出集合，绑定到 UI 下方的日志面板。
        /// </summary>
        public ObservableCollection<string> LogMessages { get; } = new();

        public event EventHandler? PreviewUpdated;

        public MainViewModel()
        {
            _pipeClient = new SkeletonPipeClient();
            ScanPipes();
        }

        partial void OnIsConnectedChanged(bool value)
        {
            PreviewUpdated?.Invoke(this, EventArgs.Empty);
        }

        partial void OnRootBonesChanged(ObservableCollection<BoneInfo> value)
        {
            PreviewUpdated?.Invoke(this, EventArgs.Empty);
        }

        partial void OnSelectedPipeChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _pipeClient = new SkeletonPipeClient(value);
                StatusMessage = $"已选择管道：{value}";
            }
        }

        partial void OnSelectedBoneChanged(BoneInfo? value)
        {
            if (value != null && IsConnected)
            {
                _ = LoadAttachmentsForBoneAsync(value);
            }
        }

        private void RaisePreviewUpdated()
        {
            PreviewUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 写入日志输出（带时间戳），同时保留到 Fire and forget 的最大 500 条。
        /// </summary>
        private void Log(string message)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogMessages.Insert(0, entry);
                while (LogMessages.Count > 500)
                    LogMessages.RemoveAt(LogMessages.Count - 1);
            });
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogMessages.Clear();
            Log("日志已清除");
        }

        [RelayCommand]
        private void SaveLog()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"KfuPet_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllLines(dialog.FileName, LogMessages);
                    Log("日志已保存到: " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    Log("日志保存失败: " + ex.Message);
                }
            }
        }

        private void DisconnectInternal(string statusMessage)
        {
            StopHeartbeat();
            IsConnected = false;
            StatusMessage = statusMessage;
            RootBones.Clear();
            SelectedBone = null;
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatTimer = new System.Timers.Timer(500);
            _heartbeatTimer.Elapsed += OnHeartbeatElapsed;
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        private void StopHeartbeat()
        {
            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Stop();
                _heartbeatTimer.Elapsed -= OnHeartbeatElapsed;
                _heartbeatTimer.Dispose();
                _heartbeatTimer = null;
            }
        }

        private async void OnHeartbeatElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                using var cts = new CancellationTokenSource(300);
                var alive = await _pipeClient.PingAsync(cts.Token);
                if (!alive)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (IsConnected)
                        {
                            DisconnectInternal("连接已断开（连接超时）");
                            Log("心跳检测失败，已自动断开");
                        }
                    });
                }
            }
            catch
            {
                // PingAsync 已内部 catch，此处的 catch 以防万一
            }
        }

        /// <summary>
        /// 校验数值是否为有限值（非 Infinity / NaN），避免超大输入导致 JSON 序列化失败。
        /// </summary>
        private bool IsFinite(double value, string fieldName)
        {
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                Log($"{fieldName} 值无效：{value}，请输入合理范围的数字");
                MessageBox.Show($"{fieldName} 值无效（{value}），请输入合理范围的数字。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        [RelayCommand]
        private void ScanPipes()
        {
            var pipes = PipeDiscoveryService.DiscoverKfuPetPipes();
            int processCount = PipeDiscoveryService.CountKfuPetProcesses();

            AvailablePipes.Clear();
            foreach (var pipe in pipes)
            {
                AvailablePipes.Add(pipe);
            }

            if (pipes.Count == 0)
            {
                SelectedPipe = null;
                StatusMessage = "未发现 KfuPet 管道，请确认 KfuPet 已运行";
                Log("扫描管道：未发现任何 KfuPet 管道");
            }
            else if (pipes.Count == 1)
            {
                SelectedPipe = pipes[0];
                if (processCount > 1)
                {
                    StatusMessage = $"已识别管道：{pipes[0]}（警告：检测到 {processCount} 个 KfuPet 进程，命令可能随机分配到不同实例，建议只保留一个）";
                    Log($"扫描管道：发现 {pipes[0]}，但检测到 {processCount} 个 KfuPet 进程（可能串扰）");
                }
                else
                {
                    StatusMessage = $"已自动识别管道：{pipes[0]}";
                    Log($"扫描管道：自动选择 {pipes[0]}");
                }
            }
            else
            {
                SelectedPipe = null;
                StatusMessage = $"发现 {pipes.Count} 个管道，请手动选择";
                Log($"扫描管道：发现 {pipes.Count} 个管道（{string.Join(", ", pipes)}），请手动选择");
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (IsLoading) return;

            if (string.IsNullOrEmpty(SelectedPipe))
            {
                StatusMessage = "请先选择一个管道";
                return;
            }

            IsLoading = true;
            try
            {
                var boneIds = await Task.Run(() => _pipeClient.GetBoneIds());
                if (boneIds.Count > 0)
                {
                    IsConnected = true;
                    StatusMessage = $"已连接：{SelectedPipe}";
                    Log($"已连接：{SelectedPipe}，获取到 {boneIds.Count} 个骨骼");
                    StartHeartbeat();
                    await LoadBoneTreeAsync();
                    IsDebugSkeleton = await Task.Run(() => _pipeClient.GetDebugSkeleton());
                }
                else
                {
                    StatusMessage = "连接失败：未获取到骨骼数据";
                    Log("连接失败：服务端返回 0 个骨骼");
                }
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusMessage = $"连接失败：{ex.Message}";
                Log($"连接失败：{ex.Message}");
                MessageBox.Show($"连接失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            StopHeartbeat();
            IsConnected = false;
            StatusMessage = "已断开";
            Log("已断开连接");
            RootBones.Clear();
            SelectedBone = null;
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!IsConnected) return;
            Log("刷新骨骼树...");
            await LoadBoneTreeAsync();
        }

        private async Task LoadBoneTreeAsync()
        {
            try
            {
                var boneIds = await Task.Run(() => _pipeClient.GetBoneIds());
                var boneDict = new Dictionary<string, BoneInfo>();

                foreach (var id in boneIds)
                {
                    var bone = new BoneInfo { BoneId = id };
                    boneDict[id] = bone;
                }

                foreach (var bone in boneDict.Values)
                {
                    bone.BoneName = await Task.Run(() => _pipeClient.GetBoneName(bone.BoneId) ?? bone.BoneId);
                    bone.ParentBoneId = await Task.Run(() => _pipeClient.GetParentBoneId(bone.BoneId));

                    var pos = await Task.Run(() => _pipeClient.GetPosition(bone.BoneId));
                    if (pos.HasValue)
                    {
                        bone.PositionX = pos.Value.X;
                        bone.PositionY = pos.Value.Y;
                    }

                    bone.Rotation = await Task.Run(() => _pipeClient.GetRotation(bone.BoneId)) ?? 0;

                    var scale = await Task.Run(() => _pipeClient.GetScale(bone.BoneId));
                    if (scale.HasValue)
                    {
                        bone.ScaleX = scale.Value.X;
                        bone.ScaleY = scale.Value.Y;
                    }

                    bone.IsActive = await Task.Run(() => _pipeClient.IsActive(bone.BoneId)) ?? true;

                    var worldPos = await Task.Run(() => _pipeClient.GetWorldPosition(bone.BoneId));
                    if (worldPos.HasValue)
                    {
                        bone.WorldX = worldPos.Value.X;
                        bone.WorldY = worldPos.Value.Y;
                    }
                }

                RootBones.Clear();
                foreach (var bone in boneDict.Values)
                {
                    if (string.IsNullOrEmpty(bone.ParentBoneId))
                    {
                        RootBones.Add(bone);
                    }
                    else if (boneDict.TryGetValue(bone.ParentBoneId, out var parent))
                    {
                        parent.Children.Add(bone);
                    }
                }
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载骨骼树失败：{ex.Message}";
                Log($"加载骨骼树失败：{ex.Message}");
                MessageBox.Show($"加载骨骼树失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task SetPositionAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            if (!IsFinite(bone.PositionX, "位置 X") || !IsFinite(bone.PositionY, "位置 Y")) return;

            try
            {
                var ok = await Task.Run(() => _pipeClient.SetPosition(bone.BoneId, bone.PositionX, bone.PositionY));
                if (!ok)
                {
                    Log("设置位置失败");
                    return;
                }
                await UpdateWorldPositionAsync(bone);
                Log($"已设置 {bone.BoneName} 位置为 ({bone.PositionX:F1}, {bone.PositionY:F1})");
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"设置 {bone.BoneName} 位置失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SetRotationAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            if (!IsFinite(bone.Rotation, "旋转角度")) return;

            try
            {
                var boneId = bone.BoneId;
                var targetDegrees = bone.Rotation;
                var ok = await Task.Run(() => _pipeClient.SetRotation(boneId, targetDegrees));
                if (!ok)
                {
                    Log("设置旋转失败");
                    return;
                }

                var actualDegrees = await Task.Run(() => _pipeClient.GetRotation(boneId));
                if (actualDegrees.HasValue && Math.Abs(actualDegrees.Value - targetDegrees) > 0.01)
                {
                    Log($"警告：设置 {bone.BoneName} 旋转为 {targetDegrees}°，但服务端返回 {actualDegrees.Value}°（可能是 KfuPet 服务端问题）");
                }
                else
                {
                    Log($"已设置 {bone.BoneName} 旋转为 {targetDegrees}°");
                }

                await RefreshAllWorldPositionsAsync();
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"设置 {bone.BoneName} 旋转失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SetScaleAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            if (!IsFinite(bone.ScaleX, "缩放 X") || !IsFinite(bone.ScaleY, "缩放 Y")) return;

            try
            {
                var ok = await Task.Run(() => _pipeClient.SetScale(bone.BoneId, bone.ScaleX, bone.ScaleY));
                if (!ok)
                {
                    Log("设置缩放失败");
                    return;
                }
                Log($"已设置 {bone.BoneName} 缩放为 ({bone.ScaleX:F2}, {bone.ScaleY:F2})");
                await RefreshAllWorldPositionsAsync();
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"设置 {bone.BoneName} 缩放失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SetActiveAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            try
            {
                await Task.Run(() => _pipeClient.SetActive(bone.BoneId, bone.IsActive));
                Log($"已{(bone.IsActive ? "激活" : "隐藏")}骨骼 {bone.BoneName}");
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"设置 {bone.BoneName} 激活状态失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ResetBoneAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            try
            {
                var boneId = bone.BoneId;
                await Task.Run(() => _pipeClient.ResetBone(boneId));

                await RefreshBoneAsync(bone);
                await RefreshAllWorldPositionsAsync();
                RaisePreviewUpdated();
                Log($"已恢复骨骼 {bone.BoneName} 到默认状态");
            }
            catch (Exception ex)
            {
                Log($"恢复 {bone.BoneName} 失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ResetAllAsync()
        {
            if (!IsConnected) return;

            var result = MessageBox.Show("确定要恢复所有骨骼到默认状态吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(() => _pipeClient.ResetAll());

                foreach (var bone in GetAllBones())
                {
                    await RefreshBoneAsync(bone);
                }

                await RefreshAllWorldPositionsAsync();
                RaisePreviewUpdated();
                Log("已恢复所有骨骼到默认状态");
            }
            catch (Exception ex)
            {
                Log($"恢复所有骨骼失败：{ex.Message}");
            }
        }

        private async Task RefreshBoneAsync(BoneInfo bone)
        {
            var pos = await Task.Run(() => _pipeClient.GetPosition(bone.BoneId));
            if (pos.HasValue)
            {
                bone.PositionX = pos.Value.X;
                bone.PositionY = pos.Value.Y;
            }

            bone.Rotation = await Task.Run(() => _pipeClient.GetRotation(bone.BoneId)) ?? 0;

            var scale = await Task.Run(() => _pipeClient.GetScale(bone.BoneId));
            if (scale.HasValue)
            {
                bone.ScaleX = scale.Value.X;
                bone.ScaleY = scale.Value.Y;
            }

            bone.IsActive = await Task.Run(() => _pipeClient.IsActive(bone.BoneId)) ?? true;
        }

        private async Task UpdateWorldPositionAsync(BoneInfo bone)
        {
            var worldPos = await Task.Run(() => _pipeClient.GetWorldPosition(bone.BoneId));
            if (worldPos.HasValue)
            {
                bone.WorldX = worldPos.Value.X;
                bone.WorldY = worldPos.Value.Y;
            }

            foreach (var child in bone.Children)
            {
                await UpdateWorldPositionAsync(child);
            }
        }

        private async Task RefreshAllWorldPositionsAsync()
        {
            try
            {
                foreach (var bone in RootBones)
                {
                    await UpdateWorldPositionAsync(bone);
                }
            }
            catch (Exception ex)
            {
                Log($"刷新世界坐标失败：{ex.Message}");
            }
        }

        public IEnumerable<BoneInfo> GetAllBones()
        {
            foreach (var bone in RootBones)
            {
                yield return bone;
                foreach (var child in GetAllBones(bone))
                {
                    yield return child;
                }
            }
        }

        private IEnumerable<BoneInfo> GetAllBones(BoneInfo parent)
        {
            foreach (var child in parent.Children)
            {
                yield return child;
                foreach (var grandChild in GetAllBones(child))
                {
                    yield return grandChild;
                }
            }
        }

        private async Task LoadAttachmentsForBoneAsync(BoneInfo bone)
        {
            try
            {
                var ids = await Task.Run(() => _pipeClient.GetBoneAttachments(bone.BoneId));

                System.Windows.Application.Current.Dispatcher.Invoke(() => bone.Attachments.Clear());

                foreach (var id in ids)
                {
                    var je = await Task.Run(() => _pipeClient.GetAttachment(id));
                    if (je == null) continue;

                    var att = new AttachmentInfo
                    {
                        Id = je.Value.GetProperty("id").GetString() ?? id,
                        BoneId = je.Value.GetProperty("boneId").GetString() ?? bone.BoneId,
                        Name = je.Value.GetProperty("name").GetString() ?? "",
                        ResourcePath = je.Value.GetProperty("resourcePath").GetString() ?? "",
                        OffsetX = GetJsonDouble(je.Value, "offsetX"),
                        OffsetY = GetJsonDouble(je.Value, "offsetY"),
                        PivotX = GetJsonDouble(je.Value, "pivotX", 0.5),
                        PivotY = GetJsonDouble(je.Value, "pivotY", 0.5),
                        ZOrder = je.Value.TryGetProperty("zOrder", out var zo) ? zo.GetInt32() : 0,
                        Visible = je.Value.TryGetProperty("visible", out var vis) && vis.GetBoolean(),
                        ScaleX = GetJsonDouble(je.Value, "scaleX", 1.0),
                        ScaleY = GetJsonDouble(je.Value, "scaleY", 1.0)
                    };

                    System.Windows.Application.Current.Dispatcher.Invoke(() => bone.Attachments.Add(att));
                }

                if (ids.Count > 0)
                    Log($"已加载骨骼 {bone.BoneName} 的 {ids.Count} 个图片");
            }
            catch (Exception ex)
            {
                Log($"加载附件列表失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task UploadAndAddAttachmentAsync()
        {
            var bone = SelectedBone;
            if (bone == null || !IsConnected) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|所有文件 (*.*)|*.*",
                Title = "选择要挂载的图片"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var bytes = await Task.Run(() => System.IO.File.ReadAllBytes(dialog.FileName));
                var base64 = Convert.ToBase64String(bytes);
                var ext = System.IO.Path.GetExtension(dialog.FileName).TrimStart('.').ToLower();
                var dataUri = $"data:image/{ext};base64,{base64}";

                var path = await Task.Run(() => _pipeClient.UploadResource(dataUri, bone.BoneId));
                if (path == null)
                {
                    Log("上传图片失败：服务端返回空路径");
                    MessageBox.Show("上传图片失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var attachmentId = $"{bone.BoneId}_img_{DateTime.Now:HHmmss}";
                var name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);

                var ok = await Task.Run(() => _pipeClient.AddAttachment(bone.BoneId, attachmentId, name, path));
                if (!ok)
                {
                    Log($"挂载图片失败：骨骼 {bone.BoneName} 可能不存在");
                    return;
                }

                Log($"已挂载图片 {name} 到骨骼 {bone.BoneName}");
                await LoadAttachmentsForBoneAsync(bone);
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"挂载图片失败：{ex.Message}");
                MessageBox.Show($"挂载图片失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveAttachmentAsync(AttachmentInfo? attachment)
        {
            if (attachment == null || SelectedBone == null || !IsConnected) return;

            var result = MessageBox.Show($"确定要移除图片 \"{attachment.Name}\" 吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var ok = await Task.Run(() => _pipeClient.RemoveAttachment(attachment.Id));
                if (ok)
                {
                    Log($"已移除图片 {attachment.Name}");
                    SelectedBone.Attachments.Remove(attachment);
                    RaisePreviewUpdated();

                    var resourcePath = attachment.ResourcePath;
                    if (!string.IsNullOrEmpty(resourcePath))
                    {
                        var deleteResult = MessageBox.Show(
                            $"是否同时删除缓存中的资源文件？\n{resourcePath}",
                            "清理缓存", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (deleteResult == MessageBoxResult.Yes)
                        {
                            var deleted = await Task.Run(() => _pipeClient.DeleteResource(resourcePath));
                            Log(deleted ? $"已删除资源文件：{resourcePath}" : $"删除资源文件失败（可能不在缓存目录）：{resourcePath}");
                        }
                    }
                }
                else
                {
                    Log($"移除图片 {attachment.Name} 失败");
                }
            }
            catch (Exception ex)
            {
                Log($"移除图片失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ToggleAttachmentVisibleAsync(AttachmentInfo? attachment)
        {
            if (attachment == null || !IsConnected) return;

            try
            {
                var ok = await Task.Run(() => _pipeClient.SetAttachmentVisible(attachment.Id, attachment.Visible));
                if (ok)
                {
                    Log($"已{(attachment.Visible ? "显示" : "隐藏")}图片 {attachment.Name}");
                    RaisePreviewUpdated();
                }
            }
            catch (Exception ex)
            {
                Log($"设置图片可见性失败：{ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SetDebugSkeletonAsync()
        {
            if (!IsConnected) return;

            try
            {
                await Task.Run(() => _pipeClient.SetDebugSkeleton(IsDebugSkeleton));
                Log(IsDebugSkeleton ? "已开启调试线框" : "已关闭调试线框");
                RaisePreviewUpdated();
            }
            catch (Exception ex)
            {
                Log($"设置调试线框失败：{ex.Message}");
                IsDebugSkeleton = !IsDebugSkeleton;
            }
        }

        [RelayCommand]
        private async Task SetAttachmentScaleAsync()
        {
            var att = SelectedAttachment;
            if (att == null || !IsConnected) return;

            if (!IsFinite(att.ScaleX, "图片缩放 X") || !IsFinite(att.ScaleY, "图片缩放 Y")) return;

            try
            {
                var ok = await Task.Run(() => _pipeClient.SetAttachmentScale(att.Id, att.ScaleX, att.ScaleY));
                if (ok)
                {
                    Log($"已设置图片 {att.Name} 缩放为 ({att.ScaleX:F2}, {att.ScaleY:F2})");
                    RaisePreviewUpdated();
                }
                else
                {
                    Log($"设置图片 {att.Name} 缩放失败");
                }
            }
            catch (Exception ex)
            {
                Log($"设置图片缩放失败：{ex.Message}");
            }
        }

        private static double GetJsonDouble(JsonElement je, string property, double defaultValue = 0)
        {
            if (je.TryGetProperty(property, out var prop) && prop.ValueKind != JsonValueKind.Null)
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return prop.GetDouble();
                if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var d))
                    return d;
            }
            return defaultValue;
        }

    }
}
