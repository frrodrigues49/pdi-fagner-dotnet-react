import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function LoginPage() {
  const { session, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const [username, setUsername] = useState('analista');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  if (session) return <Navigate to={from} replace />;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await login(username, password);
      navigate(from, { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Não foi possível entrar.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login">
      <form className="card login-card" onSubmit={handleSubmit}>
        <img src="/favicon.svg" alt="" width={44} height={44} />
        <h1>Tela de aprovação</h1>
        <p className="muted">Análise antifraude das propostas de abertura de conta.</p>

        <label htmlFor="username">Usuário</label>
        <input id="username" value={username} autoComplete="username" onChange={(e) => setUsername(e.target.value)} />

        <label htmlFor="password">Senha</label>
        <input
          id="password"
          type="password"
          value={password}
          autoComplete="current-password"
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && (
          <p className="alert alert-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" className="button button-primary" disabled={loading || !username || !password}>
          {loading ? 'Entrando…' : 'Entrar'}
        </button>

        <p className="hint">
          Demo: <code>analista</code> / <code>analista123</code>
        </p>
      </form>
    </div>
  );
}
