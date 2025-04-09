using NModbus;
using NModbus.Logging;
using PlcCommunicator.Events;
using PlcCommunicator.Models;
using PlcCommunicator.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PlcCommunicator.Services.Implements
{
    public class ModeBusTCPService : IModBusTCPService
    {
        private readonly SemaphoreSlim _registerLock = new(1, 1); // 细粒度锁，用于防止多客户端同时读写寄存器。
        private readonly SemaphoreSlim _coilLock = new(1, 1); // 细粒度锁，用于防止多客户端同时读写线圈。
        private readonly object _serviceLock = new object(); // 服务锁，用于防止页面快速点击，同时启动服务
        public ConcurrentDictionary<string, ClientInfo> ConnectedClients => new();

        private IModbusSlaveNetwork _slaveNetwork; // ModBus从站网络实例
        private IModbusSlave _slave; // 从站实例
        private IModbusFactory _modbusFactory; // 用于创建 Modbus 相关对象的工厂类。我们利用 NModbus 库创建相应的 Slave（从站）网络和实例。
        //private readonly Channel<ClientEventArgs> _clientEventChannel = Channel.CreateUnbounded<ClientEventArgs>(); // 客户端事件通道，用于异步通知客户端的事件
        private TcpListener _listener; // TCP监听器实例，用于接受客户端连接请求。
        private bool _isRunning; // 服务运行状态（状态先行）
        private readonly CancellationTokenSource _cancellationTokenSource = new(); // 取消令牌源




        public ModeBusTCPService()
        {
            _modbusFactory = new ModbusFactory(logger: new ConsoleModbusLogger(LoggingLevel.Warning));// 此处指定使用 ConsoleModbusLogger，并设定日志级别为 Warning，便于观察警告及以上级别的日志信息
        }

        public Task StartServiceAsync(int port, byte slaveId)
        {

            lock (_serviceLock)
            {
                if (_isRunning) throw new InvalidOperationException("服务已启动!请勿重复启动!");
                _isRunning = true; //状态先行
            }

            bool startupSuccessful = false; //用于标记服务是否成功启动

            try
            {
                if (!IsPortAvailable(port)) throw new InvalidOperationException("端口已被占用");
                _listener = new TcpListener(IPAddress.Any, port); // 1. 创建TCP监听器所有可用的网络接口（包括本地回环、以太网、Wi-Fi等）
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true); // 设置套接字选项，允许地址重用。这对于避免“已在使用中的端口”错误非常有用。

                _listener.Start();

                _slave = _modbusFactory.CreateSlave(slaveId); // 2. 创建从站实例
                _slaveNetwork = _modbusFactory.CreateSlaveNetwork(_listener); // 3. 创建从站网络实例
                _slaveNetwork.AddSlave(_slave); // 4. 将从站实例添加到网络中

                _slaveNetwork.ListenAsync(_cancellationTokenSource.Token).ContinueWith(t =>
                {
                    if (t.IsFaulted) Console.WriteLine($"Modbus监听异常: {t.Exception?.InnerException?.Message}");

                    _isRunning = startupSuccessful = false;
                }, TaskContinuationOptions.OnlyOnFaulted); // 5. 开始监听客户端连接请求，并在出现异常时记录日志。此处使用了 TaskContinuationOptions.OnlyOnFaulted 来确保只在任务失败时执行后续操作（即记录错误）。

                startupSuccessful = true; // 6. 设置服务运行状态为 true

                Console.WriteLine($"ModBus TCP服务已启动，端口：{port}，从站ID：{slaveId}"); // 7. 记录日志（后续替换为EventAggregator）
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _listener?.Stop(); // 清理已分配的资源
                _slaveNetwork?.Dispose(); // 清理已分配的资源
                Console.WriteLine($"服务启动失败：{ex.Message}"); // 记录错误日志
                throw;
            }
            finally
            {
                if (!startupSuccessful)
                {
                    lock (_serviceLock)
                    {
                        _isRunning = false;
                    }
                }
            }
        }

        private static bool IsPortAvailable(int port) // 检查端口是否可用
        {
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException($"端口{port}不在有效范围内！");
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Bind(new IPEndPoint(IPAddress.Any, port));
                    socket.Close();
                    return true;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return false;
                }
            }
        }

        public Task StopServiceAsync()
        {
            throw new NotImplementedException();
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
    }
}
