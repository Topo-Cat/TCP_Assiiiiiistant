using System.Windows;
using PlcCommunicator.Services.Implements;
using PlcCommunicator.Services.Interfaces;
using PlcCommunicator.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace PlcCommunicator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IMainNavigationService>(() =>
            {
                var regionManager = containerRegistry.GetContainer().Resolve<IRegionManager>();
                return new MainNavigationService(regionManager, () =>
                {
                    return regionManager.Regions.ContainsRegionWithName("ContentRegion") ?
                    regionManager.Regions["ContentRegion"].NavigationService : null;
                }
                );
            }
            );
        containerRegistry.RegisterForNavigation<ClientMonitor>();
        containerRegistry.RegisterForNavigation<DataManagement>();
        containerRegistry.RegisterForNavigation<ModBusTCPServicePage>();
    }
}

