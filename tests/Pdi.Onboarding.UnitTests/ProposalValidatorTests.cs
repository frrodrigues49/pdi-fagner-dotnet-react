using Pdi.Onboarding.Api.Application.Proposals;

namespace Pdi.Onboarding.UnitTests;

public class ProposalValidatorTests
{
  private readonly ProposalValidator _validator = new();

  [Fact]
  public void Validate_WhenPayloadIsValid_ShouldReturnNoErrors()
  {
    var errors = _validator.Validate(ValidRequest());

    Assert.Empty(errors);
  }

  [Fact]
  public void Validate_WhenFullNameIsMissing_ShouldReturnError()
  {
    var request = ValidRequest();
    request.FullName = "";

    var errors = _validator.Validate(request);

    Assert.Contains(errors, error => error.Contains("FullName"));
  }

  [Fact]
  public void Validate_WhenTaxIdIsInvalid_ShouldReturnError()
  {
    var request = ValidRequest();
    request.TaxId = "11111111111";

    var errors = _validator.Validate(request);

    Assert.Contains(errors, error => error.Contains("TaxId"));
  }

  [Fact]
  public void Validate_WhenPersonIsUnderage_ShouldReturnError()
  {
    var request = ValidRequest();
    request.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-17));

    var errors = _validator.Validate(request);

    Assert.Contains(errors, error => error.Contains("BirthDate"));
  }

  private static ProposalValidationRequest ValidRequest()
  {
    return new ProposalValidationRequest
    {
      FullName = "Ana Souza",
      TaxId = "52998224725",
      Email = "ana@email.com",
      Phone = "11999998888",
      BirthDate = new DateOnly(1990, 5, 15),
      Street = "Rua A, 100",
      City = "Sao Paulo",
      State = "SP",
      PostalCode = "01310100",
      MotherName = "Maria Souza"
    };
  }
}