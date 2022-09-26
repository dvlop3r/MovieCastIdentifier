namespace MovieCastIdentifier.Services;

using System.Collections.Concurrent;
using System.Threading.Channels;

public interface IBackgroundTaskQueue
{
    // Enqueues the given task.
    void EnqueueAsync(Func<IServiceScopeFactory, CancellationToken, Task> workItem);

    // Dequeues and returns one task. This method blocks until a task becomes available.
    Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}

public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<Func<IServiceScopeFactory, CancellationToken, Task>> _items = new();
    
    // Holds the current count of tasks in the queue.
    private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
    
    public void EnqueueAsync(Func<IServiceScopeFactory, CancellationToken, Task> workItem)
    {
        if(workItem == null)
            throw new ArgumentNullException(nameof(workItem));
        _items.Enqueue(workItem);
        _signal.Release();
    }

    public async Task<Func<IServiceScopeFactory, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        // Wait for task to become available
        await _signal.WaitAsync(cancellationToken);

        _items.TryDequeue(out var task);
        return task;
    }
}