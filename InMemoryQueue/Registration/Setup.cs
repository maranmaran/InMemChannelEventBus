using System.Threading.Channels;
using InMemoryQueue.Contracts;
using InMemoryQueue.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InMemoryQueue.Registration;

public static class Setup
{
    public static IServiceCollection AddInMemoryEvent<T, THandler>(this IServiceCollection services)
        where THandler : class, IEventHandler<T>
    {
        // TODO: Expose configuration options and allow user to customize
        var queue = Channel.CreateUnbounded<Event<T>>(
            new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
            }
        );

        // typed event handler
        services.AddScoped<IEventHandler<T>, THandler>();

        // typed event producer
        services.AddSingleton(typeof(IProducer<T>), _ => new InMemoryQueueProducer<T>(queue.Writer));

        // typed event consumer
        var consumerFactory = (IServiceProvider provider) => new InMemoryQueueConsumer<T>(
            queue.Reader,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<InMemoryQueueConsumer<T>>()
        );
        services.AddSingleton(typeof(IConsumer), consumerFactory.Invoke);
        services.AddSingleton(typeof(IConsumer<T>), consumerFactory.Invoke);

        // typed event context accessor
        services.AddSingleton(typeof(IEventContextAccessor<T>), typeof(EventContextAccessor<T>));

        return services;
    }

    public static async Task<IServiceProvider> StartConsumers(this IServiceProvider services)
    {
        var consumers = services.GetServices<IConsumer>();
        foreach (var consumer in consumers)
        {
            await consumer.Start().ConfigureAwait(false);
        }

        return services;
    }

    public static async Task<IServiceProvider> StopConsumers(this IServiceProvider services)
    {
        var consumers = services.GetServices<IConsumer>();
        foreach (var consumer in consumers)
        {
            await consumer.Stop().ConfigureAwait(false);
        }

        return services;
    }
}