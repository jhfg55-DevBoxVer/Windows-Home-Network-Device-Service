using Microsoft.Win32;
using System.Reflection;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<IPlugin> _plugins = new List<IPlugin>();

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    // Windowsϵͳ�Զ���¼�����������ֹͣ�¼�������Ҫ����Ĵ���
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // ���ز��
            LoadPlugins();

            // �������
            foreach (var plugin in _plugins)
            {
                await plugin.StartAsync(stoppingToken);
            }

            // ���ַ�������״̬���ȴ�ֹͣ����
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("������յ�ֹͣ����");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "�������й����з���δ������쳣��");
        }
        finally
        {
            // ֹͣ���
            foreach (var plugin in _plugins)
            {
                await plugin.StopAsync();
            }

            _logger.LogInformation("������ֹͣ�����ɹ��ͷ���Դ��");
        }
    }

    private void LoadPlugins()
    {
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (!Directory.Exists(pluginFolder))
        {
            _logger.LogWarning("���Ŀ¼�����ڣ�{pluginFolder}", pluginFolder);
            return;
        }

        var pluginFiles = Directory.GetFiles(pluginFolder, "*.dll");
        foreach (var file in pluginFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        // ʹ������ע�봴�����ʵ��
                        var plugin = ActivatorUtilities.CreateInstance(_serviceProvider, type) as IPlugin;
                        if (plugin != null)
                        {
                            // ����ע����е������жϲ���Ƿ�����
                            if (IsPluginEnabled(plugin.Name))
                            {
                                _plugins.Add(plugin);
                                _logger.LogInformation("�Ѽ��ز����{pluginName}", plugin.Name);
                            }
                            else
                            {
                                _logger.LogInformation("����ѽ��ã�{pluginName}", plugin.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "���ز��ʱ��������{file}", file);
            }
        }
    }

    /// <summary>
    /// ����ע����е������жϲ���Ƿ����á�
    /// ע���·����HKEY_LOCAL_MACHINE\SOFTWARE\WindowsHomeNetworkDeviceService\Plugins
    /// ����Ϊ�������ʾ���ƣ�ֵ1Ϊ���ã�0Ϊ���á����û�����ã�Ĭ��Ϊ���á�
    /// </summary>
    /// <param name="pluginName">�����ʾ����</param>
    /// <returns>���÷��� true�����򷵻� false</returns>
    private bool IsPluginEnabled(string pluginName)
    {
        const string registryPath = @"SOFTWARE\WindowsHomeNetworkDeviceService\Plugins";
        using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
        {
            if (key != null)
            {
                var value = key.GetValue(pluginName);
                if (value != null && int.TryParse(value.ToString(), out int enabled))
                {
                    return enabled == 1;
                }
            }
        }
        return false;
    }
}
