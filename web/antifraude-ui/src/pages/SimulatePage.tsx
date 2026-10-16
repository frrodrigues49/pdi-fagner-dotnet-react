import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../api/http';
import { onboardingApi } from '../api/onboarding';
import type { Proposal, ProposalInput } from '../api/types';
import { StatusBadge } from '../components/StatusBadge';
import { generateCpf } from '../utils/format';

const FIRST = ['Ana', 'Bruno', 'Carla', 'Diego', 'Elisa', 'Fábio', 'Gabriela', 'Henrique', 'Isabela', 'João'];
const LAST = ['Souza', 'Lima', 'Pereira', 'Almeida', 'Costa', 'Ribeiro', 'Martins', 'Rocha', 'Dias', 'Nunes'];
const pick = <T,>(list: T[]) => list[Math.floor(Math.random() * list.length)];

function sampleCustomer(): ProposalInput {
  const first = pick(FIRST);
  const last = pick(LAST);
  return {
    fullName: `${first} ${last}`,
    taxId: generateCpf(),
    email: `${first.toLowerCase().normalize('NFD').replace(/\p{M}/gu, '')}.${last.toLowerCase()}@email.com`,
    phone: '11999998888',
    birthDate: '1990-05-15',
    street: 'Av. Paulista, 1000',
    city: 'São Paulo',
    state: 'SP',
    postalCode: '01310100',
    motherName: `Maria ${last}`,
  };
}

const FIELDS: { key: keyof ProposalInput; label: string; type?: string }[] = [
  { key: 'fullName', label: 'Nome completo' },
  { key: 'taxId', label: 'CPF' },
  { key: 'email', label: 'E-mail', type: 'email' },
  { key: 'phone', label: 'Telefone' },
  { key: 'birthDate', label: 'Nascimento', type: 'date' },
  { key: 'motherName', label: 'Nome da mãe' },
  { key: 'street', label: 'Endereço' },
  { key: 'city', label: 'Cidade' },
  { key: 'state', label: 'UF' },
  { key: 'postalCode', label: 'CEP' },
];

/// Faz o papel do "Postman" do organograma: cria o rascunho e finaliza na API Onboarding.
export function SimulatePage() {
  const [form, setForm] = useState<ProposalInput>(sampleCustomer);
  const [result, setResult] = useState<Proposal | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [sending, setSending] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setSending(true);
    setErrors([]);
    setResult(null);
    try {
      const draft = await onboardingApi.create(form);
      setResult(await onboardingApi.finalize(draft.id));
    } catch (e) {
      setErrors(e instanceof ApiError ? e.errors : ['Não foi possível criar a proposta.']);
    } finally {
      setSending(false);
    }
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Simular cliente</h1>
          <p className="muted">
            Cria o pré-cadastro e finaliza na API Onboarding. A finalização publica <code>proposta-conta-finalizada</code>,
            e o Worker cria a análise no antifraude.
          </p>
        </div>
        <button type="button" className="button button-secondary" onClick={() => setForm(sampleCustomer())}>
          Gerar outro cliente
        </button>
      </div>

      <form className="card" onSubmit={handleSubmit}>
        <div className="form-grid">
          {FIELDS.map((f) => (
            <label key={f.key}>
              {f.label}
              <input
                type={f.type ?? 'text'}
                value={form[f.key]}
                onChange={(e) => setForm({ ...form, [f.key]: e.target.value })}
              />
            </label>
          ))}
        </div>

        {errors.length > 0 && (
          <div className="alert alert-error" role="alert">
            <strong>A API recusou a proposta:</strong>
            <ul>
              {errors.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          </div>
        )}

        <button type="submit" className="button button-primary" disabled={sending}>
          {sending ? 'Enviando…' : 'Criar e finalizar proposta'}
        </button>
      </form>

      {result && (
        <div className="alert alert-success" role="status">
          Proposta <code>{result.id}</code> <StatusBadge status={result.status} />. Em instantes ela aparece em{' '}
          <Link to="/">Análises pendentes</Link>.
        </div>
      )}
    </>
  );
}
