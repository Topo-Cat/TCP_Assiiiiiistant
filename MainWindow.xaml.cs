using System.Windows;
using ModbusCommunicator.Services.Interfaces;
using ModbusCommunicator.Views;

namespace ModbusCommunicator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IMainNavigationService _navigationService;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(IMainNavigationService navigationService) : base()
        {
            InitializeComponent();
            _navigationService = navigationService ?? throw new System.ArgumentNullException($"导航服务未被正确初始化！");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Application.Current?.Dispatcher?.BeginInvoke(() =>
            //{
            //    _navigationService.NavigateTo("ModBusTCPClosedLoopPage", false);
            //}
            //);

        }

        private void ClientMonitorButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                _navigationService.NavigateTo("ModBusTCPClosedLoopPage", false);
            }
);
        }
    }
}