import { describe, expect, it } from 'vitest';
import { formatCpf, formatDate, formatPhone, formatPostalCode, generateCpf, isValidCpf } from './format';

describe('format', () => {
  it('formata CPF, telefone e CEP', () => {
    expect(formatCpf('52998224725')).toBe('529.982.247-25');
    expect(formatPhone('11999998888')).toBe('(11) 99999-8888');
    expect(formatPhone('1133334444')).toBe('(11) 3333-4444');
    expect(formatPostalCode('01310100')).toBe('01310-100');
    expect(formatDate('1990-05-15')).toBe('15/05/1990');
  });

  it('mantém o valor original quando não dá para formatar', () => {
    expect(formatCpf('123')).toBe('123');
    expect(formatDate(null)).toBe('—');
  });
});

describe('CPF', () => {
  it.each(['52998224725', '529.982.247-25', '11144477735'])('aceita CPF válido %s', (cpf) => {
    expect(isValidCpf(cpf)).toBe(true);
  });

  it.each(['11111111111', '52998224724', '123', ''])('recusa CPF inválido %s', (cpf) => {
    expect(isValidCpf(cpf)).toBe(false);
  });

  it('gera CPFs sempre válidos', () => {
    for (let i = 0; i < 200; i++) expect(isValidCpf(generateCpf())).toBe(true);
  });

  it('não gera CPF de dígitos repetidos mesmo com um random "viciado"', () => {
    // 1ª tentativa: nove "1" (inválido) → o gerador tem que descartar e sortear de novo.
    let calls = 0;
    const random = () => (calls++ < 9 ? 0.1 : (calls % 10) / 10);

    const cpf = generateCpf(random);

    expect(calls).toBeGreaterThan(9);
    expect(isValidCpf(cpf)).toBe(true);
  });
});
