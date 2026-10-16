import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { antifraudeApi } from '../api/antifraude';
import type { Analysis } from '../api/types';
import { ProposalTracker } from '../components/ProposalTracker';
import { RejectForm } from '../components/RejectForm';
import { StatusBadge } from '../components/StatusBadge';
import { formatCpf, formatDate, formatDateTime, formatPhone, formatPostalCode } from '../utils/format';
import { usePolling } from '../utils/usePolling';

export function AnalysisDetailPage() {
  const { id = '' } = useParams();
  const [analysis, setAnalysis] = useState<Analysis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [approving, setApproving] = useState(false);
  const [loaded, setLoaded] = useState(false);

  usePolling(
    async () => {
      try {
        setAnalysis(await antifraudeApi.get(id));
        setError(null);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Erro ao carregar a análise.');
      } finally {
        setLoaded(true);
      }
    },
    5000,
    true,
    id,
  );

  async function approve() {
    setApproving(true);
    setError(null);
    try {
      setAnalysis(await antifraudeApi.approve(id));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível aprovar.');
    } finally {
      setApproving(false);
    }
  }

  async function reject(reason: string) {
    setAnalysis(await antifraudeApi.reject(id, reason));
  }

  if (!loaded) return <p className="empty">Carregando…</p>;
  if (!analysis)
    return (
      <>
        <Link to="/" className="back">
          ← Análises
        </Link>
        <p className="alert alert-error">{error ?? 'Análise não encontrada.'}</p>
      </>
    );

  const fields: [string, string][] = [
    ['CPF', formatCpf(analysis.taxId)],
    ['E-mail', analysis.email || '—'],
    ['Telefone', analysis.phone ? formatPhone(analysis.phone) : '—'],
    ['Nascimento', formatDate(analysis.birthDate)],
    ['Nome da mãe', analysis.motherName || '—'],
    ['Endereço', [analysis.street, analysis.city, analysis.state].filter(Boolean).join(' · ') || '—'],
    ['CEP', analysis.postalCode ? formatPostalCode(analysis.postalCode) : '—'],
  ];

  return (
    <>
      <Link to="/" className="back">
        ← Análises
      </Link>

      <div className="page-header">
        <div>
          <h1>{analysis.fullName}</h1>
          <p className="muted">
            Recebida em {formatDateTime(analysis.createdAt)} · proposta <code>{analysis.proposalId}</code>
          </p>
        </div>
        <StatusBadge status={analysis.status} />
      </div>

      {error && <p className="alert alert-error">{error}</p>}

      <div className="grid">
        <section className="card">
          <h2>Dados do cliente</h2>
          <p className="muted">Cópia dos dados no momento em que a proposta foi finalizada.</p>
          <dl className="fields">
            {fields.map(([label, value]) => (
              <div key={label}>
                <dt>{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className="card">
          <h2>Decisão</h2>
          {analysis.status === 'Pending' ? (
            <>
              <p className="muted">Aprovar dispara a criação das contas 173, 120, 9 e 10 no core.</p>
              <button type="button" className="button button-primary button-block" onClick={approve} disabled={approving}>
                {approving ? 'Aprovando…' : 'Aprovar proposta'}
              </button>
              <div className="divider">ou</div>
              <RejectForm onSubmit={reject} />
            </>
          ) : (
            <dl className="fields">
              <div>
                <dt>Resultado</dt>
                <dd>
                  <StatusBadge status={analysis.status} />
                </dd>
              </div>
              <div>
                <dt>Decidido por</dt>
                <dd>{analysis.decidedBy}</dd>
              </div>
              <div>
                <dt>Em</dt>
                <dd>{formatDateTime(analysis.decidedAt)}</dd>
              </div>
              {analysis.rejectionReason && (
                <div>
                  <dt>Motivo</dt>
                  <dd>{analysis.rejectionReason}</dd>
                </div>
              )}
            </dl>
          )}
        </section>
      </div>

      <ProposalTracker proposalId={analysis.proposalId} analysisStatus={analysis.status} />
    </>
  );
}
