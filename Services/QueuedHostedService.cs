namespace MovieCastIdentifier.Services;

public class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueuedHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public QueuedHostedService(IBackgroundTaskQueue taskQueue, ILogger<QueuedHostedService> logger, IServiceScopeFactory scopeFactory)
    {
        _taskQueue = taskQueue;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Background Service is starting.");

        stoppingToken.Register(() =>
            _logger.LogInformation("Queue Background Service is stopping."));

        await BackgroundProcessing(stoppingToken);
    }

    public async Task BackgroundProcessing(CancellationToken stoppingToken)
    {
        // Dequeue and execute tasks until the application is stopped
        while (!stoppingToken.IsCancellationRequested)
        {
            // Get next task
            // This blocks until a task becomes available
            var task = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                // Execute task
                await task(_scopeFactory, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error occurred executing {WorkItem}.", nameof(task));
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queued Hosted Service is stopping.");

        await base.StopAsync(stoppingToken);
    }
}