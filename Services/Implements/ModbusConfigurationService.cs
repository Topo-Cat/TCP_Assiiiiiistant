using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ModbusCommunicator.Events.PrismEventAggregator;
using ModbusCommunicator.Services.Configuration;
using ModbusCommunicator.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Services.Implements
{
    public class ModbusConfigurationService : IModbusTcpConfigurationService
    {
        //private readonly IConfiguration _configuration;

        private readonly string _configDirectory;
        private readonly string _closedLoopConfigPath;
        private readonly string _masterConfigPath;
        private readonly string _slaveConfigPath;

        private readonly IEventAggregator _eventAggregator;

        public ModbusConfigurationService(IEventAggregator eventAggregator)
        {
            //_configuration = configuration ?? throw new ArgumentNullException("IConfiguration注入对象为空！");
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException("IEventAggregator注入对象为空！");
            _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config"); // 当前程序根目录

            // 确保目录存在
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            _closedLoopConfigPath = Path.Combine(_configDirectory, "modbus-closedloop-config.json");
            _masterConfigPath = Path.Combine(_configDirectory, "modbus-master-config.json");
            _slaveConfigPath = Path.Combine(_configDirectory, "modbus-slave-config.json");
        }

        public ModbusTcpClosedLoopOptions GetClosedLoopConfig()
        {
            return GetConfig<ModbusTcpClosedLoopOptions>(_closedLoopConfigPath);
        }

        public ModbusTcpMasterOptions GetMasterConfig()
        {
            return GetConfig<ModbusTcpMasterOptions>(_masterConfigPath);
        }

        public ModbusTcpSlaveOptions GetSlaveConfig()
        {
            return GetConfig<ModbusTcpSlaveOptions>(_slaveConfigPath);
        }

        public void SaveClosedLoopConfig(ModbusTcpClosedLoopOptions config)
        {
            if (SaveConfig(_closedLoopConfigPath, config))
            {
                _eventAggregator.GetEvent<ClosedLoopConfigUpdatedEvent>().Publish(config);
                Debug.WriteLine("闭环配置已保存。");
            }
        }

        public void SaveMasterConfig(ModbusTcpMasterOptions config)
        {
            if (SaveConfig(_masterConfigPath, config))
            {
                _eventAggregator.GetEvent<MasterConfigUpdatedEvent>().Publish(config);
                Debug.WriteLine("主站配置已保存。");
            }
        }

        public void SaveSlaveConfig(ModbusTcpSlaveOptions config)
        {
            if (SaveConfig(_slaveConfigPath, config))
            {
                _eventAggregator.GetEvent<SlaveConfigUpdatedEvent>().Publish(config);
                Debug.WriteLine("从站配置已保存。");
            }
        }

        private T GetConfig<T>(string configPath) where T : class, new()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<T>(json);

                    if (config != null)
                    {
                        return config;
                    }
                    // 如果反序列化返回 null，也视为一种错误或缺失配置，返回默认值
                    Debug.WriteLine($"反序列化配置返回 null: {configPath}，将使用默认配置");
                }
                else
                {
                    // 文件不存在，记录日志并创建默认配置
                    var defaultOptions = new T ();
                    SaveConfig(configPath, defaultOptions);
                    Debug.WriteLine($"配置文件不存在: {configPath}，将使用默认配置");
                    // 也可以使用 Debug.WriteLine 或其他日志机制
                }
            }
            catch (JsonException jsonEx) // 更具体地捕获 JSON 错误
            {
                Debug.WriteLine($"反序列化配置失败: {configPath}, Error: {jsonEx.Message}，将使用默认配置");
            }
            catch (IOException ioEx) // 捕获文件读写错误
            {
                Debug.WriteLine($"读取配置文件 IO 错误: {configPath}, Error: {ioEx.Message}，将使用默认配置");
            }
            catch (Exception ex) // 捕获其他意外错误
            {
                // 读取失败，记录错误并创建默认配置
                Debug.WriteLine($"读取配置时发生未知错误: {configPath}, {ex.Message}，将使用默认配置");
            }

            // 返回带默认值的新实例
            return new T();
        }


        private bool SaveConfig<T>(string configPath, T config) where T : class, new()
        {
            if (config == null)
            {
                Debug.WriteLine($"尝试保存 null 配置到 {configPath}，操作已取消。");
                return false;
            }

            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, json, Encoding.UTF8); // 明确指定编码
                Debug.WriteLine($"配置已保存: {configPath}");
                return true;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"序列化配置到 JSON 失败: {configPath}, Error: {jsonEx.Message}");
                return false;
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"写入配置文件 IO 错误: {configPath}, Error: {ioEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存配置时发生未知错误: {configPath}, Error: {ex.Message}");
                return false;
            }
        }
    }
}
