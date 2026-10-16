const onlyDigits = (value: string) => value.replace(/\D/g, '');

export function formatCpf(value: string): string {
  const d = onlyDigits(value);
  return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : value;
}

export function formatPhone(value: string): string {
  const d = onlyDigits(value);
  if (d.length === 11) return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`;
  if (d.length === 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`;
  return value;
}

export function formatPostalCode(value: string): string {
  const d = onlyDigits(value);
  return d.length === 8 ? `${d.slice(0, 5)}-${d.slice(5)}` : value;
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return '—';
  const [year, month, day] = isoDate.split('-');
  return `${day}/${month}/${year}`;
}

function cpfCheckDigit(digits: number[]): number {
  const weightStart = digits.length + 1;
  const sum = digits.reduce((acc, digit, i) => acc + digit * (weightStart - i), 0);
  const rest = sum % 11;
  return rest < 2 ? 0 : 11 - rest;
}

/// Mesmo algoritmo do ProposalValidator da API Onboarding.
export function isValidCpf(value: string): boolean {
  const d = onlyDigits(value);
  if (d.length !== 11 || /^(\d)\1{10}$/.test(d)) return false;
  const numbers = [...d].map(Number);
  return cpfCheckDigit(numbers.slice(0, 9)) === numbers[9] && cpfCheckDigit(numbers.slice(0, 10)) === numbers[10];
}

/// Gera um CPF válido (só para dados de teste na tela "Simular cliente").
export function generateCpf(random: () => number = Math.random): string {
  let base: number[];
  do {
    base = Array.from({ length: 9 }, () => Math.floor(random() * 10));
  } while (base.every((n) => n === base[0]));
  const first = cpfCheckDigit(base);
  const second = cpfCheckDigit([...base, first]);
  return [...base, first, second].join('');
}
