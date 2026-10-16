using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Pdi.Onboarding.Worker.Kafka;

public static class KafkaConsumerExtensions
{
  public static IServiceCollection AddKafkaConsumer<TEvent, THandler>(
      this IServiceCollection services,
      string topic,
      string groupId)
      where TEvent : class
      where THandler : class, IMessageHandler<TEvent>
  {
    // Um producer de DLQ só, compartilhado por todos os consumers.
    services.TryAddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();
    services.AddScoped<IMessageHandler<TEvent>, THandler>();

    services.AddSingleton<IHostedService>(sp => new KafkaConsumerService<TEvent>(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IDeadLetterPublisher>(),
        sp.GetRequiredService<IOptions<KafkaOptions>>(),
        new KafkaConsumerRegistration(topic, groupId),
        sp.GetRequiredService<ILogger<KafkaConsumerService<TEvent>>>()));

    return services;
  }
}
