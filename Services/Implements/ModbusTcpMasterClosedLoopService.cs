using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;
using NModbus.Data;
using NModbus.Logging;
using PlcCommunicator.Events.PrismEventAggregator;
using PlcCommunicator.Models;
using PlcCommunicator.Services.Configuration;
using PlcCommunicator.Services.Interfaces;
using PlcCommunicator.Services.Interfaces.ModbusTcpClosedLoopServices;
using Polly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcCommunicator.Services.Implements
{
    public class ModbusTcpMasterClosedLoopService : IModbusTcpMasterClosedLoopService
    {
        // --- 依赖注入字段 --- // 区域注释：通过构造函数注入的服务实例。
        private readonly ILogger<ModbusTcpClosedLoopOptions> _logger; // 日志记录器实例。
        private readonly IModbusFactory _modbusFactory; // NModbus 工厂实例，用于创建 Modbus 主站。
        private readonly TcpStatusSharedService _tcpSharedService;  // TCP 状态共享服务实例，用于与其他组件通信。
        private readonly IEventAggregator? _eventAggregator; // 事件聚合器实例，用于发布和订阅跨组件的事件。可为 null。
        private readonly IModbusTcpConfigurationService _configurationService;

        // --- 动态的私有字段 (受配置影响) --- // 区域注释：受配置选项影响的字段。
        private ModbusTcpClosedLoopOptions CurrentOptions => _configurationService.GetClosedLoopConfig();
        // 用于处理连接操作的异步策略，根据配置动态更新。初始化为无操作策略。
        // 类型更改为 IAsyncPolicy 以接受 NoOpAsync 和 RetryAsync 策略
        private IAsyncPolicy _connectRetryPolicy = Policy.NoOpAsync(); // 使用 IAsyncPolicy 接口
        // 用于处理读/写操作的异步策略，根据配置动态更新。初始化为无操作策略。
        // 类型更改为 IAsyncPolicy 以接受 NoOpAsync 和 RetryAsync 策略
        private IAsyncPolicy _operationRetryPolicy = Policy.NoOpAsync(); // 使用 IAsyncPolicy 接口

        // --- 连接状态字段 --- // 区域注释：与 TCP 连接和 Modbus 会话状态相关的字段。
        private TcpClient? _tcpClient; // TCP 客户端实例，用于底层 TCP 连接。可为 null。
        private IModbusMaster? _master; // Modbus 主站接口实例，用于执行 Modbus 操作。可为 null。
        private readonly SemaphoreSlim _connectionLock = new(1, 1); // 信号量，用于控制对连接和断开操作的并发访问，防止竞态条件。
        private long _disposed; // 标记服务是否已被释放，用于实现 IDisposable 模式。使用 long 是为了 Interlocked 操作。
        private IDisposable? _optionsChangeListener; // 用于持有配置变更订阅的句柄，以便在 Dispose 时取消订阅。
        private readonly CancellationTokenSource _cts = new(); //取消信号
        private CancellationToken token => _cts.Token; //返回取消令牌

        public bool IsConnected => _tcpClient?.Connected ?? false && _master != null; // 判断 TCP 客户端是否连接且 Modbus 主站实例存在

        public ModbusTcpMasterClosedLoopService(ILogger<ModbusTcpClosedLoopOptions> logger,
            IModbusTcpConfigurationService configurationService,
            IModbusFactory modbusFactory, TcpStatusSharedService tcpSharedService,
            IEventAggregator eventAggregator
            )
        {
            // 参数校验
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modbusFactory = modbusFactory ?? throw new ArgumentNullException(nameof(modbusFactory));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator)); ;
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService)); ;
            _tcpSharedService = tcpSharedService ?? throw new ArgumentNullException(nameof(tcpSharedService));

            _optionsChangeListener = _eventAggregator?.GetEvent<ClosedLoopConfigUpdatedEvent>().Subscribe(ApplyOptions, ThreadOption.PublisherThread);
            ApplyOptions(CurrentOptions);

            _logger.LogInformation("ModbusTcpMasterService 已初始化。当前目标 IP: {IpAddress}, 端口: {Port}",
            CurrentOptions.IpAddress, CurrentOptions.Port);
        }

        // --- 私有方法 --- // 区域注释：仅在类内部使用的方法。

        /// <summary>
        /// 应用配置选项。当配置更改或服务初始化时调用。
        /// </summary>
        /// <param name="options">要应用的配置选项。</param>
        private void ApplyOptions(ModbusTcpClosedLoopOptions options) // 私有方法：应用配置选项。
        {
            _logger.LogInformation("正在应用新的 Modbus TCP 配置: IP={IpAddress}, Port={Port}, Retries={Retries}, Delay={Delay}ms, SendTimeout={SendTimeout}ms, ReceiveTimeout={ReceiveTimeout}ms",
                options.IpAddress,
                options.Port,
                options.NumberOfRetries,
                options.RetryDelayMilliseconds,
                options.SendTimeoutMilliseconds,
                options.ReceiveTimeoutMilliseconds);

            // --- 配置连接重试策略 ---
            if (options.NumberOfRetries > 0) // 如果配置了重试次数
            {
                // 创建一个异步重试策略
                // 显式转换为 IAsyncPolicy (虽然赋值时会自动转换，但显式写出更清晰)
                _connectRetryPolicy = (IAsyncPolicy)Policy // 开始定义 Polly 策略。
                    .Handle<SocketException>() // 指定处理 SocketException 网络层错误。
                    .Or<TimeoutException>()    // 指定处理 TimeoutException 连接超时错误。
                    .Or<IOException>()         // 指定处理 IOException 流相关错误 (SocketException 是其子类)。
                    .WaitAndRetryAsync( // 配置等待和重试逻辑
                        options.NumberOfRetries, // 重试次数
                        retryAttempt => TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds), // 每次重试的延迟时间
                        (exception, timeSpan, retryCount, context) => // 重试时执行的回调
                        {
                            _logger.LogWarning(exception, "连接失败 (第 {RetryCount} 次尝试)，将在 {Delay}ms 后重试...",
                            retryCount, timeSpan.TotalMilliseconds);
                        });
            }
            else // 如果未配置重试次数
            {
                // 使用无操作策略，即不进行重试
                _connectRetryPolicy = Policy.NoOpAsync();
            }

            // --- 配置操作重试策略 ---
            if (options.NumberOfRetries > 0) // 如果配置了重试次数
            {
                // 创建一个异步重试策略
                // 显式转换为 IAsyncPolicy
                _operationRetryPolicy = (IAsyncPolicy)Policy // 开始定义 Polly 策略。
                    .Handle<IOException>() // 指定处理 IOException (覆盖 SocketException 等流相关错误)。
                    .Or<TimeoutException>() // 指定处理 TimeoutException (可能是 NModbus 内部或 Stream 的超时)。
                                            // 注意：可以根据需要添加或排除其他特定异常，例如 ModbusException
                                            // .Or<Modbus.SlaveException>() // 如果需要重试 Modbus 从站异常
                    .WaitAndRetryAsync( // 配置等待和重试逻辑
                        options.NumberOfRetries, // 重试次数
                        retryAttempt => TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds), // 每次重试的延迟时间
                        (exception, timeSpan, retryCount, context) => // 重试时执行的回调
                        {
                            _logger.LogWarning(exception, "Modbus 操作失败 (第 {RetryCount} 次尝试)，将在 {Delay}ms 后重试...",
                                               retryCount, timeSpan.TotalMilliseconds);
                        });
            }
            else // 如果未配置重试次数
            {
                // 使用无操作策略，即不进行重试
                _operationRetryPolicy = Policy.NoOpAsync();
            }
        }

        public async Task ConnectAsync()
        {
            ThrowIfDisposed(); // 检查服务是否已释放

            if (IsConnected) // 如果已连接，则直接返回
            {
                _logger.LogDebug("已连接到 Modbus TCP 从站，跳过连接操作。");
                return;
            }

            await _connectionLock.WaitAsync(token).ConfigureAwait(false); // 使用信号量确保同一时间只有一个线程执行连接逻辑

            try
            {
                if (IsConnected) // 再次检查连接状态，防止在等待锁期间其他线程已成功连接
                {
                    _logger.LogDebug("在获取锁后发现已连接，跳过连接操作。");
                    return;
                }

                ValidateTcpOptions(CurrentOptions); // 校验 IP 地址

                var ipAddress = CurrentOptions.IpAddress; // 获取当前 IP 地址
                var port = CurrentOptions.Port; // 获取当前端口号

                _logger.LogInformation("尝试连接到 Modbus TCP 从站: {IpAddress}:{Port}...", ipAddress, port);

                // 获取当前的连接重试策略，类型为 IAsyncPolicy
                var currentConnectPolicy = _connectRetryPolicy;
                var currentRetries = CurrentOptions.NumberOfRetries;
                var currentRetryDelay = CurrentOptions.RetryDelayMilliseconds;

                // 执行带重试的连接操作
                var policyResult = await currentConnectPolicy.ExecuteAndCaptureAsync(async ct =>
                {
                    CleanupConnectionResources(); // 清理旧的连接资源（如果存在）

                    // 创建新的 TCP 客户端
                    _tcpClient = new TcpClient // 注意：TcpClient 的构造函数不执行连接
                    {
                        // 根据配置设置发送和接收超时
                        SendTimeout = CurrentOptions.SendTimeoutMilliseconds,
                        ReceiveTimeout = CurrentOptions.ReceiveTimeoutMilliseconds
                    };

                    var address = default(IPAddress);

                    if (!(IPAddress.TryParse(ipAddress, out address) && address != null))
                    {
                        _logger.LogError(new ArgumentException(), "主站连接使用无效IP地址:{IpAddress} ！", ipAddress);
                        throw new ArgumentException($"主站连接使用无效IP地址:{ipAddress}");
                    }

                    _logger.LogDebug("正在连接 TCP 到 {IpAddress}:{Port}...", ipAddress, port);
                    // 异步连接 (这是实际发起连接的地方)

                    await _tcpClient.ConnectAsync(address, port, ct).ConfigureAwait(false); // 使用 CancellationToken
                    _logger.LogDebug("TCP 连接成功。");

                    _eventAggregator?.GetEvent<ClosedLoopStatusChangedEvent>().Publish(true);

                }, token).ConfigureAwait(false); // 将 CancellationToken 传递给 Polly

                // 检查 Polly 执行结果
                if (policyResult.Outcome == OutcomeType.Failure)
                {
                    _logger.LogError(policyResult.FinalException,
                                     "连接到 Modbus TCP 从站失败 (IP: {IpAddress}, Port: {Port})。尝试了 {Retries} 次重试，延迟 {Delay}ms。",
                                     ipAddress, port, currentRetries, currentRetryDelay);
                    // 连接失败后，确保资源被清理
                    CleanupConnectionResources();
                    // 抛出最终捕获的异常
                    throw policyResult.FinalException ?? new IOException("从站连接失败，且 Polly 未捕获到具体异常。");
                }
            }
            finally
            {
                _connectionLock.Release(); // 释放信号量
            }
        }

        /// <summary>
        /// 清理 TCP 连接和 Modbus 主站资源。
        /// 安全地释放 TcpClient 和 IModbusMaster 实例。
        /// </summary>
        private void CleanupConnectionResources() // 私有方法：清理连接资源。
        {
            _logger.LogDebug("正在清理 Modbus 连接资源...");

            // 安全地释放 Modbus Master 实例
            if (_master != null)
            {
                _logger.LogTrace("将 Modbus Master 实例转换为 IDisposable 对象进行释放。");
                try
                {
                    (_master as IDisposable)?.Dispose();
                    _logger.LogTrace("ModbusMaster释放完成, 赋值实例为 null。");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "在清理 ModbusMaster 资源时发生异常。"); // 记录在关闭/释放过程中发生的任何异常，但不应阻止继续执行
                }
                finally
                {
                    _master = null; // 解除引用
                }
            }

            // 安全地释放 TcpClient 实例
            if (_tcpClient != null)
            {
                try
                {
                    // 关闭连接并释放资源
                    _logger.LogTrace("正在关闭并释放 TcpClient...");
                    _tcpClient.Close(); // 关闭连接
                    _tcpClient.Dispose(); // 释放资源
                    _logger.LogTrace("TcpClient 已关闭并释放。");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "在清理 TcpClient 资源时发生异常。"); // 记录在关闭/释放过程中发生的任何异常，但不应阻止继续执行
                }
                finally
                {
                    _tcpClient = null; // 确保引用被清除
                }
                _logger.LogDebug("Modbus 连接资源清理完毕。");
            }
        }

        private void ValidateTcpOptions(ModbusTcpClosedLoopOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.IpAddress) || !IPAddress.TryParse(options.IpAddress, out _))
            {
                throw new ArgumentException();
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 如果服务已被释放，则抛出 ObjectDisposedException。
        /// </summary>
        /// <exception cref="ObjectDisposedException">当服务实例已被释放时抛出。</exception>
        private void ThrowIfDisposed() // 私有方法：检查对象是否已释放。
        {
            if (Interlocked.Read(ref _disposed) == 1) // 读取释放标志
            {
                throw new ObjectDisposedException(GetType().Name); // 抛出异常
            }
        }

        public async Task DisconnectAsync()
        {
            // 这里不检查 Disposed，允许在 Dispose 时调用 Disconnect
            if (!IsConnected && _tcpClient == null && _master == null) // 如果未连接且资源已清理，则直接返回
            {
                _logger.LogDebug("未连接或资源已清理，跳过断开操作。");
                return;
            }

            // 使用信号量确保同一时间只有一个线程执行断开逻辑
            await _connectionLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                _logger.LogInformation("正在断开与 Modbus TCP 从站的连接...");
                CleanupConnectionResources(); // 调用内部方法清理资源
                _logger.LogInformation("与 Modbus TCP 从站的连接已断开。");
            }
            finally
            {
                _connectionLock.Release(); // 释放信号量
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool[]> ReadDiscreteInputsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteMultipleRegistersAsync(byte slaveAddress, ushort startAddress, ushort[] data, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }




    public class ModbusTcpSlaveClosedLoopService : IModbusTcpSlaveClosedLoopService
    {
        #region 私有字段 (Private Fields)

        // --- NModbus 核心对象 ---
        private IModbusSlaveNetwork? _slaveNetwork; // Modbus 从站网络层
        private IModbusSlave? _slave;               // Modbus 从站逻辑单元
        private SlaveDataStore? _slaveDataStore;    // 从站数据存储区
        private IModbusFactory? _modbusFactory;     // NModbus 工厂实例

        // --- 网络监听器 ---
        private TcpListener? _listener;             // TCP 监听器

        // --- 服务配置 ---
        private ModbusTcpClosedLoopOptions _currentOptions => _configurationService.GetClosedLoopConfig(); // 当前生效的配置
        private readonly ILogger _logger;
        private readonly IModbusTcpConfigurationService _configurationService;
        private readonly TcpStatusSharedService _tcpSharedService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDisposable? _optionsChangeListener; // 用于取消注册

        // --- 状态与控制 ---
        private volatile bool _isRunning;             // 服务是否正在运行的标志
        private CancellationTokenSource? _cts;        // 用于取消后台任务
        private Task? _listenerTask;                // 后台监听任务

        // --- 线程同步锁 ---
        // 统一操作锁：保护 Start, Stop, Standalone Initialize, 和配置变更重启这四个互斥操作
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        // 数据区读写锁：允许多读单写，用于保护各个数据区的并发访问
        private readonly ReaderWriterLockSlim _coilLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _inputDiscreteLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _holdingRegisterLock = new ReaderWriterLockSlim();
        private readonly ReaderWriterLockSlim _inputRegisterLock = new ReaderWriterLockSlim();

        // --- IDisposable 相关 ---
        private bool _disposed = false; // 标记是否已释放

        #endregion

        public ModbusTcpSlaveClosedLoopService(ILogger<ModbusTcpClosedLoopOptions> logger,
            IModbusTcpConfigurationService configurationService,
            IModbusFactory modbusFactory, 
            TcpStatusSharedService tcpSharedService,
            IEventAggregator eventAggregator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _modbusFactory = modbusFactory ?? throw new ArgumentNullException(nameof(modbusFactory));
            _tcpSharedService = tcpSharedService ?? throw new ArgumentNullException(nameof(tcpSharedService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));

            _optionsChangeListener = _eventAggregator.GetEvent<ClosedLoopConfigUpdatedEvent>().Subscribe(HandleOptionsChanged);

            LogCurrentConfig("构造函数 - 初始"); // 记录初始配置



            _modbusFactory = new ModbusFactory(logger: new ConsoleModbusLogger(LoggingLevel.Warning));// 此处指定使用 ConsoleModbusLogger，并设定日志级别为 Warning，便于观察警告及以上级别的日志信息
            Debug.WriteLine($"ModbusTcpService 已初始化。配置监听器已注册。");
        }

        // --- 公共启动方法 (Public Start Method) ---
        /// <summary>
        /// 异步启动 Modbus TCP 从站服务。
        /// 此方法是线程安全的，使用 _operationLock 保证与停止、初始化、配置变更互斥。
        /// </summary>
        /// <returns>表示异步启动操作的任务。</returns>
        public async Task StartServiceAsync()
        {
            await _operationLock.WaitAsync(); // 等待操作锁
            Debug.WriteLine("StartServiceAsync: 已获取操作锁。");

            try
            {
                ValidateTcpOptions(_currentOptions); // 验证初始配置

                if (_isRunning)  // 如果服务已在运行，则直接返回，避免重复启动
                {
                    Debug.WriteLine("StartServiceAsync: 服务已在运行中，无需再次启动。");
                    return;
                }
                else
                {
                    Debug.WriteLine("StartServiceAsync: 准备调用内部启动逻辑...");
                    await StartInternalAsync(); // 调用包含核心启动逻辑的内部方法
                    Debug.WriteLine("StartServiceAsync: 内部启动逻辑已完成。");
                }
            }
            finally
            {
                _operationLock.Release();
                Debug.WriteLine("StartServiceAsync: 已释放操作锁。");
            }
        }

        private async Task StartInternalAsync()
        {
            Debug.WriteLine("StartInternalAsync: 开始执行核心启动步骤...");

            bool startupSuccessful = false; // 标记启动是否成功
            _cts = new CancellationTokenSource(); // 创建新的 CancellationTokenSource

            // 获取当前配置，确保后续 try/catch 都可访问
            var config = _currentOptions;
            var ipAddress = config.IpAddress;
            var port = config.Port;
            var unitId = config.UnitId;
            Debug.WriteLine($"StartInternalAsync: 使用配置 IP={ipAddress}, Port={port}, UnitId={unitId}");

            try
            {
                await InitializeDataStoreInternalAsync(false); //数据初始化并创建自定义DataStore
                Debug.WriteLine("StartInternalAsync: 数据存储区初始化完成。");

                if (_slaveDataStore == null) // 确保数据存储区已成功创建
                {
                    throw new InvalidOperationException("DataStore 在 StartInternalAsync 中未能成功初始化。");
                }
                _modbusFactory ??= new ModbusFactory(logger: new ConsoleModbusLogger(LoggingLevel.Warning)); // 如果工厂未创建则创建
                _slave = _modbusFactory.CreateSlave(unitId, _slaveDataStore); // 创建从站，关联数据存储
                Debug.WriteLine($"StartInternalAsync: Modbus 从站 (ID: {unitId}) 实例已创建。");

                _listener = new TcpListener(IPAddress.Parse(ipAddress), port); // 创建 TCP 监听器
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); // 设置地址重用，以便快速重启服务
                _listener.Start(); // 开始监听传入连接
                Debug.WriteLine($"StartInternalAsync: TCP 监听器已在 {ipAddress}:{port} 上启动。");

                _slaveNetwork = _modbusFactory.CreateSlaveNetwork(_listener);
                _slaveNetwork.AddSlave(_slave);
                Debug.WriteLine("StartInternalAsync: Modbus 从站网络层已创建并关联从站。");

                _listenerTask = Task.Run(async () => { await ListenLoopAsync(_cts.Token); }, _cts.Token);
                Debug.WriteLine("StartInternalAsync: 后台监听任务已启动。");

                startupSuccessful = true; // 标记启动成功
                _isRunning = true; // 正式将服务状态标记为运行中
                Debug.WriteLine($"StartInternalAsync: 服务已成功启动。");

            }
            catch (SocketException sockEx) // 特别处理端口占用等网络错误
            {
                Debug.WriteLine($"StartInternalAsync: 启动时发生 Socket 错误 (端口可能被占用): {sockEx.Message}");
                _isRunning = false;
                await CleanupFailedStartAsync(_cts); // 清理资源
                throw; // 重新抛出异常，让外部调用者知道启动失败
            }
            catch (Exception ex) // 捕获其他所有启动错误
            {
                Debug.WriteLine($"StartInternalAsync: 启动过程中发生未处理的错误: {ex.Message}");
                Debug.WriteLine($"StartInternalAsync: 错误详情: {ex.StackTrace}"); // 记录堆栈跟踪
                _isRunning = false; // 确保启动失败时状态正确
                await CleanupFailedStartAsync(_cts); // 清理资源
                throw; // 重新抛出异常，让外部调用者知道启动失败
            }
        }

        private async Task CleanupFailedStartAsync(CancellationTokenSource? token)
        {
            Debug.WriteLine("CleanupFailedStartAsync: 正在清理启动失败时遗留的资源...");

            if (token != null)
            {
                try { token.Cancel(); } catch (ObjectDisposedException) { Debug.WriteLine($"取消令牌资源未获取或已释放，忽略异常。"); }
            }

            if (_slaveNetwork != null)
            {
                try { _slaveNetwork.Dispose(); } catch (Exception ex) { Debug.WriteLine($"CleanupFailedStartAsync: 释放 _slaveNetwork 时出错: {ex.Message}"); }
                _slaveNetwork = null;
            }

            if (_listener != null)
            {
                try { _listener.Stop(); } catch (Exception ex) { Debug.WriteLine($"CleanupFailedStartAsync: 停止 _listener 监听时出错: {ex.Message}"); }
                _listener = null;
            }

            if (_listenerTask != null && !_listenerTask.IsCompleted) // 等待可能已启动的任务结束 (短暂等待)
            {
                await WaitWithTimeoutAsync(_listenerTask, TimeSpan.FromSeconds(1)); // 最多等1秒
            }
            _listenerTask = null; // 清理任务引用

            if (_slave != null)
            {
                try { (_slave as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"CleanupFailedStartAsync: 释放 _slave 时出错: {ex.Message}"); }
                _slave = null;
            }

            if (_cts == token) _cts = null;

            _slaveDataStore = null; // 清理 DataStore 引用
            Debug.WriteLine("CleanupFailedStartAsync: 清理完毕。");
        }

        private async Task WaitWithTimeoutAsync(Task? taskToWait, TimeSpan timeSpan)
        {
            if (taskToWait == null || taskToWait.IsCompleted) return;

            var taskToTimeout = Task.Delay(timeSpan);
            var completedTask = await Task.WhenAny(taskToTimeout, taskToWait);

            if (completedTask == taskToWait)
            {
                try
                {
                    await taskToWait;
                    Debug.WriteLine("WaitWithTimeoutAsync: 任务在超时时间内成功结束。");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("WaitWithTimeoutAsync: 任务在超时时间内被成功取消。");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"WaitWithTimeoutAsync: 任务在超时时间内结束，但抛出异常: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"WaitWithTimeoutAsync: 警告: 等待任务结束超时 ({timeSpan.TotalSeconds}秒)。任务状态: {taskToWait.Status}");
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            if (_slaveNetwork == null)
            {
                Debug.WriteLine("ListenLoopAsync: 错误：SlaveNetwork 为空，无法开始监听。");
                return; // 防御性编程
            }
            try
            {
                Debug.WriteLine("ListenLoopAsync: 开始监听 Modbus 请求...");
                await _slaveNetwork.ListenAsync(token).ConfigureAwait(false);
                Debug.WriteLine("ListenLoopAsync: 异步循环监听已返回 (很可能是由于 CancellationToken 触发)。");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("ListenLoopAsync: 监听任务已被成功取消。"); //停止令牌触发
            }
            catch (ObjectDisposedException ode)
            {
                Debug.WriteLine($"ListenLoopAsync: 因对象 '{ode.ObjectName}' 被释放而停止。"); // 当 StopInternalAsync 中 Dispose 了 _slaveNetwork 或 _listener 时可能发生
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException || ex.InnerException is SocketException)
            {

                Debug.WriteLine($"ListenLoopAsync: 遇到网络错误，监听任务终止: {ex.Message}"); //处理常见的网络相关错误

                if (_isRunning) // 如果 IsRunning 仍然是 true，需要调用 StopServiceAsync 来确保状态一致
                {
                    Debug.WriteLine("ListenLoopAsync: 检测到网络错误且服务标记为运行，尝试触发停止...");
                    _ = StopServiceAsync(); // 异步触发停止，不要 await，避免阻塞 ListenLoop 的 finally
                }
            }
            finally
            {
                Debug.WriteLine("ListenLoopAsync: 监听循环结束。");
                // 可以考虑在这里做一些最终清理，但主要清理应在 StopInternalAsync 中完成
            }
        }

        private void ValidateTcpOptions(ModbusTcpClosedLoopOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.IpAddress) || !IPAddress.TryParse(options.IpAddress, out _))
            {
                throw new ArgumentException($"配置的 IP 地址 '{options.IpAddress}' 无效。", nameof(options.IpAddress));
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Port), $"配置的端口号 {options.Port} 必须在 1 到 65535 之间。");
            }

            // UnitId 是 byte 类型，天然在 0-255 范围，但 Modbus 规范通常建议 1-247
            if (options.UnitId == 0)
            {
                Debug.WriteLine("ValidateOptions: 警告：配置的从站 ID 为 0，这通常用于广播地址，可能不适用于从站服务。");
            }
            else if (options.UnitId > 247)
            {
                Debug.WriteLine($"ValidateOptions: 警告：配置的从站 ID ({options.UnitId}) 超出了 Modbus 规范推荐的范围 (1-247)。");
            }
        }

        private void LogCurrentConfig(string prefix)
        {
            Console.WriteLine($"{prefix}: IP={_currentOptions.IpAddress}, 端口={_currentOptions.Port}, 从站ID={_currentOptions.UnitId}");
        }

        private void HandleOptionsChanged(ModbusTcpClosedLoopOptions newOptions)
        {
            LogCurrentConfig("检测到配置发生变化,未变更前的配置");

            try
            {
                ValidateTcpOptions(newOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误：新的配置无效，将继续使用旧配置。错误信息: {ex.Message}");
                return;
            }

            var oldOptions = _currentOptions;
            //_currentOptions = newOptions;
            LogCurrentConfig("新配置");

            if (_isRunning)
            {
                Console.WriteLine("服务正在运行，将使用新配置重启服务...");

                Task.Run(async () =>
                {
                    try
                    {
                        await StopServiceAsync();
                        Console.WriteLine("旧服务已停止，准备使用新配置启动...");

                        await StartServiceAsync();
                        Console.WriteLine("新服务已启动，配置更新完成。");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"使用新配置重启服务时出错: {ex.Message}");
                        _isRunning = false; // 确保状态正确
                    }
                }
                    );
            }
        }

        // --- 公共独立初始化方法 (Public Standalone Initialize Method) ---
        /// <summary>
        /// 异步初始化或重置 Modbus 数据存储区。此操作仅能在服务正在运行时执行。
        /// 主要供外部调用（如 UI 按钮）触发数据重置。
        /// 此方法是线程安全的，使用 _operationLock 保证与启动、停止、配置变更互斥。
        /// </summary>
        /// <returns>表示异步初始化操作的任务。</returns>
        /// <exception cref="InvalidOperationException">如果服务当前未运行。</exception>
        private async Task InitializeDataStoreStandaloneAsync()
        {
            await _operationLock.WaitAsync(); // 获取统一操作锁
            Debug.WriteLine("InitializeDataStoreStandaloneAsync: 已获取操作锁。");

            try
            {
                if (!_isRunning)
                {
                    Debug.WriteLine("InitializeDataStoreStandaloneAsync: 服务正在运行，无法执行独立初始化。");
                    throw new InvalidOperationException("服务当前正在运行，无法初始化数据存储区。");
                }
                Debug.WriteLine("InitializeDataStoreStandaloneAsync: 准备调用内部初始化逻辑 (强制重置)...");

                await InitializeDataStoreInternalAsync(true); // 调用内部初始化逻辑，传入 true 表示强制重置
                Debug.WriteLine("InitializeDataStoreStandaloneAsync: 内部初始化逻辑已完成。");
            }
            catch (Exception ex)
            {
                var errorMsg = "在执行内部数据存储初始化时发生错误。";
                Debug.WriteLine($"InitializeDataStoreStandaloneAsync: {errorMsg} - {ex.Message}");
                throw;
            }
            finally
            {
                _operationLock.Release();
                Debug.WriteLine("InitializeDataStoreStandaloneAsync: 已释放操作锁。");
            }
        }

        private async Task InitializeDataStoreInternalAsync(bool forceReset = false)
        {
            //InitializeDataStoreStandaloneAsync();
            var MaxCoils = _currentOptions.MaxCoils;
            var MaxInputDiscretes = _currentOptions.MaxInputDiscretes;
            var MaxHoldingRegisters = _currentOptions.MaxHoldingRegisters;
            var MaxInputRegisters = _currentOptions.MaxInputRegisters;

            try
            {
                if (_slaveDataStore == null)
                {
                    _slaveDataStore = new SlaveDataStore();
                    Debug.WriteLine("InitializeDataStoreInternalAsync: SlaveDataStore 实例已创建。");
                }
                else if (forceReset)
                {
                    _slaveDataStore = new SlaveDataStore();
                    Debug.WriteLine("InitializeDataStoreInternalAsync: SlaveDataStore 实例已存在，执行强制重置。");
                }
                try
                {
                    _coilLock.EnterWriteLock();
                    try { _slaveDataStore.CoilDiscretes.WritePoints(1, new bool[MaxCoils]); Debug.WriteLine("线圈初始化完成。"); } finally { _coilLock.ExitWriteLock(); }

                    _inputDiscreteLock.EnterWriteLock();
                    try { _slaveDataStore.CoilInputs.WritePoints(1, new bool[MaxInputDiscretes]); Debug.WriteLine("只读线圈初始化完成。"); } finally { _inputDiscreteLock.ExitWriteLock(); }

                    _holdingRegisterLock.EnterWriteLock();
                    try { _slaveDataStore.HoldingRegisters.WritePoints(1, new ushort[MaxHoldingRegisters]); Debug.WriteLine("保持寄存器初始化完成。"); } finally { _holdingRegisterLock.ExitWriteLock(); }

                    _inputRegisterLock.EnterWriteLock();
                    try { _slaveDataStore.InputRegisters.WritePoints(1, new ushort[MaxInputRegisters]); Debug.WriteLine("线圈初始化完成。"); } finally { _inputRegisterLock.ExitWriteLock(); }

                    Debug.WriteLine("InitializeDataStoreInternalAsync: Modbus 数据存储区所有区域初始化/清零完成。");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitializeDataStoreInternalAsync: 在写入数据区时发生错误: {ex.Message}");
                    throw; // 向上抛出异常
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InitializeDataStoreInternalAsync: 内部初始化 DataStore 时发生严重错误: {ex.Message}");
                throw; // 重新抛出，让调用者 (StartInternal 或 StandaloneInitialize) 知道失败
            }
        }

        public async Task StopServiceAsync()
        {
            await _operationLock.WaitAsync(); // 获取统一操作锁
            Debug.WriteLine("StopServiceAsync: 已获取操作锁。");

            try 
            {
                if (!_isRunning)
                {
                    Debug.WriteLine("StopServiceAsync: 服务尚未运行，无需停止。");
                    return;
                }

                Debug.WriteLine("StopServiceAsync: 准备调用内部停止逻辑...");
                await StopInternalAsync(); // 调用并等待内部停止逻辑完成
                Debug.WriteLine("StopServiceAsync: 内部停止逻辑已完成。");

            }
            // 省略 catch 块，让异常直接冒泡
            finally
            {
                _operationLock.Release(); // 无论如何，最终释放锁
                Debug.WriteLine("StopServiceAsync: 已释放操作锁。");
            }
        }

        private async Task StopInternalAsync()
        {
            Debug.WriteLine("StopInternalAsync: 开始执行核心停止步骤...");

            CancellationTokenSource? currentCts = _cts;
            Task? currentListenerTask = _listenerTask;

            try
            {
                if (currentCts != null) // 步骤 1: 请求取消后台任务
                {
                    Debug.WriteLine("StopInternalAsync: 请求取消后台监听任务...");
                    try { currentCts.Cancel(); } catch (ObjectDisposedException) { /* Cts 可能已被 Dispose，忽略 */ }
                }

                if (_slaveNetwork != null)// 步骤 2: 停止并释放网络层和监听器资源，Dispose 网络层会间接关闭监听器和连接
                {
                    Debug.WriteLine("StopInternalAsync: 释放 SlaveNetwork...");
                    try { _slaveNetwork.Dispose(); } catch (Exception ex) { Debug.WriteLine($"StopInternalAsync: 释放 _slaveNetwork 时出错: {ex.Message}"); }
                    _slaveNetwork = null;
                }

                if (_listener != null) // 再次尝试停止 Listener (如果 Dispose 未完全处理)
                {
                    Debug.WriteLine("StopInternalAsync: 停止 Listener...");
                    try { _listener.Stop(); } catch (Exception ex) { Debug.WriteLine($"StopInternalAsync: 停止 _listener 时出错: {ex.Message}"); }
                    _listener = null;
                }

                if (currentListenerTask != null)
                {
                    Debug.WriteLine("StopInternalAsync: 等待后台监听任务或停止完成...");
                    try { await WaitWithTimeoutAsync(_listenerTask, TimeSpan.FromSeconds(5)); } catch (Exception ex) { Debug.WriteLine($"StopInternalAsync: 等待 _listenerTask 时出错: {ex.Message}"); }
                }
            }
            catch (Exception ex) // 捕获停止过程中的异常
            {
                Debug.WriteLine($"StopInternalAsync: 停止服务过程中发生错误: {ex.Message}");
                // 即使出错，也要继续执行 finally 中的清理
            }
            finally
            {
                Debug.WriteLine("StopInternalAsync: 清理服务资源...");

                if (_slave != null)
                {
                    Debug.WriteLine("StopInternalAsync: 释放 Slave...");
                    try { (_slave as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"StopInternalAsync: 释放 _slave 时出错: {ex.Message}"); }
                    _slave = null;
                }

                if (currentCts != null)
                {
                    try { currentCts.Dispose(); } catch (Exception ex) { Debug.WriteLine($"StopInternalAsync: 释放 {nameof(currentCts)} 时出错: {ex.Message}"); }
                }

                // 清理内部状态引用
                _slaveDataStore = null; // 重要：将 DataStore 引用置空，防止后续访问已无效的对象
                _listenerTask = null;
                _cts = null;

                Debug.WriteLine("StopInternalAsync: 服务已停止，资源清理完毕。");
                _isRunning = false;

            }
        }

        public bool IsServiceRunning()
        {
            throw new NotImplementedException();
        }

        public Task<ushort> ReadHoldingRegisterAsync(ushort address)
        {
            throw new NotImplementedException();
        }

        public Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count)
        {
            throw new NotImplementedException();
        }

        public Task WriteHoldingRegisterAsync(ushort startAddress, ushort value)
        {
            throw new NotImplementedException();
        }

        public Task WriteHoldingRegistersAsync(ushort startAddress, ushort count, ushort value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ReadCoilAsync(ushort address)
        {
            throw new NotImplementedException();
        }

        public Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count)
        {
            throw new NotImplementedException();
        }

        public Task WriteCoilAsync(ushort address, bool value)
        {
            throw new NotImplementedException();
        }

        public Task WriteCoilsAsync(ushort address, bool[] values)
        {
            throw new NotImplementedException();
        }

        public Task FillCoilsAsync(ushort address, ushort count, bool value)
        {
            throw new NotImplementedException();
        }

        public int ConnectionCount()
        {
            throw new NotImplementedException();
        }

        public Task DisconnectClientAsync(string clientId)
        {
            throw new NotImplementedException();
        }

        public void ClearAllData()
        {
            throw new NotImplementedException();
        }

        public Task<DiagnosticInfo> GetDiagnosticInfoAsync()
        {
            throw new NotImplementedException();
        }

        public string GetServerStatus()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 释放由 ModbusTcpService 使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true); // 调用实际的释放逻辑，并指示正在进行托管资源释放
            GC.SuppressFinalize(this); // 通知垃圾收集器不需要再调用终结器
        }

        /// <summary>
        /// 执行实际的资源释放操作。
        /// </summary>
        /// <param name="disposing">如果为 true，则释放托管资源和非托管资源；如果为 false，则仅释放非托管资源。</param>
        public void Dispose(bool disposing)
        {
            if (_disposed) return; // 防止重复释放

            if (disposing)
            {
                Debug.WriteLine("Dispose: 开始释放托管资源...");

                _optionsChangeListener?.Dispose();
                Debug.WriteLine("Dispose: OptionsChangeListener 已取消注册。");

                // 尝试立即获取操作锁，如果失败则说明有操作正在进行，此时应直接取消并清理
                bool lockAcquired = false;

                try
                {
                    lockAcquired = _operationLock.Wait(0);
                    if (lockAcquired)
                    {
                        Debug.WriteLine("Dispose: 已获取操作锁，尝试调用内部停止...");
                        if (_isRunning)
                        {
                            try
                            {
                                _cts?.Cancel(); //仅触发取消，让 ListenLoop 和相关资源自行清理
                                _isRunning = false;
                                Debug.WriteLine("Dispose: 已请求取消并标记为停止。");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Dispose: 请求取消时出错: {ex.Message}");
                            }
                        }
                        else { Debug.WriteLine("Dispose: 服务已停止，无需再次停止。"); }
                    }
                    else
                    {
                        Debug.WriteLine("Dispose: 操作锁正忙，将直接尝试取消后台任务...");
                        try { _cts?.Cancel(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 直接取消 Cts 时出错: {ex.Message}"); }
                        // 标记为停止
                        _isRunning = false;
                    }
                }
                catch (ObjectDisposedException) { /* 锁可能已被释放 */ Debug.WriteLine("Dispose: 操作锁已被释放。"); }
                catch (Exception ex) { Debug.WriteLine($"Dispose: 尝试获取操作锁或取消时出错: {ex.Message}"); }
                finally
                {
                    if (lockAcquired) _operationLock.Release(); // 如果获取了锁，则释放
                }

                // 3. 显式释放网络资源 (再次确保)
                Debug.WriteLine("Dispose: 尝试释放网络资源...");
                try { _slaveNetwork?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _slaveNetwork 异常: {ex.Message}"); }
                try { _listener?.Stop(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 停止 _listener 异常: {ex.Message}"); }

                // 4. 释放其他托管对象
                Debug.WriteLine("Dispose: 释放其他托管对象...");
                try { _cts?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _cts 异常: {ex.Message}"); }
                try { (_slave as IDisposable)?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _slave 异常: {ex.Message}"); }
                // Task 不需要 Dispose

                // 5. 释放同步原语
                Debug.WriteLine("Dispose: 释放同步原语...");
                try { _operationLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _operationLock 异常: {ex.Message}"); }
                try { _coilLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _coilLock 异常: {ex.Message}"); }
                try { _inputDiscreteLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _inputDiscreteLock 异常: {ex.Message}"); }
                try { _holdingRegisterLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _holdingRegisterLock 异常: {ex.Message}"); }
                try { _inputRegisterLock?.Dispose(); } catch (Exception ex) { Debug.WriteLine($"Dispose: 释放 _inputRegisterLock 异常: {ex.Message}"); }
                _listenerTask = null;
                Debug.WriteLine("Dispose: 托管资源释放完毕。");
            }
            _disposed = true; // 标记为已释放
            Debug.WriteLine("Dispose: 完成。");
        }

        // 终结器 (备用，确保非托管资源被释放)
        ~ModbusTcpSlaveClosedLoopService()
        {
            Dispose(false); // 仅释放非托管资源
        }
    }
}
