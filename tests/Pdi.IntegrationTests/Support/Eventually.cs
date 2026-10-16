namespace Pdi.IntegrationTests.Support;

/// Sistemas assíncronos não respondem "na hora": espera a condição ficar verdadeira.
public static class Eventually
{
  public static async Task<T> Get<T>(
      Func<Task<T?>> probe,
      Func<T, bool> until,
      TimeSpan? timeout = null,
      string? because = null)
  {
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
    T? last = default;
    while (DateTime.UtcNow < deadline)
    {
      last = await probe();
      if (last is not null && until(last))
        return last;
      await Task.Delay(200);
    }

    throw new TimeoutException($"Condition not met in time{(because is null ? "" : $": {because}")}. Last value: {last}");
  }

  public static async Task True(Func<Task<bool>> probe, TimeSpan? timeout = null, string? because = null)
  {
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
    while (DateTime.UtcNow < deadline)
    {
      if (await probe())
        return;
      await Task.Delay(200);
    }

    throw new TimeoutException($"Condition not met in time{(because is null ? "" : $": {because}")}.");
  }
}
