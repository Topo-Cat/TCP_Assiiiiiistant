using System.ComponentModel.DataAnnotations;

namespace ModbusCommunicator.Services.Configuration
{
    /// <summary>
    /// 包含 Modbus TCP 从站（服务）配置选项的类。
    /// </summary>
    public class ModbusTcpClosedLoopOptions
    {
        /// <summary>
        /// 要监听的本地 IP 地址。
        /// 使用 "0.0.0.0" 或 "::" 监听所有网络接口。
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "IP 地址是必需的，且不能为空字符串！")] // 标记为必需字段
        public string IpAddress { get; set; } = "192.168.0.1"; // 默认监听所有 IPv4 地址

        /// <summary>
        /// 要监听的 TCP 端口号。
        /// </summary>
        [Required(ErrorMessage = "端口号是必需的！")] // 标记为必需字段
        [Range(1, 65535, ErrorMessage = "端口号必须介于 {1} 至 {2} 之间！") ] // 限制端口范围
        public int Port { get; set; } = 502; // 标准 Modbus TCP 端口

        /// <summary>
        /// Modbus 从站的单元 ID (Slave Address)。
        /// </summary>
        [Required(ErrorMessage = "从站 ID 是必需的！")] // 标记为必需字段
        [Range(1, 247, ErrorMessage = "Modbus 从站 ID 必须介于 {1} 至 {2} 之间。")] // Modbus 标准范围是 1-247，0 为广播，248-255 保留
        public byte UnitId { get; set; } = 1;

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大线圈数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        public ushort MaxCoils = 100;             // 最大线圈数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大离散输入数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        public ushort MaxInputDiscretes = 100;    // 最大离散输入数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大保持寄存器数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        public ushort MaxHoldingRegisters = 100;  // 最大保持寄存器数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大输入寄存器数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        public ushort MaxInputRegisters = 100;    // 最大输入寄存器数

        /// <summary>
        /// 获取或设置一个值，指示从站是否应以只读模式运行。
        /// 如果为 true，则所有写入操作（功能码 0x05, 0x06, 0x0F, 0x10）将被拒绝。
        /// </summary>
        public bool ReadOnly { get; set; } = false;

         //--- 以下选项根据具体需求添加 ---
         /// <summary>
         /// 获取或设置一个值，指示请求失败的最大重试次数。
         /// </summary>
        [Range(1, 10)] // 设置一个合理的范围
        public int NumberOfRetries { get; set; } = 3;

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示重试时的延时间隔。
        /// </summary>
        [Range(5, 2000)] // 设置一个合理的范围
        public int RetryDelayMilliseconds { get; set; } = 20;

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示发送请求的超时时间。
        /// </summary>
        [Range(50, 2000)] // 设置一个合理的范围
        public int SendTimeoutMilliseconds { get; set; } = 500;

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示接收超时时间。
        /// </summary>
        [Range(50, 2000)] // 设置一个合理的范围
        public int ReceiveTimeoutMilliseconds { get; set; } = 500;
    }


   
}
