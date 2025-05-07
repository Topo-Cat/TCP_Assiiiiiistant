using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Models
{
    public class DiagnosticInfo
    {
        public int TotalRequests { get; set; }  // 总请求数
        public double AvgResponseTimeMs { get; set; }  // 平均响应时间
        public Dictionary<byte, int> SlaveErrorCounts { get; }  // 从站错误统计
    }
}
