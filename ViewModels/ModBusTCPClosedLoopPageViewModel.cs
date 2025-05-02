using NModbus;
using PlcCommunicator.Commands;
using PlcCommunicator.Events;
using PlcCommunicator.Models;
using PlcCommunicator.Services.Configuration;
using PlcCommunicator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PlcCommunicator.ViewModels
{
    /// <summary>
    /// ModBus TCP服务页面的视图模型，处理UI和ModBus服务器之间的交互
    /// </summary>
    public class ModBusTCPClosedLoopPageViewModel : ViewModelBase
    {
        private readonly TcpStatusSharedService _sharedService;
        private readonly IModbusConfigurationService _configService;
        private ModbusTcpClosedLoopOptions _closedLoopConfig => _configService.GetClosedLoopConfig();
        private readonly IEventAggregator _eventAggregator;
        private readonly IModbusTcpMasterClosedLoopService _closedLoopMasterService;
        private readonly IModbusTcpSlaveClosedLoopService _closedLoopSlaveService;

        private bool _isRunning => _sharedService.IsClosedLoopServiceReady;// 服务器运行状态标志


        // --- 用于 UI 绑定的编辑状态对象 ---      
        private ModbusTcpClosedLoopOptions _uiConfig;

        public ModBusTCPClosedLoopPageViewModel(TcpStatusSharedService? sharedService,
            IModbusConfigurationService? configService,
            IEventAggregator? eventAggregator,
            IModbusTcpMasterClosedLoopService? closedLoopMasterService,
            IModbusTcpSlaveClosedLoopService? closedLoopSlaveService)
        {
            _sharedService = sharedService ?? throw new ArgumentNullException("共享服务类对象为空！");
            _configService = configService ?? throw new ArgumentNullException("配置服务类对象为空！");
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException("事件聚合器类对象为空！");
            _closedLoopMasterService = closedLoopMasterService ?? throw new ArgumentNullException("ModBusTCP闭环测试主站服务类对象为空！");
            _closedLoopSlaveService = closedLoopSlaveService ?? throw new ArgumentNullException("ModBusTCP闭环测试从站服务类对象为空！");

            ToggleServerCommand = new RelayCommand(StartOrStopServiceAsync, CanStartOrStopService);

            LoadConfiguration();
        }

        #region 服务器设置
        public string IPAddress // 服务器端口，默认 ModBus TCP 端口为 502
        {
            get => _uiConfig?.IpAddress ?? "";
            set
            {
                // 这里我们想改变的是 _uiConfig.IpAddress，而不是一个本地的后备字段
                if (_uiConfig != null && _uiConfig.IpAddress != value)
                {
                    _uiConfig.IpAddress = value; // 直接修改 _uiConfig 对象fig 对象
                                                 // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                }
            }
        }

        public int Port// 服务器端口，默认 ModBus TCP 端口为 502
        {
            get => _uiConfig?.Port ?? 502;
            set
            {
                // 这里我们想改变的是 _uiConfig.IpAddress，而不是一个本地的后备字段
                if (_uiConfig != null && _uiConfig.Port != value)
                {
                    _uiConfig.Port = value; // 直接修改 _uiConfig 对象fig 对象
                                            // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                }
            }
        }

        public byte SlaveId // 从站 ID，ModBus 设备标识符，范围通常为 1-247
        {
            get => _uiConfig?.UnitId ?? 1;
            set
            {
                // 这里我们想改变的是 _uiConfig.IpAddress，而不是一个本地的后备字段
                if (_uiConfig != null && _uiConfig.UnitId != value)
                {
                    _uiConfig.UnitId = value; // 直接修改 _uiConfig 对象fig 对象
                                              // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                }
            }
        }
        #endregion

        #region 服务器状态
        private string _serverStatus = "未启动"; // 服务器状态描述文本
        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }



        private int _connectionCount; // 客户端连接数
        public int ConnectionCount
        {
            get => _connectionCount;
            set => SetProperty(ref _connectionCount, value);
        }

        private string _statusMessage = "服务器就绪"; // 状态栏消息
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        #endregion

        #region 日志相关
        private ObservableCollection<string> _logMessages = new ObservableCollection<string>();// 日志消息集合，用于UI显示
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            private set => SetProperty(ref _logMessages, value);
        }


        private bool _autoScroll = true; // 是否自动滚动到最新日志
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }
        #endregion

        #region 命令
        public RelayCommand ToggleServerCommand { get; } // 启动/停止服务器命令

        public IAsyncCommand UpdateRegisterCommand { get; } // 更新保持寄存器命令

        public ICommand UpdateCoilCommand { get; } // 更新线圈命令

        public ICommand ShowRegistersCommand { get; } // 显示寄存器窗口命令

        public ICommand ShowCoilsCommand { get; } // 显示线圈窗口命令

        public ICommand ClearLogCommand { get; } // 清空日志命令

        public IAsyncCommand ExportLogCommand { get; } // 导出日志命令

        public ICommand SaveConfigCommand { get; } // 保存配置命令

        public ICommand LoadConfigCommand { get; } // 加载配置命令

        #endregion



        private void LoadConfiguration()
        {
            try
            {
                var persistedConfig = _configService.GetClosedLoopConfig();
                if (persistedConfig != null)
                {
                    _uiConfig = new ModbusTcpClosedLoopOptions
                    {
                        IpAddress = persistedConfig.IpAddress,
                        Port = persistedConfig.Port,
                        UnitId = persistedConfig.UnitId,
                    };
                }
                else
                {
                    Debug.WriteLine($"加载配置失败, 配置类对象为空，创建默认配置！");
                    _uiConfig = new ModbusTcpClosedLoopOptions();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载配置失败: {ex.Message}");
                _uiConfig = new ModbusTcpClosedLoopOptions();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfiguration()
        {
            if (_closedLoopConfig == null)
            {
                _configService.GetClosedLoopConfig();
            }

            try
            {
                // 保存配置
                _configService.SaveClosedLoopConfig(_closedLoopConfig);

                // 添加日志记录
                Debug.WriteLine("ModBus TCP闭环测试配置已保存");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        private async Task StartOrStopServiceAsync()
        {
            if (_isRunning)
            {
                try
                {
                    await _closedLoopMasterService.DisconnectAsync();
                    await _closedLoopSlaveService.StopServiceAsync();
                }
                finally
                {
                    Debug.WriteLine($"闭环测试服务完全关闭");
                }
            }
            else if (!_isRunning)
            {
                try
                {
                    await _closedLoopSlaveService.StartServiceAsync();
                    await _closedLoopMasterService.ConnectAsync();
                }
                catch (SocketException sockEx)
                {
                    Debug.WriteLine($"{sockEx}。正在尝试关闭服务......");
                    try
                    {
                        await _closedLoopMasterService.DisconnectAsync();
                        await _closedLoopSlaveService.StopServiceAsync();
                    }
                    catch { }// 停止服务失败时通常是重复清理或是引用已失效对象，默认忽略

                    Debug.WriteLine($"闭环测试服务完全关闭");
                }
                catch (IOException ioEx)
                {
                    Debug.WriteLine($"{ioEx}。正在尝试关闭服务......");
                    try
                    {
                        await _closedLoopMasterService.DisconnectAsync();
                        await _closedLoopSlaveService.StopServiceAsync();
                    }
                    catch { }// 停止服务失败时通常是重复清理或是引用已失效对象，默认忽略

                    Debug.WriteLine($"闭环测试服务完全关闭");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"闭环测试服务启动失败：{ex}。正在尝试关闭服务......");
                    try
                    {
                        await _closedLoopMasterService.DisconnectAsync();
                        await _closedLoopSlaveService.StopServiceAsync();
                    }
                    catch { }// 停止服务失败时通常是重复清理或是引用已失效对象，默认忽略

                    Debug.WriteLine($"闭环测试服务完全关闭");
                }
            }
        }

        private bool  CanStartOrStopService()
        {
            return !(ToggleServerCommand.IsExecuting);
        }
    }
}
