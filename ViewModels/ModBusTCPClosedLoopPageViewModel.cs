using NModbus;
using ModbusCommunicator.Commands;
using ModbusCommunicator.Events;
using ModbusCommunicator.Events.PrismEventAggregator;
using ModbusCommunicator.Models;
using ModbusCommunicator.Services.Configuration;
using ModbusCommunicator.Services.Interfaces;
using ModbusCommunicator.Services.Interfaces.ModbusTcpClosedLoopServices;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ModbusCommunicator.ViewModels
{
    /// <summary>
    /// ModBus TCP服务页面的视图模型，处理UI和ModBus服务器之间的交互
    /// </summary>
    public class ModBusTCPClosedLoopPageViewModel : ViewModelBase
    {
        private readonly TcpStatusSharedService _sharedService;
        private readonly IModbusTcpConfigurationService _configService;
        private ModbusTcpClosedLoopOptions _closedLoopConfig => _configService.GetClosedLoopConfig();
        private readonly IEventAggregator _eventAggregator;
        private readonly IModbusTcpMasterClosedLoopService _closedLoopMasterService;
        private readonly IModbusTcpSlaveClosedLoopService _closedLoopSlaveService;

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

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                SetProperty(ref _isRunning, value);
            }
        }


        // --- 用于 UI 绑定的编辑状态对象 ---      
        private ModbusTcpClosedLoopOptions _uiConfig;

        public ModBusTCPClosedLoopPageViewModel(TcpStatusSharedService? sharedService,
            IModbusTcpConfigurationService? configService,
            IEventAggregator? eventAggregator,
            IModbusTcpMasterClosedLoopService? closedLoopMasterService,
            IModbusTcpSlaveClosedLoopService? closedLoopSlaveService)
        {
            _sharedService = sharedService ?? throw new ArgumentNullException("共享服务类对象为空！");
            _configService = configService ?? throw new ArgumentNullException("配置服务类对象为空！");
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException("事件聚合器类对象为空！");
            _closedLoopMasterService = closedLoopMasterService ?? throw new ArgumentNullException("ModBusTCP闭环测试主站服务类对象为空！");
            _closedLoopSlaveService = closedLoopSlaveService ?? throw new ArgumentNullException("ModBusTCP闭环测试从站服务类对象为空！");

            _eventAggregator.GetEvent<ClosedLoopStatusChangedEvent>().Subscribe(ClosedLoopServiceChanged);

            ToggleServerCommand = new RelayCommand(StartOrStopServiceAsync, CanStartOrStopService);
            SaveConfigCommand = new RelayCommand(SaveConfiguration);

            LoadConfiguration();
        }

        #region 服务器设置
        public string IPAddress // 服务器端口，默认 ModBus TCP 端口为 502
        {
            get => _uiConfig?.IpAddress ?? "192.168.0.1";
            set
            {
                // 这里我们想改变的是 _uiConfig.IpAddress，而不是一个本地的后备字段
                if (_uiConfig != null && _uiConfig.IpAddress != value)
                {
                    _uiConfig.IpAddress = value; // 直接修改 _uiConfig 对象fig 对象
                                                 // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                    ValidateProperty();
                }
            }
        }

        public int Port// 服务器端口，默认 ModBus TCP 端口为 502
        {
            get => _uiConfig?.Port ?? 502;
            set
            {
                if (_uiConfig != null && _uiConfig.Port != value)
                {
                    _uiConfig.Port = value; // 直接修改 _uiConfig 对象fig 对象
                                            // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                    ValidateProperty();
                }
            }
        }

        public byte UnitId // 从站 ID，ModBus 设备标识符，范围通常为 1-247
        {
            get => _uiConfig?.UnitId ?? 1;
            set
            {
                if (_uiConfig != null && _uiConfig.UnitId != value)
                {
                    _uiConfig.UnitId = value; // 直接修改 _uiConfig 对象fig 对象
                                              // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged(); // <-- 调用 ViewModelBase 中的 OnPropertyChanged
                    ValidateProperty();
                }
            }
        }

        public ushort MaxCoils
        {
            get => _uiConfig?.MaxCoils ?? 100;
            set
            {
                if (_uiConfig != null && _uiConfig.MaxCoils != value)
                {
                    _uiConfig.MaxCoils = value; // 直接修改 _uiConfig 对象fig 对象
                                                // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public ushort MaxInputDiscretes
        {
            get => _uiConfig?.MaxInputDiscretes ?? 100;
            set
            {
                if (_uiConfig != null && _uiConfig.MaxInputDiscretes != value)
                {
                    _uiConfig.MaxInputDiscretes = value; // 直接修改 _uiConfig 对象fig 对象
                                                         // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public ushort MaxHoldingRegisters
        {
            get => _uiConfig?.MaxHoldingRegisters ?? 100;
            set
            {
                if (_uiConfig != null && _uiConfig.MaxHoldingRegisters != value)
                {
                    _uiConfig.MaxHoldingRegisters = value; // 直接修改 _uiConfig 对象fig 对象
                                                           // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public ushort MaxInputRegisters
        {
            get => _uiConfig?.MaxInputRegisters ?? 100;
            set
            {
                if (_uiConfig != null && _uiConfig.MaxInputRegisters != value)
                {
                    _uiConfig.MaxInputRegisters = value; // 直接修改 _uiConfig 对象fig 对象
                                                         // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public bool ReadOnly
        {
            get => _uiConfig?.ReadOnly ?? false;
            set
            {
                if (_uiConfig != null && _uiConfig.ReadOnly != value)
                {
                    _uiConfig.ReadOnly = value; // 直接修改 _uiConfig 对象fig 对象
                                                // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public int NumberOfRetries
        {
            get => _uiConfig?.NumberOfRetries ?? 3;
            set
            {
                if (_uiConfig != null && _uiConfig.NumberOfRetries != value)
                {
                    _uiConfig.NumberOfRetries = value; // 直接修改 _uiConfig 对象fig 对象
                                                       // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public int RetryDelayMilliseconds
        {
            get => _uiConfig?.RetryDelayMilliseconds ?? 20;
            set
            {
                if (_uiConfig != null && _uiConfig.RetryDelayMilliseconds != value)
                {
                    _uiConfig.RetryDelayMilliseconds = value; // 直接修改 _uiConfig 对象fig 对象
                                                              // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public int SendTimeoutMilliseconds
        {
            get => _uiConfig?.SendTimeoutMilliseconds ?? 500;
            set
            {
                if (_uiConfig != null && _uiConfig.SendTimeoutMilliseconds != value)
                {
                    _uiConfig.SendTimeoutMilliseconds = value; // 直接修改 _uiConfig 对象fig 对象
                                                               // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
        }

        public int ReceiveTimeoutMilliseconds
        {
            get => _uiConfig?.ReceiveTimeoutMilliseconds ?? 500;
            set
            {
                if (_uiConfig != null && _uiConfig.ReceiveTimeoutMilliseconds != value)
                {
                    _uiConfig.ReceiveTimeoutMilliseconds = value; // 直接修改 _uiConfig 对象fig 对象
                                                                  // 因为没有本地后备字段传给 SetProperty，所以需要手动调用 OnPropertyChanged
                    OnPropertyChanged();
                    ValidateProperty();
                }
            }
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

        private void ValidateProperty([CallerMemberName] string? propertyName = null)
        {
            Debug.WriteLine($"当前验证的属性名: {propertyName}");    

            if (propertyName == null) return;

            object? propertyValue = null;
            var ValidationResults = new List<ValidationResult>();

            switch (propertyName)
            {
                case nameof(IPAddress):
                    propertyValue = IPAddress;
                    break;
                case nameof(Port):
                    propertyValue = Port;
                    break;
                case nameof(UnitId):
                    propertyValue = UnitId;
                    break;
                case nameof(MaxCoils):
                    propertyValue = MaxCoils;
                    break;
                case nameof(MaxInputDiscretes):
                    propertyValue = MaxInputDiscretes;
                    break;
                case nameof(MaxHoldingRegisters):
                    propertyValue = MaxHoldingRegisters;
                    break;
                case nameof(MaxInputRegisters):
                    propertyValue = MaxInputRegisters;
                    break;
                case nameof(ReadOnly):
                    propertyValue = ReadOnly;
                    break;
                case nameof(NumberOfRetries):
                    propertyValue = NumberOfRetries;
                    break;
                case nameof(RetryDelayMilliseconds):
                    propertyValue = RetryDelayMilliseconds;
                    break;
                case nameof(SendTimeoutMilliseconds):
                    propertyValue = SendTimeoutMilliseconds;
                    break;
                case nameof(ReceiveTimeoutMilliseconds):
                    propertyValue = ReceiveTimeoutMilliseconds;
                    break;
                default:
                    return;
            }

            bool isValid = Validator.TryValidateProperty(propertyValue, 
                new ValidationContext(_uiConfig) { MemberName = propertyName },
                ValidationResults);

            if (isValid)
            {
                ClearError(propertyName);
            }
            else
            {
                SetError(propertyName, ValidationResults.First().ErrorMessage);
            }
        }



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
                        MaxCoils = persistedConfig.MaxCoils,
                        MaxInputDiscretes = persistedConfig.MaxInputDiscretes,
                        MaxHoldingRegisters = persistedConfig.MaxHoldingRegisters,
                        MaxInputRegisters = persistedConfig.MaxInputRegisters,
                        ReadOnly = persistedConfig.ReadOnly,
                        NumberOfRetries = persistedConfig.NumberOfRetries,
                        RetryDelayMilliseconds = persistedConfig.RetryDelayMilliseconds,
                        SendTimeoutMilliseconds = persistedConfig.SendTimeoutMilliseconds,
                        ReceiveTimeoutMilliseconds = persistedConfig.ReceiveTimeoutMilliseconds,
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



        private async Task StartOrStopServiceAsync() // 启动/停止闭环测试服务的Execute带入函数
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

        private bool CanStartOrStopService() // 启动/停止闭环测试服务的CanExecute带入函数
        {
            return !(ToggleServerCommand.IsExecuting);
        }

        private void SaveConfiguration() // 闭环测试服务配置储存的Execute带入函数
        {
            if (!HasErrors && IsConfigChanged())
            {
                var newConfig = new ModbusTcpClosedLoopOptions()
                {
                    IpAddress = this.IPAddress,
                    Port = this.Port,
                    UnitId = this.UnitId,
                    MaxCoils = this.MaxCoils,
                    MaxInputDiscretes = this.MaxInputDiscretes,
                    MaxHoldingRegisters = this.MaxHoldingRegisters,
                    MaxInputRegisters = this.MaxInputRegisters,
                    ReadOnly = this.ReadOnly,
                    NumberOfRetries = this.NumberOfRetries,
                    RetryDelayMilliseconds = this.RetryDelayMilliseconds,
                    SendTimeoutMilliseconds = this.SendTimeoutMilliseconds,
                    ReceiveTimeoutMilliseconds = this.ReceiveTimeoutMilliseconds,
                };

                try
                {
                    // 保存配置
                    _configService.SaveClosedLoopConfig(newConfig);

                    // 添加日志记录
                    Debug.WriteLine("ModBus TCP闭环测试配置已保存");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"保存配置失败: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("配置未更改或存在错误，无需保存");// 暂时使用Debug.WriteLine记录
            }
        }

        private bool IsConfigChanged()// 检查配置是否更改，用于判断是否能够保存配置
        {
            return _closedLoopConfig.IpAddress != this.IPAddress ||
                _closedLoopConfig.Port != this.Port ||
                _closedLoopConfig.UnitId != this.UnitId ||
                _closedLoopConfig.MaxCoils != this.MaxCoils ||
                _closedLoopConfig.MaxInputDiscretes != this.MaxInputDiscretes ||
                _closedLoopConfig.MaxHoldingRegisters != this.MaxHoldingRegisters ||
                _closedLoopConfig.MaxInputRegisters != this.MaxInputRegisters ||
                _closedLoopConfig.ReadOnly != this.ReadOnly ||
                _closedLoopConfig.NumberOfRetries != this.NumberOfRetries ||
                _closedLoopConfig.RetryDelayMilliseconds != this.RetryDelayMilliseconds ||
                _closedLoopConfig.SendTimeoutMilliseconds != this.SendTimeoutMilliseconds ||
                _closedLoopConfig.ReceiveTimeoutMilliseconds != this.ReceiveTimeoutMilliseconds;
        }

        private void ClosedLoopServiceChanged(bool isRunning) // 闭环测试服务状态改变事件
        {
            IsRunning = isRunning;
        }
    }
}
