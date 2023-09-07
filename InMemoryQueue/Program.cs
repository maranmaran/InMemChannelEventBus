using InMemoryQueue;
using InMemoryQueue.Contracts;
using InMemoryQueue.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole());

// Test scoped dependency that will be consumed in DateEventHandler
services.AddScoped<ITraceDependency, TraceDependency>();

// Register queue services
services.AddInMemoryEvent<DateTime, DateEventHandler>();
services.AddInMemoryEvent<OrderEvent, OrderPlacedEventHandler>();
services.AddInMemoryEvent<OrderEvent, TrackUserOrderItemsEventHandler>();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

var dateProducer = provider.GetRequiredService<IProducer<DateTime>>();
var orderProducer = provider.GetRequiredService<IProducer<OrderEvent>>();
var publishEventsFn = async (int i) =>
{
    var dateTask = new Event<DateTime>(DateTime.UtcNow);
    var counterTask = new Event<OrderEvent>(new OrderEvent(i, i, i));

    await dateProducer.Publish(dateTask).ConfigureAwait(false);
    await orderProducer.Publish(counterTask).ConfigureAwait(false);
};

await provider.StartConsumers();

Console.ReadKey();

for (var i = 0; i < 3; i++)
{
    await publishEventsFn.Invoke(i);
}

// allow events to process and stop consumers
await Task.Delay(TimeSpan.FromSeconds(3));
await provider.StopConsumers();
logger.LogInformation("\nConsumers stopped\n");

// queue some more events while the consumers are stopped
for (var i = 3; i < 8; i++)
{
    await publishEventsFn.Invoke(i);
}

// start the consumers
logger.LogInformation("\nConsumers started\n");
await provider.StartConsumers();
await Task.Delay(TimeSpan.FromSeconds(3));