// 引入 System 命名空间，提供基础类和基类型。
using System;
// 引入 System.Threading 命名空间，提供支持多线程编程的类。
using System.Threading;
// 引入 System.Threading.Tasks 命名空间，提供用于异步编程的类型。
using System.Threading.Tasks;

// 定义项目的命名空间。
namespace ModbusCommunicator.Services.Interfaces.ModbusTcpClosedLoopServices
{
    /// <summary>
    /// 定义与 Modbus TCP 从站进行通信的主站服务接口。
    /// 提供连接、断开、读写 Modbus 数据以及获取连接状态的功能。
    /// 实现此接口的服务应负责管理 TCP 连接和 Modbus 通信协议。
    /// </summary>
    // 定义公共接口 IModbusTcpMasterService，并继承 IDisposable 和 IAsyncDisposable 以支持资源释放。
    public interface IModbusTcpMasterClosedLoopService : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 获取一个值，该值指示 Modbus 主站当前是否已连接到从站。
        /// </summary>
        /// <value>如果已连接，则为 true；否则为 false。</value>
        // 定义一个只读属性 IsConnected，用于获取连接状态。
        bool IsConnected { get; }

        /// <summary>
        /// 异步尝试连接到指定的 Modbus TCP 从站。
        /// 实现应处理连接逻辑，包括必要的超时和重试机制（如果配置）。
        /// </summary>
        /// <param name="ipAddress">目标从站的 IP 地址。</param>
        /// <param name="port">目标从站的端口号 (通常为 502)。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>
        /// 一个表示异步操作的任务。任务结果为 <c>true</c> 如果连接成功建立；否则为 <c>false</c>。
        /// </returns>
        /// <exception cref="ArgumentNullException">如果 <paramref name="ipAddress"/> 为 null 或空白。</exception>
        /// <exception cref="ArgumentOutOfRangeException">如果 <paramref name="port"/> 无效 (例如，小于等于 0 或大于 65535)。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        /// <remarks>
        /// 如果服务当前已连接，再次调用此方法通常应先断开现有连接，然后再尝试连接到新的目标。
        /// </remarks>
        // 定义异步连接方法 ConnectAsync。
        Task ConnectAsync();

        /// <summary>
        /// 异步断开与当前连接的 Modbus TCP 从站的连接。
        /// 如果当前未连接，则此操作不执行任何操作。
        /// </summary>
        /// <returns>一个表示异步断开操作的任务。</returns>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步断开连接方法 DisconnectAsync。
        Task DisconnectAsync();

        /// <summary>
        /// 异步从 Modbus 从站读取一个或多个线圈（Coils, 0x 区）的状态。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要读取的第一个线圈的地址 (从 0 开始)。</param>
        /// <param name="numberOfPoints">要读取的线圈数量。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>
        /// 一个表示异步操作的任务。任务结果为一个布尔数组，表示读取到的线圈状态。
        /// </returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误（例如，超时、网络中断）且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步读取线圈方法 ReadCoilsAsync。
        Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步从 Modbus 从站读取一个或多个离散输入（Discrete Inputs, 1x 区）的状态。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要读取的第一个离散输入的地址 (从 0 开始)。</param>
        /// <param name="numberOfPoints">要读取的离散输入数量。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>
        /// 一个表示异步操作的任务。任务结果为一个布尔数组，表示读取到的离散输入状态。
        /// </returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步读取离散输入方法 ReadDiscreteInputsAsync。
        Task<bool[]> ReadDiscreteInputsAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步从 Modbus 从站读取一个或多个保持寄存器（Holding Registers, 4x 区）的值。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要读取的第一个保持寄存器的地址 (从 0 开始)。</param>
        /// <param name="numberOfPoints">要读取的保持寄存器数量。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>
        /// 一个表示异步操作的任务。任务结果为一个 ushort 数组，表示读取到的寄存器值。
        /// </returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步读取保持寄存器方法 ReadHoldingRegistersAsync。
        Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步从 Modbus 从站读取一个或多个输入寄存器（Input Registers, 3x 区）的值。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要读取的第一个输入寄存器的地址 (从 0 开始)。</param>
        /// <param name="numberOfPoints">要读取的输入寄存器数量。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>
        /// 一个表示异步操作的任务。任务结果为一个 ushort 数组，表示读取到的寄存器值。
        /// </returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步读取输入寄存器方法 ReadInputRegistersAsync。
        Task<ushort[]> ReadInputRegistersAsync(byte slaveAddress, ushort startAddress, ushort numberOfPoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步向 Modbus 从站写入单个线圈（Coil, 0x 区）的状态。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="coilAddress">要写入的线圈地址 (从 0 开始)。</param>
        /// <param name="value">要写入的值 (true 表示 ON, false 表示 OFF)。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>一个表示异步写入操作的任务。</returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步写单个线圈方法 WriteSingleCoilAsync。
        Task WriteSingleCoilAsync(byte slaveAddress, ushort coilAddress, bool value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步向 Modbus 从站写入单个保持寄存器（Holding Register, 4x 区）的值。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="registerAddress">要写入的保持寄存器地址 (从 0 开始)。</param>
        /// <param name="value">要写入的 16 位值。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>一个表示异步写入操作的任务。</returns>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步写单个保持寄存器方法 WriteSingleRegisterAsync。
        Task WriteSingleRegisterAsync(byte slaveAddress, ushort registerAddress, ushort value, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步向 Modbus 从站写入多个连续线圈（Coils, 0x 区）的状态。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要写入的第一个线圈的地址 (从 0 开始)。</param>
        /// <param name="data">包含要写入的线圈状态的布尔数组。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>一个表示异步写入操作的任务。</returns>
        /// <exception cref="ArgumentNullException">如果 <paramref name="data"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步写多个线圈方法 WriteMultipleCoilsAsync。
        Task WriteMultipleCoilsAsync(byte slaveAddress, ushort startAddress, bool[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步向 Modbus 从站写入多个连续保持寄存器（Holding Registers, 4x 区）的值。
        /// </summary>
        /// <param name="slaveAddress">目标从站的单元地址/ID。</param>
        /// <param name="startAddress">要写入的第一个保持寄存器的地址 (从 0 开始)。</param>
        /// <param name="data">包含要写入的 16 位寄存器值的 ushort 数组。</param>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>一个表示异步写入操作的任务。</returns>
        /// <exception cref="ArgumentNullException">如果 <paramref name="data"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">如果客户端未连接。</exception>
        /// <exception cref="System.IO.IOException">如果发生通信错误且重试失败。</exception>
        /// <exception cref="NModbus.SlaveException">如果从站返回一个 Modbus 异常响应。</exception>
        /// <exception cref="OperationCanceledException">如果操作被 <paramref name="cancellationToken"/> 取消。</exception>
        /// <exception cref="ObjectDisposedException">如果服务实例已被释放。</exception>
        // 定义异步写多个保持寄存器方法 WriteMultipleRegistersAsync。
        Task WriteMultipleRegistersAsync(byte slaveAddress, ushort startAddress, ushort[] data, CancellationToken cancellationToken = default);
    } // 结束接口定义
} // 结束命名空间定义
