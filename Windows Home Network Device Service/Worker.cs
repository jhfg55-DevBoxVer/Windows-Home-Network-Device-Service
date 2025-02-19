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

    // Windows系统自动记录服务的启动和停止事件，不需要额外的代码
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 加载插件
            LoadPlugins();

            // 启动插件
            foreach (var plugin in _plugins)
            {
                await plugin.StartAsync(stoppingToken);
            }

            // 保持服务运行状态，等待停止请求
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("服务接收到停止请求。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "服务运行过程中发生未处理的异常。");
        }
        finally
        {
            // 停止插件
            foreach (var plugin in _plugins)
            {
                await plugin.StopAsync();
            }

            _logger.LogInformation("服务已停止，并成功释放资源。");
        }
    }

    private void LoadPlugins()
    {
        var pluginFolder = Path.Combine(AppContext.BaseDirectory, "Plugins");
        if (!Directory.Exists(pluginFolder))
        {
            _logger.LogWarning("插件目录不存在：{pluginFolder}", pluginFolder);
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
                        // 使用依赖注入创建插件实例
                        var plugin = ActivatorUtilities.CreateInstance(_serviceProvider, type) as IPlugin;
                        if (plugin != null)
                        {
                            // 根据注册表中的配置判断插件是否启用
                            if (IsPluginEnabled(plugin.Name))
                            {
                                _plugins.Add(plugin);
                                _logger.LogInformation("已加载插件：{pluginName}", plugin.Name);
                            }
                            else
                            {
                                _logger.LogInformation("插件已禁用：{pluginName}", plugin.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件时发生错误：{file}", file);
            }
        }
    }

    /// <summary>
    /// 根据注册表中的配置判断插件是否启用。
    /// 注册表路径：HKEY_LOCAL_MACHINE\SOFTWARE\WindowsHomeNetworkDeviceService\Plugins
    /// 键名为插件的显示名称，值1为启用，0为禁用。如果没有配置，默认为禁用。
    /// </summary>
    /// <param name="pluginName">插件显示名称</param>
    /// <returns>启用返回 true；否则返回 false</returns>
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
