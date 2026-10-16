using System.Text.RegularExpressions;

namespace Pdi.Onboarding.Api.Application.Proposals;

public sealed class ProposalValidator
{
  private static readonly HashSet<string> BrazilianStates =
  [
      "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
        "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO"
  ];

  public IReadOnlyList<string> Validate(ProposalValidationRequest request)
  {
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 3)
      errors.Add("FullName is required and must have at least 3 characters.");

    if (!IsValidCpf(request.TaxId))
      errors.Add("TaxId must be a valid CPF.");

    if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
      errors.Add("Email must be a valid email address.");

    if (!IsValidPhone(request.Phone))
      errors.Add("Phone must have 10 or 11 digits.");

    if (!IsAdult(request.BirthDate))
      errors.Add("BirthDate must belong to an adult (18+).");

    if (string.IsNullOrWhiteSpace(request.Street))
      errors.Add("Street is required.");

    if (string.IsNullOrWhiteSpace(request.City))
      errors.Add("City is required.");

    if (string.IsNullOrWhiteSpace(request.State) || !BrazilianStates.Contains(request.State.Trim().ToUpperInvariant()))
      errors.Add("State must be a valid Brazilian UF.");

    if (!IsValidPostalCode(request.PostalCode))
      errors.Add("PostalCode must have 8 digits.");

    if (string.IsNullOrWhiteSpace(request.MotherName) || request.MotherName.Trim().Length < 3)
      errors.Add("MotherName is required and must have at least 3 characters.");

    return errors;
  }

  private static bool IsAdult(DateOnly birthDate)
  {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    if (birthDate == default || birthDate >= today)
      return false;

    var age = today.Year - birthDate.Year;
    if (birthDate > today.AddYears(-age))
      age--;

    return age >= 18;
  }

  private static bool IsValidEmail(string email)
  {
    return Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
  }

  private static bool IsValidPhone(string phone)
  {
    var digits = DigitsOnly(phone);
    return digits.Length is 10 or 11;
  }

  private static bool IsValidPostalCode(string postalCode)
  {
    return DigitsOnly(postalCode).Length == 8;
  }

  private static bool IsValidCpf(string taxId)
  {
    var digits = DigitsOnly(taxId);
    if (digits.Length != 11)
      return false;

    if (digits.Distinct().Count() == 1)
      return false;

    return HasValidCpfCheckDigits(digits);
  }

  private static bool HasValidCpfCheckDigits(string digits)
  {
    var numbers = digits.Select(c => c - '0').ToArray();

    var sum = 0;
    for (var i = 0; i < 9; i++)
      sum += numbers[i] * (10 - i);

    var firstDigit = sum % 11 < 2 ? 0 : 11 - (sum % 11);
    if (numbers[9] != firstDigit)
      return false;

    sum = 0;
    for (var i = 0; i < 10; i++)
      sum += numbers[i] * (11 - i);

    var secondDigit = sum % 11 < 2 ? 0 : 11 - (sum % 11);
    return numbers[10] == secondDigit;
  }

  private static string DigitsOnly(string value)
  {
    return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
  }
}