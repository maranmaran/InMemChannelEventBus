using System.Threading.Channels;
using InMemoryQueue.Contracts;

namespace InMemoryQueue.Implementation;

internal sealed class InMemoryQueueProducer<T> : IProducer<T>
{
    private readonly ChannelWriter<Event<T>> _queue;

    public InMemoryQueueProducer(ChannelWriter<Event<T>> queue)
    {
        _queue = queue;
    }

    public async ValueTask Publish(Event<T> @event, CancellationToken token = default)
    {
        await _queue.WriteAsync(@event, token).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _queue.TryComplete();

        return ValueTask.CompletedTask;
    }
}