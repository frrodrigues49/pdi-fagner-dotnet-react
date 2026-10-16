import { useState, type FormEvent } from 'react';

export const MIN_REASON = 3;
export const MAX_REASON = 500;

interface Props {
  onSubmit: (reason: string) => Promise<void>;
}

/// Mesmas regras do RejectAnalysisValidator da API (3 a 500 caracteres):
/// a tela avisa antes, a API garante.
export function RejectForm({ onSubmit }: Props) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const length = reason.trim().length;
  const valid = length >= MIN_REASON && length <= MAX_REASON;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!valid) return;
    setSending(true);
    setError(null);
    try {
      await onSubmit(reason.trim());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível rejeitar.');
    } finally {
      setSending(false);
    }
  }

  return (
    <form className="reject-form" onSubmit={handleSubmit}>
      <label htmlFor="reason">Motivo da rejeição</label>
      <textarea
        id="reason"
        value={reason}
        maxLength={MAX_REASON}
        rows={3}
        placeholder="Ex.: documento ilegível, divergência de dados..."
        onChange={(e) => setReason(e.target.value)}
      />
      <div className="reject-form-footer">
        <span className={`hint ${length > 0 && !valid ? 'hint-error' : ''}`}>
          {length}/{MAX_REASON} · mínimo {MIN_REASON} caracteres
        </span>
        <button type="submit" className="button button-danger" disabled={!valid || sending}>
          {sending ? 'Rejeitando…' : 'Rejeitar'}
        </button>
      </div>
      {error && (
        <p className="alert alert-error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}
