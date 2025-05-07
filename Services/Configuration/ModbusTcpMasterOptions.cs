using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Services.Configuration
{
    /// <summary>
    /// 包含 Modbus TCP 主站（客户端）配置选项的类。
    /// </summary>
    public class ModbusTcpMasterOptions
    {
        /// <summary>
        /// 要连接的目标 Modbus 从站的 IP 地址。
        /// </summary>
        [Required(AllowEmptyStrings = false)] // 必需字段
        public string TargetIpAddress { get; set; } = "127.0.0.1"; // 默认连接本地

        /// <summary>
        /// 要连接的目标 Modbus 从站的 TCP 端口号。
        /// </summary>
        [Range(1, 65535)] // 限制端口范围
        public int TargetPort { get; set; } = 502; // 标准 Modbus TCP 端口

        /// <summary>
        /// 要通信的目标 Modbus 从站的单元 ID (Slave Address)。
        /// </summary>
        [Range(1, 247)] // Modbus 标准范围是 1-247
        public byte TargetUnitId { get; set; } = 1;

        /// <summary>
        /// 尝试建立 TCP 连接的超时时间（毫秒）。
        /// 在这段时间内未能建立连接将被视为失败。
        /// </summary>
        [Range(100, 60000)] // 合理范围，例如 100ms 到 1 分钟
        public int ConnectTimeoutMilliseconds { get; set; } = 2000; // 默认 2 秒

        /// <summary>
        /// 发送 Modbus 请求后等待响应的超时时间（毫秒）。
        /// 这适用于读写操作，在 TCP 连接建立之后。
        /// </summary>
        [Range(100, 60000)] // 合理范围
        public int TransportReadTimeoutMilliseconds { get; set; } = 1000; // 默认 1 秒

        /// <summary>
        /// 当 Modbus 请求失败（例如超时或通信错误）时，自动重试的次数。
        /// 0 表示不重试。
        /// </summary>
        [Range(0, 10)] // 合理的重试次数范围
        public int NumberOfRetries { get; set; } = 3; // 默认重试 3 次

        /// <summary>
        /// 两次重试之间的等待时间（毫秒）。
        /// </summary>
        [Range(50, 5000)] // 合理的延迟范围
        public int RetryDelayMilliseconds { get; set; } = 200; // 默认重试间隔 200ms

        /// <summary>
        /// 主站自动轮询从站的时间间隔（毫秒）。
        /// 如果设置为 0 或负数，则禁用自动轮询。
        /// 轮询任务通常需要单独实现。
        /// </summary>
        [Range(0, 3600000)] // 允许 0 (禁用) 到 1 小时
        public int PollingIntervalMilliseconds { get; set; } = 0; // 默认禁用自动轮询

        // --- 其他可能的选项 ---
        // /// <summary>
        // /// 获取或设置一个值，指示是否在 TCP 连接上启用 Keep-Alive。
        // /// 这有助于检测断开连接，但通常由操作系统处理。
        // /// </summary>
        // public bool EnableTcpKeepAlive { get; set; } = false;

        // /// <summary>
        // /// TCP Keep-Alive 的时间间隔（秒）。仅在 EnableTcpKeepAlive 为 true 时有效。
        // /// </summary>
        // [Range(1, 3600)]
        // public int KeepAliveIntervalSeconds { get; set; } = 60;
    }
}
