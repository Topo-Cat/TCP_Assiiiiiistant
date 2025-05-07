using System.Windows;
using System.IO;
using ModbusCommunicator.Services.Implements;
using ModbusCommunicator.Services.Interfaces;
using ModbusCommunicator.Views;
using Prism.Ioc;
using Prism.Modularity;
using Microsoft.Extensions.Hosting;
using Prism.DryIoc;
using NModbus.Logging;
using NModbus;
using Microsoft.Extensions.Logging;
using ModbusCommunicator.Services.Configuration;
using System.Diagnostics;
using ModbusCommunicator.Services.Interfaces.ModbusTcpClosedLoopServices;

namespace ModbusCommunicator;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized(); // 调用基类方法

        Debug.WriteLine("--- 开始手动解析测试 ---");
        try
        {
            // 1. 尝试解析 ILoggerFactory 本身
            Debug.WriteLine("尝试解析 ILoggerFactory...");
            var factory = Container.Resolve<ILoggerFactory>();
            Debug.WriteLine($"手动解析 ILoggerFactory 成功! 类型: {factory?.GetType().FullName}");

            // 2. 尝试使用已解析的 Factory 创建 Logger (模拟容器行为)
            if (factory != null)
            {
                Debug.WriteLine("尝试使用 Factory 创建 ILogger<ModbusTcpClosedLoopOptions>...");
                var loggerManual = factory.CreateLogger<ModbusTcpClosedLoopOptions>();
                Debug.WriteLine($"手动创建 ILogger<ModbusTcpClosedLoopOptions> 成功! 类型: {loggerManual?.GetType().FullName}");
            }
            else
            {
                Debug.WriteLine("无法获取 ILoggerFactory 实例，跳过手动创建 Logger 测试。");
            }

            Debug.WriteLine("尝试让容器解析 ILogger<ModbusTcpClosedLoopOptions>...");
            var loggerResolved = Container.Resolve<ILogger<ModbusTcpClosedLoopOptions>>();
            Debug.WriteLine($"容器解析 ILogger<ModbusTcpClosedLoopOptions> 成功! 类型: {loggerResolved?.GetType().FullName}");

        }
        catch (Exception ex)
        {
            // !!! 这里会捕获到容器解析失败时的具体异常 !!!
            Debug.WriteLine($"!!! 手动解析时发生错误: {ex.GetType().FullName} !!!");
            Debug.WriteLine($"错误消息: {ex.Message}");
            Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}"); // 详细的堆栈信息
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"--- 内部异常 ---");
                Debug.WriteLine($"内部异常类型: {ex.InnerException.GetType().FullName}");
                Debug.WriteLine($"内部异常消息: {ex.InnerException.Message}");
                Debug.WriteLine($"内部异常堆栈: {ex.InnerException.StackTrace}");
            }
        }
        Debug.WriteLine("--- 手动解析测试结束 ---");
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
        // 1. 注册 ILoggerFactory (你已有的代码)
        containerRegistry.RegisterSingleton<ILoggerFactory>(() =>
            LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            }));

        //// 2. !!! 获取底层的 DryIoc 注册器并调用 RegisterDelegate !!!
        //// 获取 DryIoc 的 IRegistrator 实例
        //var registrator = containerRegistry.GetContainer(); // GetContainer() 通常返回可用于注册的底层对象

        //// 在获取到的 DryIoc 注册器上调用 RegisterDelegate
        //registrator.RegisterDelegate<ILogger<ModbusTcpClosedLoopOptions>>(
        //    resolver => // resolver 现在是 DryIoc 的 IResolverContext
        //    {
        //        // 从容器中获取已注册的 ILoggerFactory
        //        var factory = resolver.Resolve<ILoggerFactory>();
        //        // 使用工厂创建所需的 Logger 实例
        //        return factory.CreateLogger<ModbusTcpClosedLoopOptions>();
        //    },
        //    reuse: Reuse.Singleton // 明确指定为 Singleton，与 LoggerFactory 保持一致
        //);

        containerRegistry.RegisterSingleton<ILogger<ModbusTcpClosedLoopOptions>>(() =>
        { 
            var factory = containerRegistry.GetContainer().Resolve<ILoggerFactory>();
            return factory.CreateLogger<ModbusTcpClosedLoopOptions>();
        }
        );

        containerRegistry.RegisterSingleton<IModbusTcpConfigurationService, ModbusConfigurationService>();

        containerRegistry.RegisterSingleton<IModbusFactory>(() =>
        new ModbusFactory(logger: new ConsoleModbusLogger(LoggingLevel.Warning)));
        containerRegistry.RegisterSingleton<TcpStatusSharedService>();

        containerRegistry.RegisterSingleton<IModbusTcpMasterClosedLoopService, ModbusTcpMasterClosedLoopService>();
        containerRegistry.RegisterSingleton<IModbusTcpSlaveClosedLoopService, ModbusTcpSlaveClosedLoopService>();

        containerRegistry.RegisterForNavigation<ClientMonitor>();
        containerRegistry.RegisterForNavigation<DataManagement>();
        containerRegistry.RegisterForNavigation<ModBusTCPClosedLoopPage>();
    }
}

