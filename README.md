# 奶蛙桌宠（NaiwaDesktopPet）v2.5

C# WinForms 桌面宠物：透明异形窗口、无焦点不抢鼠标、多帧动画、拖文件回收、饥饿喂养、重力行走、互动表演。

> 源码由 AI 辅助生成，经人工逐项调试验证。
> 开源协议：[MIT](LICENSE)

---

## 版本

| 版本 | 说明 |
|---|---|
| **v2.5** | 当前版本。源码重构为模块化工程；新增待机 GIF 小动作（小动作 / 转手舞）、上抛后哭泣、音量滑条、托盘图标、`--self-test` 自检模式 |
| v2.0 | 单文件 `Naiwa.cs` 版本，保留在 Releases |

---

## 功能特性

- **透明异形窗口**：`UpdateLayeredWindow` 实现真正的透明异形窗口，窗口无边框、不抢焦点、不激活，按形状响应点击
- **眼睛跟随鼠标**：360° 眼珠平滑转动，永远看向鼠标方向，自然眨眼
- **待机 GIF 小动作**（v2.5 新增）：闲置时会自己做起「小动作」「转手舞」，画面取自 GIF 逐帧解包
- **互动动作**：
  - 双击肚子 → 大笑（配自定义音效）
  - 单击 / 拖动 → 举起来（惊恐脸）+ 重力下落，可上抛
  - 上抛落地 → **哭泣**（v2.5 新增，独立音效 `cry.wav`）
  - 双击头部 → 三阶段表演：起势 → 愤怒 → 打电话（炸开后变成 6 只小奶蛙独立窗口，落地跳舞配音乐）
- **拖文件回收**：把文件拖到奶蛙身上（靠近头部区域即张嘴）→ 张嘴吃下 → 移入回收站（可撤销）
- **饥饿系统**：喂 1 个文件 +1 点、每 10 分钟 -1 点；低于 3 点饿肚子乞讨；喂满 10 点 → 冒烟变成胖袋鼠
- **喂食反馈**：喂完唱歌跳舞（自定义音效，结束前 1 秒渐隐淡出）
- **睡觉**：闲置自动入睡，播放「笑倒」动画后定格躺卧，鼠标靠近唤醒
- **行走**：20fps 多帧行走动画，落地停稳即走，碰屏幕边缘转向，鼠标在身上停走
- **右贴边隐藏**：拖到屏幕右缘只露头，可拖动拖出
- **系统集成**：开机自启动、全屏自动隐藏、整点报时、休息提醒
- **托盘图标**（v2.5 新增）：右键托盘等同右键奶蛙，双击唤回
- **右键菜单**：饥饿条、使用说明书、设置（查看动作 / 大小 / 声音 / 音量滑条 / 自启 / 全屏隐藏 / 退出）

---

## 运行

### 方式一：下载即用（推荐给普通用户）

