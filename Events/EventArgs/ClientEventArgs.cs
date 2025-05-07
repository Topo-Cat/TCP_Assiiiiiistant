using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Events
{
    public class ClientEventArgs
    {
        public string ClientId { get; }      // 客户端唯一标识（如IP+端口）
        public string IpAddress { get; }     // 客户端IP
        public DateTime EventTime { get; }   // 事件发生时间
        public bool IsConnected { get; }      // true=连接, false=断开

        public ClientEventArgs(string clientId, string ip, bool isConnected)
        {
            ClientId = clientId; // 客户端唯一标识（如IP+端口）
            IpAddress = ip; // 客户端IP
            IsConnected = isConnected; // true=连接, false=断开
            EventTime = DateTime.Now; // 事件发生时间
        }
    }
}
