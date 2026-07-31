using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KfuPet_Tool.Models;
using KfuPet_Tool.ViewModels;

namespace KfuPet_Tool
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            _viewModel.PreviewUpdated += ViewModel_PreviewUpdated;
        }

        private void ViewModel_PreviewUpdated(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(DrawPreview);
        }

        private void BoneTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedBone = e.NewValue as BoneInfo;
            DrawPreview();
        }

        private void IsActive_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBone != null && _viewModel.IsConnected)
            {
                _viewModel.SetActiveCommand.Execute(null);
                DrawPreview();
            }
        }

        private void IsActive_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedBone != null && _viewModel.IsConnected)
            {
                _viewModel.SetActiveCommand.Execute(null);
                DrawPreview();
            }
        }

        private void AttachmentVisible_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is AttachmentInfo att)
            {
                _viewModel.ToggleAttachmentVisibleCommand.Execute(att);
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is AttachmentInfo att)
            {
                _viewModel.RemoveAttachmentCommand.Execute(att);
            }
        }

        private void DebugSkeleton_Changed(object sender, RoutedEventArgs e)
        {
            _viewModel.SetDebugSkeletonCommand.Execute(null);
        }

        private void DrawPreview()
        {
            PreviewCanvas.Children.Clear();

            double canvasW = PreviewCanvas.Width;
            double canvasH = PreviewCanvas.Height;

            if (!_viewModel.IsConnected || _viewModel.RootBones.Count == 0)
            {
                DrawCenteredText("请先连接 KfuPet", Brushes.Gray, 14, canvasW, canvasH);
                return;
            }

            var activeBones = new List<BoneInfo>();
            CollectActiveBones(_viewModel.RootBones, activeBones);

            if (activeBones.Count == 0)
            {
                DrawCenteredText("无激活骨骼", Brushes.Gray, 14, canvasW, canvasH);
                return;
            }

            double minX = activeBones.Min(b => b.WorldX);
            double maxX = activeBones.Max(b => b.WorldX);
            double minY = activeBones.Min(b => b.WorldY);
            double maxY = activeBones.Max(b => b.WorldY);

            double padding = 50;
            double boneW = Math.Max(maxX - minX, 1);
            double boneH = Math.Max(maxY - minY, 1);

            double availW = canvasW - padding * 2;
            double availH = canvasH - padding * 2;

            double scale = Math.Min(availW / boneW, availH / boneH);
            if (scale > 3) scale = 3;
            if (scale < 0.1) scale = 0.1;

            double offsetX = (canvasW - boneW * scale) / 2 - minX * scale;
            double offsetY = (canvasH - boneH * scale) / 2 - minY * scale;

            var transformed = new Dictionary<string, (double X, double Y)>();
            foreach (var bone in activeBones)
            {
                transformed[bone.BoneId] = (bone.WorldX * scale + offsetX, bone.WorldY * scale + offsetY);
            }

            foreach (var bone in activeBones)
            {
                if (!string.IsNullOrEmpty(bone.ParentBoneId) && transformed.ContainsKey(bone.ParentBoneId))
                {
                    var (px, py) = transformed[bone.ParentBoneId];
                    var (x, y) = transformed[bone.BoneId];
                    bool isSelected = bone == _viewModel.SelectedBone;
                    var line = new Line
                    {
                        X1 = px,
                        Y1 = py,
                        X2 = x,
                        Y2 = y,
                        Stroke = isSelected ? Brushes.Yellow : Brushes.LightBlue,
                        StrokeThickness = isSelected ? 3 : 2
                    };
                    PreviewCanvas.Children.Add(line);
                }
            }

            foreach (var bone in activeBones)
            {
                var (x, y) = transformed[bone.BoneId];
                bool isSelected = bone == _viewModel.SelectedBone;

                var ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = isSelected ? Brushes.Yellow : Brushes.LightCoral
                };
                Canvas.SetLeft(ellipse, x - 5);
                Canvas.SetTop(ellipse, y - 5);
                PreviewCanvas.Children.Add(ellipse);

                var label = new TextBlock
                {
                    Text = bone.BoneName,
                    Foreground = Brushes.White,
                    FontSize = 10
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
                Canvas.SetTop(label, y + 8);
                PreviewCanvas.Children.Add(label);
            }
        }

        private void DrawCenteredText(string text, Brush foreground, double fontSize, double canvasW, double canvasH)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, (canvasW - tb.DesiredSize.Width) / 2);
            Canvas.SetTop(tb, (canvasH - tb.DesiredSize.Height) / 2);
            PreviewCanvas.Children.Add(tb);
        }

        private void CollectActiveBones(IEnumerable<BoneInfo> bones, List<BoneInfo> result)
        {
            foreach (var bone in bones)
            {
                if (!bone.IsActive) continue;
                result.Add(bone);
                CollectActiveBones(bone.Children, result);
            }
        }
    }
}