到 [Releases](https://github.com/LIN4RUN/naiwa-desktop-pet/releases) 下载 **`naiwa-pet-v2.5-portable.zip`**，解压到任意目录，双击 `奶蛙桌宠.exe` 即可（压缩包已包含全部素材与音效，免安装）。

### 方式二：源码构建

需要 **.NET SDK 6.0 及以上**（或 Visual Studio 2022，自带 SDK）：

```bat
cd 源码
build.cmd
```

`build.cmd` 会调用 `dotnet build` 编译 `奶蛙桌宠.csproj`，并把产出的 `奶蛙桌宠.exe` 复制到仓库根目录。

> 也可以手动执行：`dotnet build 奶蛙桌宠.csproj -c Release`，产物在 `源码\bin\Release\net40\奶蛙桌宠.exe`。
> 目标框架为 .NET Framework 4.0，首次构建需要联网还原 `Microsoft.NETFramework.ReferenceAssemblies` 引用程序集。

### 素材准备

以下 4 个体积较大的素材**不入库**，请从 [Releases](https://github.com/LIN4RUN/naiwa-desktop-pet/releases) 的 `naiwa-pet-v2.5-portable.zip` 中提取后放到**项目根目录**（与 exe 同级）：

| 文件 | 大小 | 用途 |
|---|---|---|
| `素材.nwp` | 71 MB | 本体形象与大笑动画 |
| `idle_gif.nwp` | 53 MB | 待机 GIF 小动作 |
| `drag.nwp` | 38 MB | 拖放回收（张嘴 / 吞咽） |
| `dance.mp3` | 18 MB | 喂食 / 舞群跳舞音效 |

仓库内已自带的素材（`walk.nwp`、`head.nwp`、`sad.nwp`、`cry.wav` 等）无需另外下载。

> 环境要求：Windows 7 SP1 / 10 / 11（64 位）；.NET Framework 4.0+（Win10/11 已内置）；音效依赖 Windows Media Player（`WMPlayer.OCX`，Win10/11 默认自带）。

---

## 技术栈

| 项目 | 说明 |
|---|---|
| 语言/框架 | C#，.NET Framework 4.0（模块化工程，除引用程序集外无第三方依赖） |
| UI | WinForms 无边框窗体 + `NotifyIcon` 托盘 |
| 透明异形窗口 | P/Invoke `UpdateLayeredWindow` + 兼容 DC 绘制，`WS_EX_LAYERED \| TOOLWINDOW \| NOACTIVATE` |
| 绘图 | GDI+（System.Drawing），自管理脏矩形与合成 |
| 动画 | 自制 `.nwp` 素材包：ZipArchive 打包 PNG 帧 + `metadata.json`，20fps 多帧动画；GIF 逐帧解包另存为 `.nwp` |
| 音效 | WAV（大笑 / 哭泣）+ MP3（喂食 / 舞群跳舞，WMP COM 播放，1 秒渐隐） |
| 物理 | 重力下落、上抛、触底回弹、贴边隐藏 |
| 自检 | `奶蛙桌宠.exe --self-test <输出目录>` 跑自动化用例，`--demo` / `--probe` / `--drop-test` 为调试入口 |

---

## 源码结构

```
源码/
├── 奶蛙桌宠.csproj        # 工程文件（net40，WinExe）
├── app.manifest           # 程序清单（高 DPI 支持）
├── app.ico                # 程序图标
├── build.cmd              # 一键编译（dotnet build → 根目录 exe）
└── NaiwaDesktop/
    ├── Program.cs         # 入口、命令行参数（--self-test / --demo / --quit / --hungry …）
    ├── PetWindow.cs       # 主窗体：交互、动画状态机、菜单、拖放、饥饿、音效
    ├── CrowdWindow.cs     # 炸开后的 6 只小奶蛙独立窗口
    ├── Settings.cs        # settings.ini 读写（大小 / 音量 / 自启 / 贴边 / 饥饿 …）
    ├── Assets.cs          # 素材加载与校验
    ├── Renderer.cs        # 分层窗口绘制与合成
    ├── LayeredSurface.cs  # UpdateLayeredWindow 封装
    ├── Motion.cs          # 重力 / 抛掷 / 行走运动
    ├── Gesture.cs         # 鼠标手势判定（单击 / 双击 / 抛掷）
    ├── Fullscreen.cs      # 全屏检测
    ├── FullscreenGate.cs  # 全屏自动隐藏门控
    ├── Native.cs          # P/Invoke 声明
    ├── WalkArt.cs         # .nwp 帧动画读取
    ├── GifArt.cs          # GIF 待机动画
    ├── EyeArt.cs          # 眼珠跟随
    ├── DragArt.cs         # 拖放张嘴 / 吞咽
    ├── FeedArt.cs         # 喂食表情
    └── Tests.cs           # --self-test 自动化用例
```

## 仓库目录

```
├── 源码/                  # 全部源码（构建 exe 唯一必需的内容）
├── *.nwp                  # 素材包（散帧打包，程序运行时从 exe 同目录读取）
├── 原版笑声.wav / cry.wav # 音效
├── make_*.py              # 素材打包脚本（PNG/GIF/WebP → .nwp，方便复现）
├── 使用说明.txt           # 面向使用者的操作说明
├── 使用说明_可编辑版.docx # 同上，可编辑版
├── setup.iss              # Inno Setup 安装脚本
├── ChineseSimplified.isl  # Inno Setup 简体中文语言文件
├── 饿一点.bat             # 手动减 1 点饥饿（调试用，等价 --hungry）
├── README.md / LICENSE / .gitignore
```

### 文件归属

| 类别 | 放哪里 |
|---|---|
| 源码 | `源码/` 目录，随仓库提交 |
| 素材（`.nwp` / `.wav` / `.mp3`） | 仓库根目录（程序运行时从 exe 同目录读取）；超 20MB 的走 Releases |
| 素材生成脚本（`make_*.py`） | 仓库根目录 |
| 安装包 / 完整压缩包 | **不进仓库**，发布到 **GitHub Releases** |
| 编译产物（`奶蛙桌宠.exe`） | 不入库（`.gitignore` 忽略 `*.exe`），由 `build.cmd` 构建或从 Releases 获取 |

---

## 交互速查

| 操作 | 效果 |
|---|---|
| 右键菜单 / 托盘右键 | 饥饿条 / 使用说明书 / 设置（查看动作·大小·声音·音量·自启·全屏隐藏·退出） |
| 双击肚子 | 大笑 |
| 双击头部 | 起势 → 愤怒 → 打电话（炸开 6 只小奶蛙跳舞） |
| 拖动奶蛙 | 举起（惊恐）→ 松手下落，可上抛 |
| 上抛落地 | 哭泣 |
| 闲置 | 待机小动作 / 转手舞，久置入睡 |
| 拖文件到身上 | 张嘴回收进回收站 |
| 拖到屏幕右缘 | 贴边隐藏只露头 |
| 鼠标靠近 | 唤醒 / 停走 |

---

## 素材说明

- 全部 `.nwp` 素材由本地脚本（`make_*.py`）从 PNG/GIF/WebP 帧打包生成，`.nwp` 为本项目自制格式（ZipArchive + PNG 帧 + `metadata.json`）。
- 音效（`原版笑声.wav`、`cry.wav`、`dance.mp3`）为本地转换 / 处理生成。
- 素材形象来源如涉及第三方版权，**商用前请自行确认素材来源与授权**；个人学习交流不受影响。

---

## 开源协议

本项目使用 [MIT License](LICENSE) 开源。

> 源码由 AI 辅助生成，经人工逐项调试验证。
