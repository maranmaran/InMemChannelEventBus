using InMemoryEventBus;
using InMemoryEventBus.Contracts;
using InMemoryEventBus.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole());

// Register bus services
services.AddInMemoryEvent<int, OrderNumberEventHandler>();
services.AddInMemoryEvent<OrderEvent, OrderPlacedEventHandler>();
services.AddInMemoryEvent<OrderEvent, TrackUserOrderItemsEventHandler>();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

var orderNumberProducer = provider.GetRequiredService<IProducer<int>>();
var orderProducer = provider.GetRequiredService<IProducer<OrderEvent>>();
var publishEventsFn = async (int i) =>
{
    var metadata = new EventMetadata(Guid.NewGuid().ToString());

    var orderNumberTask = new Event<int>(i, metadata);
    var counterTask = new Event<OrderEvent>(new OrderEvent(i, i, i), metadata);

    await orderNumberProducer.Publish(orderNumberTask).ConfigureAwait(false);
    await orderProducer.Publish(counterTask).ConfigureAwait(false);
};

await provider.StartConsumers();

for (var i = 0; i < 3; i++)
{
    await publishEventsFn.Invoke(i);
}

// allow events to process and stop consumers
await Task.Delay(TimeSpan.FromSeconds(3));
await provider.StopConsumers();
logger.LogInformation("\nConsumers stopped\n");

// bus some more events while the consumers are stopped
for (var i = 3; i < 8; i++)
{
    await publishEventsFn.Invoke(i);
    logger.LogInformation("EventBusd {0}, but consumers are not running yet", i);
}

// start the consumers
logger.LogInformation("\nConsumers started\n");
await provider.StartConsumers();
await Task.Delay(TimeSpan.FromSeconds(3));
await provider.StopConsumers();

// Prof using a parent token
logger.LogInformation("\n-------------------------------------------\n");

// First case, the token is canceled before start
var masterTokenSource = new CancellationTokenSource();
masterTokenSource.CancelAfter(TimeSpan.FromSeconds(3));
masterTokenSource.Token
    .Register(_ => logger.LogInformation("First case: a parent token was token was Canceled after 3sec "), null);

// Events while the consumers are stopped
for (var i = 100; i < 105; i++)
{
    await Task.Delay(TimeSpan.FromSeconds(1));
    await publishEventsFn.Invoke(i);
    logger.LogInformation("Published event {i}", i);
}
// 5sec later -> master token is Canceled after 3.

logger.LogInformation("Consumers starting with a master token Canceled\n");
// Do nothing, the masterTokenSource.Token was canceled.
await provider.StartConsumers(masterTokenSource.Token);
await Task.Delay(TimeSpan.FromSeconds(5));
await provider.StopConsumers();
logger.LogInformation("Stopped consumer after 5ec\n");

// Second case, the token is canceled while events are generated and consumed

var otherMasterTokenSource = new CancellationTokenSource();
otherMasterTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
otherMasterTokenSource.Token
    .Register(_ => logger.LogInformation("Second Case: a parent token was Canceled after 5sec "), null);

logger.LogInformation("Consumers started with a master token that will canceled in 5sec\n");
await provider.StartConsumers(otherMasterTokenSource.Token);
for (var i = 200; i < 210; i++)
{
    await Task.Delay(TimeSpan.FromSeconds(1));
    // After 5 seconds some events will not be processed --> otherMasterTokenSource.Token is Cancelled after 5sec
    logger.LogInformation("Start invoke event {i}\n", i);
    await publishEventsFn.Invoke(i);
    logger.LogInformation("End invoke event {i}\n", i);
    
}
await provider.StopConsumers();
