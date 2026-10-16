import { useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { antifraudeApi } from '../api/antifraude';
import type { Analysis, AnalysisStatus } from '../api/types';
import { StatusBadge } from '../components/StatusBadge';
import { formatCpf, formatDateTime } from '../utils/format';
import { usePolling } from '../utils/usePolling';

const FILTERS: { label: string; value?: AnalysisStatus }[] = [
  { label: 'Pendentes', value: 'Pending' },
  { label: 'Aprovadas', value: 'Approved' },
  { label: 'Rejeitadas', value: 'Rejected' },
  { label: 'Todas' },
];

export function AnalysesPage() {
  const navigate = useNavigate();
  const [filter, setFilter] = useState<AnalysisStatus | undefined>('Pending');
  const [items, setItems] = useState<Analysis[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const filterRef = useRef(filter);
  filterRef.current = filter;

  usePolling(
    async () => {
      const requested = filter;
      try {
        const list = await antifraudeApi.list(requested);
        if (requested !== filterRef.current) return; // resposta atrasada de um filtro antigo
        setItems([...list].reverse()); // mais recentes primeiro
        setError(null);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Erro ao carregar as análises.');
      }
    },
    5000,
    true,
    filter,
  );

  function changeFilter(value?: AnalysisStatus) {
    setFilter(value);
    setItems(null);
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>Análises</h1>
          <p className="muted">Propostas finalizadas aguardando a decisão do antifraude. Atualiza a cada 5 segundos.</p>
        </div>
        <Link to="/simular" className="button button-secondary">
          + Simular cliente
        </Link>
      </div>

      <div className="tabs" role="tablist">
        {FILTERS.map((f) => (
          <button
            key={f.label}
            role="tab"
            aria-selected={filter === f.value}
            className={filter === f.value ? 'tab active' : 'tab'}
            onClick={() => changeFilter(f.value)}
          >
            {f.label}
          </button>
        ))}
      </div>

      {error && <p className="alert alert-error">{error}</p>}

      <div className="card table-card">
        {items === null ? (
          <p className="empty">Carregando…</p>
        ) : items.length === 0 ? (
          <p className="empty">
            Nenhuma análise aqui. Use <Link to="/simular">Simular cliente</Link> para criar uma proposta e veja ela
            aparecer.
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Cliente</th>
                <th>CPF</th>
                <th>Recebida em</th>
                <th>Status</th>
                <th>Decidida por</th>
              </tr>
            </thead>
            <tbody>
              {items.map((a) => (
                <tr key={a.id} onClick={() => navigate(`/analises/${a.id}`)} className="clickable">
                  <td>
                    <Link to={`/analises/${a.id}`} onClick={(e) => e.stopPropagation()}>
                      {a.fullName}
                    </Link>
                  </td>
                  <td className="mono">{formatCpf(a.taxId)}</td>
                  <td>{formatDateTime(a.createdAt)}</td>
                  <td>
                    <StatusBadge status={a.status} />
                  </td>
                  <td>{a.decidedBy ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
