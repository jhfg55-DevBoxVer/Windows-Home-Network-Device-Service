public interface IPlugin
{
    string Name { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}
