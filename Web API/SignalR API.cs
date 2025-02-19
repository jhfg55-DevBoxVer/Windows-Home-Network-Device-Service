using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
//将 SignalRAPI 作为插件注入到 Worker Service 项目中，那么它不会以独立的 ASP.NET Core Web API 项目的形式运行，因此不需要单独的 Program.cs。Worker Service 项目的 Program.cs 会作为整个应用的入口点，并通过后台服务来管理这些插件，包括启动和停止。
public class SignalRAPI : IPlugin
{
    private readonly ILogger<SignalRAPI> _logger;
    private HubConnection _hubConnection;

    public SignalRAPI(ILogger<SignalRAPI> logger)
    {
        _logger = logger;
    }

    public string Name => "Web API";
    // StartAsync 方法用于启动 SignalR API，这里使用了 SignalR Client 的 HubConnectionBuilder 类来创建一个 SignalR 连接。
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("192.168.1.1")
            .Build();

        await _hubConnection.StartAsync(cancellationToken);
        _logger.LogInformation("SignalR API 已启动");

        // 这里可以注册 SignalR 事件或方法
        // _hubConnection.On<string>("ReceiveMessage", message => { ... });
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _logger.LogInformation("SignalR API 已停止。");
        }
    }
}
