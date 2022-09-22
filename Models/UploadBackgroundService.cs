namespace MovieCastIdentifier;

public class UploadBackgroundService : BackgroundService
{
    private readonly ILogger<UploadBackgroundService> _logger;
    private readonly IBackgroundTaskQueue _taskQueue;

    public UploadBackgroundService(ILogger<UploadBackgroundService> logger, IBackgroundTaskQueue taskQueue)
    {
        _logger = logger;
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Upload Background Service is starting.");

        stoppingToken.Register(() => _logger.LogInformation("Upload Background Service is stopping."));

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Upload Background Service is doing background work.");

            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred executing {nameof(workItem)}.");
            }
        }

        _logger.LogInformation("Upload Background Service is stopping.");
    }
}