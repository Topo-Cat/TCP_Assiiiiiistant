using ModbusCommunicator.Services.Interfaces;
using ModbusCommunicator.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ModbusCommunicator.Services.Implements
{
    public class MainNavigationService : IMainNavigationService
    {
        private readonly IRegionManager _regionManager;
        private Lazy<IRegionNavigationService> _navigationService;
        private readonly Dictionary<string, Type> _pages;
        private readonly static string _mainRegion = "ContentRegion";
        private string _currentPage;

        public MainNavigationService(IRegionManager regionManager, Func<IRegionNavigationService> func)
        {
            _pages = new Dictionary<string, Type>()
            {   { "ClientMonitor", typeof(ClientMonitor) },
                { "DataManagement", typeof(DataManagement) },
                { "ModBusTCPClosedLoopPage", typeof(ModBusTCPClosedLoopPage) }
            };
            _regionManager = regionManager;
            _navigationService = new Lazy<IRegionNavigationService>(
                func ?? throw new InvalidOperationException("未正确注册导航服务工厂！"), LazyThreadSafetyMode.ExecutionAndPublication
                );
        }

        public bool CanGoBack()
        {
            var journal = _navigationService.Value.Journal;
            return journal?.CanGoBack == true;
        }

        public bool CanGoForward()
        {
            var journal = _navigationService.Value.Journal;
            return journal?.CanGoForward == true;
        }

        public void ClearHistory()
        {
            var journal = _navigationService.Value.Journal;
            if (journal == null) throw new InvalidOperationException("导航服务未正确初始化！");

            var currentEntry = journal.CurrentEntry ?? throw new ArgumentNullException("当前页面获取失败！");

            journal.Clear();
            journal.RecordNavigation(currentEntry, false);
        }

        public void GoBack()
        {
            var journal = _navigationService.Value.Journal;
            if (journal == null) throw new InvalidOperationException("导航服务未正确初始化！");
            journal.GoBack();
        }

        public void GoForward()
        {
            var journal = _navigationService.Value.Journal;
            if (journal == null) throw new InvalidOperationException("导航服务未正确初始化！");
            journal.GoForward();
        }

        public void NavigateTo(string pageName, bool clearHistory = true)
        {
            if (_regionManager == null) throw new InvalidOperationException("区域管理器未正确初始化！");
            if (_pages.TryGetValue(pageName, out Type pageType) && pageType != null)
            {
                if (_currentPage != pageName)
                {
                    _regionManager.RequestNavigate(_mainRegion, pageName);
                    _currentPage = pageName;
                    if (clearHistory)
                    {
                        ClearHistory();
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"页面 {pageName} 未找到！");
            }
        }
    }
}
