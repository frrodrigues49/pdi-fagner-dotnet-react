using Pdi.Antifraude.Api.Application.Analyses;

namespace Pdi.Antifraude.UnitTests;

public sealed class CreateAnalysisValidatorTests
{
  private readonly CreateAnalysisValidator _validator = new();

  [Fact]
  public void Validate_WhenRequestIsComplete_ShouldReturnNoErrors()
  {
    var errors = _validator.Validate(new CreateAnalysisRequest
    {
      EventId = Guid.NewGuid(),
      ProposalId = Guid.NewGuid(),
      FullName = "Ana Souza",
      TaxId = "52998224725"
    });

    Assert.Empty(errors);
  }

  [Fact]
  public void Validate_WhenEverythingIsMissing_ShouldReturnAllErrors()
  {
    var errors = _validator.Validate(new CreateAnalysisRequest());

    Assert.Contains(errors, e => e.Contains("EventId"));
    Assert.Contains(errors, e => e.Contains("ProposalId"));
    Assert.Contains(errors, e => e.Contains("FullName"));
    Assert.Contains(errors, e => e.Contains("TaxId"));
  }
}

public sealed class RejectAnalysisValidatorTests
{
  private readonly RejectAnalysisValidator _validator = new();

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  [InlineData("ab")]
  public void Validate_WhenReasonIsTooShort_ShouldReturnError(string reason)
  {
    var errors = _validator.Validate(new RejectAnalysisRequest { Reason = reason });

    Assert.Single(errors);
  }

  [Fact]
  public void Validate_WhenReasonIsTooLong_ShouldReturnError()
  {
    var reason = new string('a', RejectAnalysisValidator.MaxReasonLength + 1);

    var errors = _validator.Validate(new RejectAnalysisRequest { Reason = reason });

    Assert.Contains(errors, e => e.Contains("at most"));
  }

  [Fact]
  public void Validate_WhenReasonIsValid_ShouldReturnNoErrors()
  {
    var errors = _validator.Validate(new RejectAnalysisRequest { Reason = "Documentos ilegiveis" });

    Assert.Empty(errors);
  }
}
