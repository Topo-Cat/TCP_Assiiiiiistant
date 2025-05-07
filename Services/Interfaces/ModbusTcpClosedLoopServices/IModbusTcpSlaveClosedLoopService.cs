using ModbusCommunicator.Events;
using ModbusCommunicator.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Services.Interfaces.ModbusTcpClosedLoopServices
{
    public interface IModbusTcpSlaveClosedLoopService
    {
        Task StartServiceAsync(); //启动TCP服务
        Task StopServiceAsync(); //停止TCP服务
        bool IsServiceRunning(); //服务是否正在运行

        Task <ushort> ReadHoldingRegisterAsync(ushort address); //读取保持寄存器
        Task <ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count); //读取多个保持寄存器
        Task WriteHoldingRegisterAsync(ushort startAddress, ushort value); //写入保持寄存器
        Task WriteHoldingRegistersAsync(ushort startAddress, ushort count, ushort value); //写入多个保持寄存器

        Task <bool> ReadCoilAsync(ushort address); //读取线圈
        Task <bool[]> ReadCoilsAsync(ushort startAddress, ushort count); //读取多个线圈
        Task WriteCoilAsync(ushort address, bool value); //写入线圈
        Task WriteCoilsAsync(ushort address, bool[] values); //写入多个线圈
        Task FillCoilsAsync(ushort address, ushort count, bool value); //写入多个线圈

        int ConnectionCount(); //当前连接客户端的数量
        Task DisconnectClientAsync(string clientId);

        void ClearAllData(); //复位所有寄存器和线圈
        Task<DiagnosticInfo> GetDiagnosticInfoAsync(); //获取诊断信息
        string GetServerStatus(); //获取服务器状态
    }
}
