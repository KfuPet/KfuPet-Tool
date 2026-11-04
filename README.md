# KfuPet 官方开发者工具

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![License](https://img.shields.io/badge/License-AGPL--3.0-blue)
![Version](https://img.shields.io/badge/version-0.0.9-orange)

KfuPet Tool 是 KfuPet 的官方外部开发者工具。它通过命名管道（Named Pipe）连接正在运行的 KfuPet 主程序，实时编辑角色骨骼、挂载与调整骨骼图片，并查看服务端运行日志。

> 目前正在积极开发中🧑‍💻，功能与接口可能随版本调整。

## 功能特性

- **骨骼树浏览** — 自动扫描系统中可用的 KfuPet 管道，连接后加载完整骨骼层级结构。
- **骨骼变换编辑** — 修改骨骼的本地位置、旋转、缩放与激活状态，输入值为相对连接时初始状态的累计修改量。
- **可视化预览** — 画布实时绘制骨骼线框与关节点，支持点击节点直接选中骨骼。
- **图片挂载** — 将本地图片上传到 KfuPet 资源缓存并挂载到指定骨骼，支持显示/隐藏、移除与缩放调整。
- **调试线框** — 一键开关 KfuPet 端的骨骼调试线框（蓝色线条 + 关节圆点）。
- **双路日志** — 主界面内嵌操作日志；独立「服务端日志」窗口订阅 `KfuPet.Log` 管道，先补发历史日志再实时推送。
- **连接守护** — 后台定时心跳检测，KfuPet 退出或失去响应时自动断开并提示。
- **单实例运行** — 避免多个工具实例同时操作造成状态混乱。
- **启动选择** — 启动时可多选要打开的功能：主程序、服务端日志。

## 环境要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 / 11（WPF 仅支持 Windows） |
| 运行时 | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 构建环境 | .NET 8 SDK、Visual Studio 2022（可选） |
| 服务端 | 已安装并启动 KfuPet 主程序 |

> 骨骼编辑依赖 `KfuPet.Skeleton` 命令管道，该管道受 KfuPet 的「开发者模式」开关控制；日志管道 `KfuPet.Log` 常开。

## 快速开始

### 构建与运行

在仓库根目录执行：

```powershell
# 构建 Release 版本
dotnet build -c Release

# 直接运行
dotnet run
```

也可以使用 Visual Studio 打开 `KfuPet-Tool.slnx` 后构建运行。

构建产物位于 `bin/Release/net8.0-windows/KfuPet-Tool.exe`。

### 使用流程

1. 启动 KfuPet，并在设置中开启「开发者模式」（否则命令管道不会监听）。
2. 启动 KfuPet Tool，在启动选择窗中勾选要打开的功能（可多选）。
3. 在主窗口点击「扫描」；若只有一个管道会自动选中，然后点击「连接」。
4. 在左侧骨骼树中选择骨骼，在右侧属性面板修改位置、旋转、缩放或激活状态，回车或点击「应用」生效。
5. 中间预览区会同步绘制骨骼结构，点击关节点也可切换选中骨骼。
6. 点击「挂载图片」选择本地图片，工具会自动上传到 KfuPet 资源缓存并挂载到当前骨骼（`root` 骨骼不承载图片）。
7. 需要查看骨骼结构时，勾选工具栏中的「调试线框」。
8. 服务端日志可通过启动选择窗打开，或在主窗口菜单「工具 → 服务端日志」中打开。

## IPC 接口概览

工具的全部能力都通过命名管道调用 KfuPet，协议为逐行 JSON。

| 管道名称 | 方向 | 传输模式 | 说明 |
|----------|------|----------|------|
| `KfuPet.Skeleton` | 双向 | Message | 骨骼命令，请求/响应模型 |
| `KfuPet.Log` | 双向 | Byte | 服务端日志，单向推送 |

请求示例：

```json
{
  "service": "skeleton",
  "action": "SetRotation",
  "params": {
    "boneId": "arm_left_upper",
    "degrees": 45
  }
}
```

响应示例：

```json
{
  "success": true,
  "data": true,
  "error": null
}
```

完整 action 列表、参数说明与调用示例见 [docs/SKELETON_API.md](docs/SKELETON_API.md)。

## 默认骨骼结构

```
root
├── body
│   ├── head
│   ├── arm_left_upper
│   │   └── arm_left_lower
│   └── arm_right_upper
│       └── arm_right_lower
├── leg_left_upper
│   └── leg_left_lower
└── leg_right_upper
    └── leg_right_lower
```

- `root` 是世界锚点，不承载图片资源，无法挂载附件。
- 父骨骼的变换会传递给所有子骨骼，例如旋转上臂时小臂会跟随旋转。
- 位置单位为逻辑像素（WPF 设备无关像素），旋转单位为角度，正值顺时针。

## 项目结构

```
KfuPet-Tool/
├── App.xaml(.cs)              # 应用入口、单实例控制与启动选择
├── LauncherWindow.xaml(.cs)   # 启动选择窗（主程序 / 服务端日志）
├── MainWindow.xaml(.cs)       # 骨骼编辑主界面与预览绘制
├── LogWindow.xaml(.cs)        # 独立服务端日志窗口
├── Models/                    # BoneInfo、AttachmentInfo
├── ViewModels/                # MainViewModel、LogViewModel
├── Services/                  # SkeletonPipeClient、LogPipeClient（IPC 客户端）
├── Helpers/                   # 管道发现、日志窗口服务、值转换器
└── docs/
    └── SKELETON_API.md        # IPC API 参考文档
```

技术栈：WPF + .NET 8 + [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) 8.4.2（MVVM 源生成器）。

## 常见问题

**扫描不到管道？**
确认 KfuPet 已启动并开启了「开发者模式」，且当前用户对管道有访问权限。日志管道不受开发者模式影响，可用来验证 KfuPet 是否正常运行。

**连接超时？**
单次 IPC 调用默认 5 秒超时。若管道存在但服务端无响应，请检查 KfuPet 是否卡顿；也可确认没有多个 KfuPet 实例占用同名管道。

**检测到多个 KfuPet 进程？**
多个实例共享同一管道名时，命令可能被随机分配到不同实例，建议只保留一个 KfuPet 进程。

**服务端日志窗口连接失败？**
`KfuPet.Log` 同一时刻只服务一个客户端；若已有其他程序连接，需等待其断开后再连接，否则会在约 5 秒后超时。

**为什么 `root` 骨骼不能挂载图片？**
`root` 是世界锚点而非身体部位，挂载图片会返回失败，请选择 `body`、`head` 等骨骼。

**缩放功能可以长期使用吗？**
骨骼缩放与图片缩放目前为测试功能，后续版本可能调整或移除。

## 路线图

- 完善骨骼编辑与图片挂载的交互体验。
- 规划 Android 端远程调试：安卓端工具通过 HttpServer 远程调用 KfuPet API。
- 逐步接入更多服务（memory / emotion / vision）。

## 许可证

本项目采用 [GNU Affero General Public License v3.0](LICENSE) 许可协议。

Copyright (C) 2026 KfuPet

## 支持项目

- 爱发电：<https://www.ifdian.net/a/lrht-llw>
- 赞助页：<https://zz.lrht.cc/>
