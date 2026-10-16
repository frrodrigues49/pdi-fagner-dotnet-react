import { useState } from 'react';
import { onboardingApi } from '../api/onboarding';
import type { AnalysisStatus, Proposal } from '../api/types';
import { formatDateTime } from '../utils/format';
import { usePolling } from '../utils/usePolling';

interface Step {
  label: string;
  done: boolean;
  detail?: string;
}

const FINAL_STATUSES = ['AccountsRequested', 'Rejected'];

/// Acompanha a proposta no pré-cadastro (API Onboarding) enquanto o fluxo assíncrono
/// anda: aprovar aqui → Kafka → Worker → Mongo precadastro → criar-conta → Oracle.
export function ProposalTracker({ proposalId, analysisStatus }: { proposalId: string; analysisStatus: AnalysisStatus }) {
  const [proposal, setProposal] = useState<Proposal | null>(null);
  const [error, setError] = useState<string | null>(null);

  const finished = proposal !== null && FINAL_STATUSES.includes(proposal.status);
  usePolling(
    async () => {
      try {
        setProposal(await onboardingApi.get(proposalId));
        setError(null);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Erro ao consultar a proposta.');
      }
    },
    2000,
    !finished,
  );

  const rejected = analysisStatus === 'Rejected' || proposal?.status === 'Rejected';
  const steps: Step[] = [
    { label: 'Proposta finalizada pelo cliente', done: true, detail: formatDateTime(proposal?.finalizedAt) },
    { label: 'Análise antifraude criada', done: true },
    rejected
      ? { label: 'Rejeitada no antifraude', done: analysisStatus === 'Rejected' }
      : { label: 'Aprovada no antifraude', done: analysisStatus === 'Approved' },
    rejected
      ? {
          label: 'Pré-cadastro marcado como rejeitado',
          done: proposal?.status === 'Rejected',
          detail: formatDateTime(proposal?.rejectedAt),
        }
      : {
          label: 'Pré-cadastro aprovado',
          done: proposal?.status === 'Approved' || proposal?.status === 'AccountsRequested',
          detail: formatDateTime(proposal?.approvedAt),
        },
  ];
  if (!rejected) {
    steps.push({
      label: 'Contas solicitadas ao core (Oracle): 173, 120, 9 e 10',
      done: proposal?.status === 'AccountsRequested',
      detail: formatDateTime(proposal?.accountsRequestedAt),
    });
  }

  return (
    <section className="card">
      <h2>Acompanhamento da proposta</h2>
      <p className="muted">
        Atualiza sozinho enquanto o Worker processa os eventos do Kafka.
        {!finished && <span className="pulse" aria-hidden="true" />}
      </p>
      <ol className="timeline">
        {steps.map((step) => (
          <li key={step.label} className={step.done ? 'done' : 'waiting'}>
            <span className="timeline-dot" />
            <div>
              <strong>{step.label}</strong>
              {step.done && step.detail && step.detail !== '—' && <small>{step.detail}</small>}
              {!step.done && <small>aguardando…</small>}
            </div>
          </li>
        ))}
      </ol>
      {error && <p className="alert alert-error">{error}</p>}
    </section>
  );
}
