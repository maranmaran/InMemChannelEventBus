using System.Threading.Channels;
using InMemoryQueue.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable CS4014

namespace InMemoryQueue.Implementation;

internal sealed class InMemoryQueueConsumer<T> : IConsumer<T>
{
    private readonly ChannelReader<Event<T>> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InMemoryQueueConsumer<T>> _logger;

    private CancellationTokenSource? _stoppingToken;

    public InMemoryQueueConsumer(
        ChannelReader<Event<T>> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<InMemoryQueueConsumer<T>> logger
    )
    {
        _logger = logger;
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask Start(CancellationToken token = default)
    {
        EnsureStoppingTokenIsCreated();

        // factory new scope so we can use it as execution context
        await using var scope = _scopeFactory.CreateAsyncScope();

        // retrieve scoped dependencies
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<T>>().ToList();
        var metadataAccessor = scope.ServiceProvider.GetRequiredService<IEventContextAccessor<T>>();

        if (handlers.FirstOrDefault() is null)
        {
            _logger.LogDebug("No handlers defined for event of {type}", typeof(T).Name);
            return;
        }

        Task.Run(
            async () => await StartProcessing(handlers, metadataAccessor).ConfigureAwait(false),
            _stoppingToken!.Token
        ).ConfigureAwait(false);
    }

    private void EnsureStoppingTokenIsCreated()
    {
        if (_stoppingToken is not null && _stoppingToken.IsCancellationRequested == false)
        {
            _stoppingToken.Cancel();
        }

        _stoppingToken = new CancellationTokenSource();
    }

    internal async ValueTask StartProcessing(List<IEventHandler<T>> handlers, IEventContextAccessor<T> contextAccessor)
    {
        var continuousChannelIterator = _queue.ReadAllAsync(_stoppingToken.Token)
            .WithCancellation(_stoppingToken.Token)
            .ConfigureAwait(false);

        await foreach (var task in continuousChannelIterator)
        {
            if (_stoppingToken.IsCancellationRequested)
            {
                break;
            }

            contextAccessor.Set(task); // set metadata and begin scope
            using var logScope = _logger.BeginScope(task.Metadata);

            // invoke handlers in parallel
            await Parallel.ForEachAsync(handlers, _stoppingToken.Token,
                (handler, scopedToken) =>
                {
                    Task.Run(
                        async () => await handler.Handle(task.Data, scopedToken), scopedToken
                    ).ConfigureAwait(false);

                    return ValueTask.CompletedTask;
                }
            ).ConfigureAwait(false);
        }
    }

    public async ValueTask Stop(CancellationToken _ = default)
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _stoppingToken?.Cancel();
    }
}