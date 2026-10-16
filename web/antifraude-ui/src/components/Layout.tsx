import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function Layout() {
  const { session, logout } = useAuth();

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <img src="/favicon.svg" alt="" width={28} height={28} />
          <span>
            PDI <strong>Antifraude</strong>
          </span>
        </div>
        <nav>
          <NavLink to="/" end>
            Análises
          </NavLink>
          <NavLink to="/simular">Simular cliente</NavLink>
        </nav>
        <div className="user">
          <span title={session?.username}>{session?.displayName}</span>
          <button type="button" className="button button-ghost" onClick={logout}>
            Sair
          </button>
        </div>
      </header>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
