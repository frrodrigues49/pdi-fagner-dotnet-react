using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Pdi.Onboarding.Worker.Accounts;

/// Número de conta DETERMINÍSTICO: a mesma proposta + produto gera sempre o mesmo número.
/// Formato (13 dígitos): PPP NNNNNNNNN D
///   PPP = código do produto (3 dígitos)  ·  N = 9 dígitos derivados do hash  ·  D = dígito Luhn
public static class AccountNumberGenerator
{
  public const int Length = 13;

  public static string Generate(Guid proposalId, int productCode)
  {
    if (productCode is < 0 or > 999)
      throw new ArgumentOutOfRangeException(nameof(productCode), "Product code must have up to 3 digits.");

    // SHA-256(proposalId + produto): espalha bem e não depende de estado (sem sequence no banco)
    var input = new byte[20];
    proposalId.ToByteArray().CopyTo(input, 0);
    BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(16), productCode);
    var hash = SHA256.HashData(input);

    var body = BinaryPrimitives.ReadUInt64BigEndian(hash) % 1_000_000_000UL;
    var digits = $"{productCode:D3}{body:D9}";

    return digits + LuhnCheckDigit(digits);
  }

  public static bool IsValid(string accountNumber)
      => accountNumber.Length == Length
         && accountNumber.All(char.IsDigit)
         && LuhnCheckDigit(accountNumber[..^1]) == accountNumber[^1] - '0';

  // Luhn (o mesmo de cartão de crédito): pega erro de digitação de 1 dígito e
  // a maioria das trocas de dois dígitos vizinhos.
  public static int LuhnCheckDigit(string digits)
  {
    var sum = 0;
    for (var i = 0; i < digits.Length; i++)
    {
      var digit = digits[digits.Length - 1 - i] - '0';
      if (i % 2 == 0)
      {
        digit *= 2;
        if (digit > 9)
          digit -= 9;
      }
      sum += digit;
    }

    return (10 - sum % 10) % 10;
  }
}