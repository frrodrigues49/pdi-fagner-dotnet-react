namespace Pdi.Onboarding.Worker.Kafka;

public sealed record KafkaConsumerRegistration(string Topic, string GroupId);