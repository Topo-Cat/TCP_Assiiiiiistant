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
    /// ModBus TCP����ҳ�����ͼģ�ͣ�����UI��ModBus������֮��Ľ���
    /// </summary>
    public class ModBusTCPServicePageViewModel : ViewModelBase
    {
        #region ����������
        private string _port = "502";// �������˿ڣ�Ĭ�� ModBus TCP �˿�Ϊ 502
        public string Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }


        private string _slaveId = "1";// ��վ ID��ModBus �豸��ʶ������Χͨ��Ϊ 1-247
        public string SlaveId
        {
            get => _slaveId;
            set => SetProperty(ref _slaveId, value);
        }
        #endregion

        #region ������״̬
        private string _serverStatus = "δ����"; // ������״̬�����ı�
        public string ServerStatus
        {
            get => _serverStatus;
            set => SetProperty(ref _serverStatus, value);
        }


        private bool _isRunning; // ����������״̬��־ - ����״ָ̬ʾ����ɫ
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }


        private int _connectionCount; // �ͻ���������
        public int ConnectionCount
        {
            get => _connectionCount;
            set => SetProperty(ref _connectionCount, value);
        }


        private string _statusMessage = "����������"; // ״̬����Ϣ
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        #endregion

        #region ���üĴ�������
        private string _registerAddress = "0"; // Ҫ�����ı��ּĴ�����ַ
        public string RegisterAddress
        {
            get => _registerAddress;
            set => SetProperty(ref _registerAddress, value);
        }


        private string _registerValue = "0"; // Ҫд�뱣�ּĴ�����ֵ
        public string RegisterValue
        {
            get => _registerValue;
            set => SetProperty(ref _registerValue, value);
        }


        private string _register0Value = "0"; // ���üĴ���0ֵ��ʾ
        public string Register0Value
        {
            get => _register0Value;
            set => SetProperty(ref _register0Value, value);
        }

        private string _register1Value = "0"; // ���üĴ���1ֵ��ʾ
        public string Register1Value
        {
            get => _register1Value;
            set => SetProperty(ref _register1Value, value);
        }

        private string _register2Value = "0"; // ���üĴ���2ֵ��ʾ
        public string Register2Value
        {
            get => _register2Value;
            set => SetProperty(ref _register2Value, value);
        }

        #endregion

        #region ������Ȧ����
        private string _coilAddress = "0"; // Ҫ��������Ȧ��ַ
        public string CoilAddress
        {
            get => _coilAddress;
            set => SetProperty(ref _coilAddress, value);
        }

        private bool _coilValue; // Ҫд����Ȧ��ֵ����/�أ�
        public bool CoilValue
        {
            get => _coilValue;
            set => SetProperty(ref _coilValue, value);
        }

        private string _coil0Value = "OFF"; // ������Ȧ0״̬��ʾ
        public string Coil0Value
        {
            get => _coil0Value;
            set => SetProperty(ref _coil0Value, value);
        }

        private string _coil1Value = "OFF"; // ������Ȧ1״̬��ʾ
        public string Coil1Value
        {
            get => _coil1Value;
            set => SetProperty(ref _coil1Value, value);
        }

        private string _coil2Value = "OFF"; // ������Ȧ2״̬��ʾ
        public string Coil2Value
        {
            get => _coil2Value;
            set => SetProperty(ref _coil2Value, value);
        }
        #endregion

        #region ��־���
        private ObservableCollection<string> _logMessages = new ObservableCollection<string>();// ��־��Ϣ���ϣ�����UI��ʾ
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            private set => SetProperty(ref _logMessages, value);
        }


        private bool _autoScroll = true; // �Ƿ��Զ�������������־
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }
        #endregion

        #region ����
        public IAsyncCommand ToggleServerCommand { get; } // ����/ֹͣ����������

        public IAsyncCommand UpdateRegisterCommand { get; } // ���±��ּĴ�������

        public IAsyncCommand UpdateCoilCommand { get; } // ������Ȧ����

        public ICommand ShowRegistersCommand { get; } // ��ʾ�Ĵ�����������

        public ICommand ShowCoilsCommand { get; } // ��ʾ��Ȧ��������

        public ICommand ClearLogCommand { get; } // �����־����

        public IAsyncCommand ExportLogCommand { get; } // ������־����
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
