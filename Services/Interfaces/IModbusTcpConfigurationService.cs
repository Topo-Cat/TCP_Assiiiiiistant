using ModbusCommunicator.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Services.Interfaces
{
    public interface IModbusTcpConfigurationService
    {
        // 获取闭环测试配置
        ModbusTcpClosedLoopOptions GetClosedLoopConfig();

        // 获取主站测试配置
        ModbusTcpMasterOptions GetMasterConfig();

        // 获取从站测试配置
        ModbusTcpSlaveOptions GetSlaveConfig();

        // 保存闭环测试配置
        void SaveClosedLoopConfig(ModbusTcpClosedLoopOptions config);

        // 保存主站测试配置
        void SaveMasterConfig(ModbusTcpMasterOptions config);

        // 保存从站测试配置
        void SaveSlaveConfig(ModbusTcpSlaveOptions config);
    }
}
