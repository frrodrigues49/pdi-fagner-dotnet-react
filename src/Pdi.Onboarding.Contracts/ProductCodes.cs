namespace Pdi.Onboarding.Contracts;

public static class ProductCodes
{
  public const int Sinacor = 173;
  public const int Investments = 120;
  public const int LoginType9 = 9;
  public const int LoginType10 = 10;

  public static readonly IReadOnlyList<int> All = [Sinacor, Investments, LoginType9, LoginType10];
}