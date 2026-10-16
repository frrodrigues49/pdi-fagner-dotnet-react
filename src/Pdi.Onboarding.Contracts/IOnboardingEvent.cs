namespace Pdi.Onboarding.Contracts;

public interface IOnboardingEvent
{
  Guid EventId { get; }
  Guid CorrelationId { get; }
}