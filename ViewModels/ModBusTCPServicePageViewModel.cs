using NModbus;
using PlcCommunicator.Commands;
using PlcCommunicator.Events;
using PlcCommunicator.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PlcCommunicator.ViewModels
{
    /// <summary>
    /// ModBus TCP服务页面的视图模型，处理UI和ModBus服务器之间的交互
    /// </summary>
    public class ModBusTCPServicePageViewModel : ViewModelBase
    {
        #region 服务器设置
        private string _port = "502";// 服务器端口，默认 ModBus TCP 端口为 502
        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }


        private string _slaveId = "1";// 从站 ID，ModBus 设备标识符，范围通常为 1-247
        public string SlaveId
        {
            get => _slaveId;
            set => SetProperty(ref _slaveId, value);
        }
        #endregion

        #region 服务器状态
        private string _serverStatus = "未启动"; // 服务器状态描述文本
        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }


        private bool _isRunning; // 服务器运行状态标志 - 控制状态指示灯颜色
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }


        private int _connectionCount; // 客户端连接数
        public int ConnectionCount
        {
            get => _connectionCount;
            set => SetProperty(ref _connectionCount, value);
        }


        private string _statusMessage = "服务器就绪"; // 状态栏消息
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        #endregion

        #region 常用寄存器操作
        private string _registerAddress = "0"; // 要操作的保持寄存器地址
        public string RegisterAddress
        {
            get => _registerAddress;
            set => SetProperty(ref _registerAddress, value);
        }


        private string _registerValue = "0"; // 要写入保持寄存器的值
        public string RegisterValue
        {
            get => _registerValue;
            set => SetProperty(ref _registerValue, value);
        }


        private string _register0Value = "0"; // 常用寄存器0值显示
        public string Register0Value
        {
            get => _register0Value;
            set => SetProperty(ref _register0Value, value);
        }

        private string _register1Value = "0"; // 常用寄存器1值显示
        public string Register1Value
        {
            get => _register1Value;
            set => SetProperty(ref _register1Value, value);
        }

        private string _register2Value = "0"; // 常用寄存器2值显示
        public string Register2Value
        {
            get => _register2Value;
            set => SetProperty(ref _register2Value, value);
        }

        #endregion

        #region 常用线圈操作
        private string _coilAddress = "0"; // 要操作的线圈地址
        public string CoilAddress
        {
            get => _coilAddress;
            set => SetProperty(ref _coilAddress, value);
        }

        private bool _coilValue; // 要写入线圈的值（开/关）
        public bool CoilValue
        {
            get => _coilValue;
            set => SetProperty(ref _coilValue, value);
        }

        private string _coil0Value = "OFF"; // 常用线圈0状态显示
        public string Coil0Value
        {
            get => _coil0Value;
            set => SetProperty(ref _coil0Value, value);
        }

        private string _coil1Value = "OFF"; // 常用线圈1状态显示
        public string Coil1Value
        {
            get => _coil1Value;
            set => SetProperty(ref _coil1Value, value);
        }

        private string _coil2Value = "OFF"; // 常用线圈2状态显示
        public string Coil2Value
        {
            get => _coil2Value;
            set => SetProperty(ref _coil2Value, value);
        }
        #endregion

        #region 日志相关
        private ObservableCollection<string> _logMessages = new ObservableCollection<string>();// 日志消息集合，用于UI显示
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            private set => SetProperty(ref _logMessages, value);
        }


        private bool _autoScroll = true; // 是否自动滚动到最新日志
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }
        #endregion

        #region 命令
        public IAsyncCommand ToggleServerCommand { get; } // 启动/停止服务器命令

        public IAsyncCommand UpdateRegisterCommand { get; } // 更新保持寄存器命令

        public IAsyncCommand UpdateCoilCommand { get; } // 更新线圈命令

        public ICommand ShowRegistersCommand { get; } // 显示寄存器窗口命令

        public ICommand ShowCoilsCommand { get; } // 显示线圈窗口命令

        public ICommand ClearLogCommand { get; } // 清空日志命令

        public IAsyncCommand ExportLogCommand { get; } // 导出日志命令
        #endregion



        public void StartService()
        {
            if (IsRunning)
                return;

            try
            {

            }
            catch (Exception ex)
            {

            }
        }
    }
}
