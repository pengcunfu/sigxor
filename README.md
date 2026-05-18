# 鼠标左键长按语音输入程序

一个支持鼠标左键长按激活语音输入的 C# WPF 桌面应用程序，默认使用阿里 **SenseVoice** 模型进行多语言语音识别，也可切换为 Whisper。

## 功能特点

- **鼠标左键长按检测**：按住鼠标左键1.5秒激活语音输入
- **实时语音录制**：使用系统麦克风进行实时语音录制
- **AI 语音识别**：默认 SenseVoice（中英日韩粤），可选 Whisper Tiny
- **自动输入**：将识别的文字自动输入到当前焦点位置
- **可配置**：支持自定义长按时间、采样率等参数
- **离线运行**：模型下载后可离线使用

## 系统要求

- Windows 10/11
- .NET 10.0 Runtime（或 .NET SDK 用于开发）
- 麦克风设备
- 首次运行需要网络连接（下载模型）

## 快速开始

### 方法1: 直接运行（推荐）

```bash
# 构建项目
dotnet build -c Release

# 运行程序
bin\Release\net10.0-windows\win-x64\MouseClickVoice.exe
```

### 方法2: 使用 Visual Studio

1. 打开 `MouseClickVoice.slnx`
2. 按 F5 或点击"启动"按钮运行

### 方法3: 使用命令行

```bash
dotnet run
```

## 使用方法

1. **启动程序**
   - 运行 MouseClickVoice.exe

2. **首次使用**
   - 点击"开始服务"按钮
   - 等待 SenseVoice 模型自动下载（约 230MB，仅首次）
   - 模型下载完成后显示"SenseVoice 就绪"

3. **开始语音输入**
   - 在任意可输入文本的地方，按住鼠标左键1.5秒
   - 听到录音状态提示后开始说话
   - 松开鼠标左键停止录音
   - 程序自动识别并输入文字到当前位置

4. **调整设置**
   - 长按时间：滑动条调整（0.5-3.0秒）
   - 输入方式：键盘模拟 / 剪贴板粘贴
   - 通知：开启/关闭状态通知

## 配置选项

程序支持在主窗口界面中配置以下参数：

- **长按时间**：0.5-3.0秒可调（默认1.5秒）
- **识别语言**：中文
- **输入方式**：键盘模拟 / 剪贴板粘贴
- **通知设置**：开启/关闭状态通知
- **音频参数**：采样率、声道数、位深度

配置会自动保存到 `config.json` 文件中。

## 技术架构

### 核心模块

- **MouseHook**：Windows 钩子技术检测鼠标事件
- **AudioCapture**：NAudio 库实现音频捕获
- **SpeechRecognition**：SenseVoice（sherpa-onnx）/ Whisper.net 双引擎
- **TextSimulator**：Win32 API 模拟键盘输入
- **Config**：JSON 配置文件管理

### 技术栈

- **框架**：.NET 10.0 + WPF
- **音频**：NAudio 2.3.0
- **语音识别**：SenseVoice（org.k2fsa.sherpa.onnx 1.13.2）/ Whisper.net 1.9.0
- **架构**：x64

## 项目文件结构

```
MouseClickVoice/
├── AudioCapture.cs          # 音频捕获模块
├── Config.cs               # 配置管理
├── MainWindow.xaml         # 主窗口界面
├── MainWindow.xaml.cs      # 主窗口逻辑
├── MouseHook.cs            # 鼠标事件检测
├── SpeechRecognition.cs    # 语音识别模块
├── TextSimulator.cs        # 文本输入模拟
├── App.xaml                # 应用程序定义
├── AssemblyInfo.cs         # 程序集信息
├── MouseClickVoice.csproj  # 项目文件
├── MouseClickVoice.slnx    # 解决方案文件
└── global.json             # SDK 版本锁定
```

## 注意事项

1. **系统要求**：Windows 10/11 系统
2. **麦克风权限**：确保程序有麦克风访问权限
3. **管理员权限**：建议以管理员身份运行以获得全局鼠标钩子权限
4. **模型下载**：首次运行需要网络连接下载 Whisper 模型（约 40MB）
5. **隐私保护**：语音识别使用本地模型，数据不会上传到外部服务器（仅首次下载模型需联网）

## 故障排除

### 构建问题

```bash
# 清理并重新构建
dotnet clean
dotnet build -c Release

# 还原依赖
dotnet restore
```

### 音频设备问题

- 打开 Windows 设置 → 系统 → 声音
- 检查麦克风是否为默认设备
- 测试麦克风是否正常工作

### 权限问题

- Windows: 右键程序选择"以管理员身份运行"
- 在 Windows 设置中授予麦克风访问权限

### 模型下载问题

- 确保网络连接正常
- SenseVoice 模型保存在 `models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/`
- Whisper 模型保存在 `models/ggml-tiny.bin`
- SenseVoice 手动下载：[sherpa-onnx 发布页](https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2)，解压到 `models/` 目录

## 构建单文件发布版本

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

输出文件位于：`bin\Release\net10.0-windows\win-x64\publish\MouseVoiceInput.exe`

## 开发环境

- Visual Studio 2026（或 Visual Studio 2022 17.13+）
- .NET 10.0 SDK
- Windows SDK

## 许可证

MIT License
