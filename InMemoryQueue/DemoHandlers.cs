using InMemoryQueue.Contracts;
using Microsoft.Extensions.Logging;

namespace InMemoryQueue;

public sealed record OrderEvent(int OrderNumber, int ItemCount, int UserId);

public sealed class OrderPlacedEventHandler : IEventHandler<OrderEvent>
{
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public OrderPlacedEventHandler(ILogger<OrderPlacedEventHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(OrderEvent time, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Order {0} has been placed", time.OrderNumber);
        return ValueTask.CompletedTask;
    }
}

public sealed class TrackUserOrderItemsEventHandler : IEventHandler<OrderEvent>
{
    private readonly ILogger<TrackUserOrderItemsEventHandler> _logger;

    public TrackUserOrderItemsEventHandler(ILogger<TrackUserOrderItemsEventHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(OrderEvent? time, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User {0} has ordered {1} items", time.UserId, time.ItemCount);
        return ValueTask.CompletedTask;
    }
}

public sealed class DateEventHandler : IEventHandler<DateTime>
{
    private readonly ITraceDependency _logger;

    public DateEventHandler(ITraceDependency logger)
    {
        _logger = logger;
    }

    public ValueTask Handle(DateTime time, CancellationToken cancellationToken = default)
    {
        _logger.Log(time);
        return ValueTask.CompletedTask;
    }
}

public interface ITraceDependency
{
    public void Log(DateTime time);
}

public class TraceDependency : ITraceDependency
{
    private readonly ILogger<TraceDependency> _logger;

    public TraceDependency(ILogger<TraceDependency> logger)
    {
        _logger = logger;
    }

    public void Log(DateTime time)
    {
        _logger.LogInformation("Triggered at {0}, but processing of event triggered at {1}", time, DateTime.UtcNow);
    }
}