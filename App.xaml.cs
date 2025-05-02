using System.Windows;
using System.IO;
using PlcCommunicator.Services.Implements;
using PlcCommunicator.Services.Interfaces;
using PlcCommunicator.Views;
using Prism.Ioc;
using Prism.Modularity;
using Microsoft.Extensions.Hosting;
using Prism.DryIoc;
using NModbus.Logging;
using NModbus;
using Microsoft.Extensions.Logging;
using PlcCommunicator.Services.Configuration;
using System.Diagnostics;

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

    protected override void OnInitialized()
    {
        base.OnInitialized(); // ���û��෽��

        Debug.WriteLine("--- ��ʼ�ֶ��������� ---");
        try
        {
            // 1. ���Խ��� ILoggerFactory ����
            Debug.WriteLine("���Խ��� ILoggerFactory...");
            var factory = Container.Resolve<ILoggerFactory>();
            Debug.WriteLine($"�ֶ����� ILoggerFactory �ɹ�! ����: {factory?.GetType().FullName}");

            // 2. ����ʹ���ѽ����� Factory ���� Logger (ģ��������Ϊ)
            if (factory != null)
            {
                Debug.WriteLine("����ʹ�� Factory ���� ILogger<ModbusTcpClosedLoopOptions>...");
                var loggerManual = factory.CreateLogger<ModbusTcpClosedLoopOptions>();
                Debug.WriteLine($"�ֶ����� ILogger<ModbusTcpClosedLoopOptions> �ɹ�! ����: {loggerManual?.GetType().FullName}");
            }
            else
            {
                Debug.WriteLine("�޷���ȡ ILoggerFactory ʵ���������ֶ����� Logger ���ԡ�");
            }

            Debug.WriteLine("�������������� ILogger<ModbusTcpClosedLoopOptions>...");
            var loggerResolved = Container.Resolve<ILogger<ModbusTcpClosedLoopOptions>>();
            Debug.WriteLine($"�������� ILogger<ModbusTcpClosedLoopOptions> �ɹ�! ����: {loggerResolved?.GetType().FullName}");

        }
        catch (Exception ex)
        {
            // !!! ����Ჶ����������ʧ��ʱ�ľ����쳣 !!!
            Debug.WriteLine($"!!! �ֶ�����ʱ��������: {ex.GetType().FullName} !!!");
            Debug.WriteLine($"������Ϣ: {ex.Message}");
            Debug.WriteLine($"��ջ����: {ex.StackTrace}"); // ��ϸ�Ķ�ջ��Ϣ
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"--- �ڲ��쳣 ---");
                Debug.WriteLine($"�ڲ��쳣����: {ex.InnerException.GetType().FullName}");
                Debug.WriteLine($"�ڲ��쳣��Ϣ: {ex.InnerException.Message}");
                Debug.WriteLine($"�ڲ��쳣��ջ: {ex.InnerException.StackTrace}");
            }
        }
        Debug.WriteLine("--- �ֶ��������Խ��� ---");
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
        // 1. ע�� ILoggerFactory (�����еĴ���)
        containerRegistry.RegisterSingleton<ILoggerFactory>(() =>
            LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            }));

        //// 2. !!! ��ȡ�ײ�� DryIoc ע���������� RegisterDelegate !!!
        //// ��ȡ DryIoc �� IRegistrator ʵ��
        //var registrator = containerRegistry.GetContainer(); // GetContainer() ͨ�����ؿ�����ע��ĵײ����

        //// �ڻ�ȡ���� DryIoc ע�����ϵ��� RegisterDelegate
        //registrator.RegisterDelegate<ILogger<ModbusTcpClosedLoopOptions>>(
        //    resolver => // resolver ������ DryIoc �� IResolverContext
        //    {
        //        // �������л�ȡ��ע��� ILoggerFactory
        //        var factory = resolver.Resolve<ILoggerFactory>();
        //        // ʹ�ù������������ Logger ʵ��
        //        return factory.CreateLogger<ModbusTcpClosedLoopOptions>();
        //    },
        //    reuse: Reuse.Singleton // ��ȷָ��Ϊ Singleton���� LoggerFactory ����һ��
        //);

        containerRegistry.RegisterSingleton<ILogger<ModbusTcpClosedLoopOptions>>(() =>
        { 
            var factory = containerRegistry.GetContainer().Resolve<ILoggerFactory>();
            return factory.CreateLogger<ModbusTcpClosedLoopOptions>();
        }
        );

        containerRegistry.RegisterSingleton<IModbusConfigurationService, ModbusConfigurationService>();

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

