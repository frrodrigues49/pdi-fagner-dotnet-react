import type { AnalysisStatus, ProposalStatus } from '../api/types';

const LABELS: Record<AnalysisStatus | ProposalStatus, string> = {
  Pending: 'Pendente',
  Approved: 'Aprovada',
  Rejected: 'Rejeitada',
  Draft: 'Rascunho',
  Finalized: 'Finalizada',
  AccountsRequested: 'Contas solicitadas',
};

export function StatusBadge({ status }: { status: AnalysisStatus | ProposalStatus }) {
  return <span className={`badge badge-${status}`}>{LABELS[status]}</span>;
}
