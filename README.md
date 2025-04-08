# PlcCommunicator

PlcCommunicator 是一个基于 C# WPF 开发的 ModBus TCP 通信模拟器，采用 MVVM 架构模式设计。该工具可以模拟 ModBus TCP 服务器，用于工业自动化测试和开发环境中的通信调试。

## 🚧 项目状态

**注意：此项目尚未完成，正在开发中。**

当前未完成的功能：
- ModBus TCP 服务器核心功能实现
- 客户端连接管理
- 数据导出功能
- 异常处理机制
- 完整的日志系统
- 用户界面优化

## ✨ 主要功能

- ModBus TCP 服务器模拟
  - 支持标准 ModBus TCP 端口(502)配置
  - 支持自定义从站 ID (1-247)
- 寄存器操作
  - 读写保持寄存器
  - 批量寄存器操作
- 线圈操作
  - 读写线圈状态
  - 批量线圈操作
- 实时监控
  - 服务器运行状态显示
  - 客户端连接数监控
  - 实时数据变化监控
- 日志系统
  - 操作日志记录
  - 自动滚动显示
  - 日志导出功能

## 🛠 技术栈

- .NET 8.0
- WPF (Windows Presentation Foundation)
- MVVM 架构模式
- NModbus 库
- DryIoc (依赖注入容器)
- Microsoft.Xaml.Behaviors

## 📦 项目结构

```
PlcCommunicator/
├── Commands/           # 命令实现
├── Events/            # 事件定义
├── Models/            # 数据模型
├── Services/          # 服务层
│   ├── Implements/    # 服务实现
│   └── Interfaces/    # 服务接口
├── ViewModels/        # 视图模型
└── Views/             # 视图
```

## 🚀 开发环境要求

- Visual Studio 2022 或更高版本
- .NET 8.0 SDK
- Windows 操作系统

## 📝 待完成功能

1. 核心功能实现
   - [ ] ModBus TCP 服务器启动/停止功能
   - [ ] 客户端连接管理
   - [ ] 数据读写操作实现

2. 用户界面
   - [ ] 数据显示优化
   - [ ] 状态指示优化
   - [ ] 操作响应优化

3. 数据管理
   - [ ] 数据导出功能
   - [ ] 数据持久化
   - [ ] 配置保存功能

4. 系统稳定性
   - [ ] 异常处理机制
   - [ ] 日志系统完善
   - [ ] 性能优化


---

**注意：** 此项目仍在开发中，功能完全不稳定(因为压根未实现)。