using Pdi.Onboarding.Worker.Accounts;

namespace Pdi.Onboarding.UnitTests.Worker;

public sealed class AccountNumberGeneratorTests
{
  private static readonly Guid ProposalId = Guid.Parse("c6a14b1e-a7d5-424d-b47e-535c6241bdc2");

  [Fact]
  public void Generate_ShouldBeDeterministic()
  {
    Assert.Equal(
        AccountNumberGenerator.Generate(ProposalId, 173),
        AccountNumberGenerator.Generate(ProposalId, 173));
  }

  [Theory]
  [InlineData(173)]
  [InlineData(120)]
  [InlineData(9)]
  [InlineData(10)]
  public void Generate_ShouldHave13DigitsStartWithProductAndPassLuhn(int productCode)
  {
    var number = AccountNumberGenerator.Generate(ProposalId, productCode);

    Assert.Equal(13, number.Length);
    Assert.All(number, c => Assert.True(char.IsDigit(c)));
    Assert.StartsWith(productCode.ToString("D3"), number);
    Assert.True(AccountNumberGenerator.IsValid(number));
  }

  [Fact]
  public void Generate_ShouldDifferByProductAndByProposal()
  {
    var numbers = new[]
    {
      AccountNumberGenerator.Generate(ProposalId, 173),
      AccountNumberGenerator.Generate(ProposalId, 120),
      AccountNumberGenerator.Generate(Guid.NewGuid(), 173),
      AccountNumberGenerator.Generate(Guid.NewGuid(), 173)
    };

    Assert.Equal(numbers.Length, numbers.Distinct().Count());
  }

  [Fact]
  public void IsValid_WhenOneDigitIsMistyped_ShouldReturnFalse()
  {
    var number = AccountNumberGenerator.Generate(ProposalId, 173);
    var wrongDigit = (char)('0' + (number[5] - '0' + 1) % 10);
    var mistyped = number[..5] + wrongDigit + number[6..];

    Assert.False(AccountNumberGenerator.IsValid(mistyped));
  }

  [Theory]
  [InlineData("7992739871", 3)] // exemplo clássico do algoritmo de Luhn
  [InlineData("0", 0)]
  public void LuhnCheckDigit_ShouldMatchKnownValues(string digits, int expected)
  {
    Assert.Equal(expected, AccountNumberGenerator.LuhnCheckDigit(digits));
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(1000)]
  public void Generate_WhenProductCodeHasMoreThan3Digits_ShouldThrow(int productCode)
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => AccountNumberGenerator.Generate(ProposalId, productCode));
  }
}