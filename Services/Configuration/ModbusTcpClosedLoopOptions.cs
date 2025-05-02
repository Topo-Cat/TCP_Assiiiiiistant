using System.ComponentModel.DataAnnotations;

namespace PlcCommunicator.Services.Configuration
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
        [Required(AllowEmptyStrings = false)] // 标记为必需字段
        public string IpAddress { get; set; } = "192.168.0.1"; // 默认监听所有 IPv4 地址

        /// <summary>
        /// 要监听的 TCP 端口号。
        /// </summary>
        [Range(1, 65535)] // 限制端口范围
        public int Port { get; set; } = 502; // 标准 Modbus TCP 端口

        /// <summary>
        /// Modbus 从站的单元 ID (Slave Address)。
        /// </summary>
        [Range(1, 247)] // Modbus 标准范围是 1-247，0 为广播，248-255 保留
        public byte UnitId { get; set; } = 1;

        /// <summary>
        /// 从站支持的线圈（Coils, 0xxxx）的总数量。
        /// 地址范围将是 0 到 NumberOfCoils - 1。
        /// </summary>
        [Range(1, 65536)] // Modbus 地址空间限制
        public int NumberOfCoils { get; set; } = 10000;

        /// <summary>
        /// 从站支持的离散输入（Discrete Inputs, 1xxxx）的总数量。
        /// 地址范围将是 0 到 NumberOfDiscreteInputs - 1。
        /// </summary>
        [Range(1, 65536)] // Modbus 地址空间限制
        public int NumberOfDiscreteInputs { get; set; } = 10000;

        /// <summary>
        /// 从站支持的保持寄存器（Holding Registers, 4xxxx）的总数量。
        /// 地址范围将是 0 到 NumberOfHoldingRegisters - 1。
        /// 单次 Modbus 读/写请求通常最多能处理约 125 个寄存器。
        /// </summary>
        [Range(1, 65536)] // 修正：应反映 Modbus 16 位地址空间的总大小
        public int NumberOfHoldingRegisters { get; set; } = 1000; // 默认值 1000 在此范围内

        /// <summary>
        /// 从站支持的输入寄存器（Input Registers, 3xxxx）的总数量。
        /// 地址范围将是 0 到 NumberOfInputRegisters - 1。
        /// 单次 Modbus 读请求通常最多能读取约 125 个寄存器。
        /// </summary>
        [Range(1, 65536)] // 修正：应反映 Modbus 16 位地址空间的总大小
        public int NumberOfInputRegisters { get; set; } = 1000; // 默认值 1000 在此范围内

        /// <summary>
        /// 允许同时连接到此从站的最大客户端数量。
        /// </summary>
        [Range(1, 100)] // 设置一个合理的范围
        public int MaxConcurrentConnections { get; set; } = 10;

        /// <summary>
        /// 获取或设置一个值，指示从站是否应以只读模式运行。
        /// 如果为 true，则所有写入操作（功能码 0x05, 0x06, 0x0F, 0x10）将被拒绝。
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// 获取或设置一个值，指示是否记录详细的 Modbus PDU 请求/响应数据。
        /// 启用此选项会产生大量日志，主要用于调试目的。
        /// </summary>
        public bool LogPduData { get; set; } = false;

        // --- 以下选项根据具体需求添加 ---
        // /// <summary>
        // /// 获取或设置一个值，指示即使请求的 Unit ID 与从站的 UnitId 不匹配，是否也应处理该请求。
        // /// 谨慎使用：这违反了标准 Modbus 行为。
        // /// </summary>
        // public bool AllowInvalidUnitIdRequests { get; set; } = false;

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

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大线圈数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        private ushort MaxCoils = 100;             // 最大线圈数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大离散输入数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        private ushort MaxInputDiscretes = 100;    // 最大离散输入数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大保持寄存器数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        private ushort MaxHoldingRegisters = 100;  // 最大保持寄存器数

        //--- 以下选项根据具体需求添加 ---
        /// <summary>
        /// 获取或设置一个值，指示最大输入寄存器数。
        /// </summary>
        [Range(1, 1000)] // 设置一个合理的范围
        private ushort MaxInputRegisters = 100;    // 最大输入寄存器数
    }


   
}
