import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { antifraudeApi } from '../api/antifraude';
import { configureHttp } from '../api/http';
import type { LoginResponse } from '../api/types';

type Session = LoginResponse;

interface AuthContextValue {
  session: Session | null;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const STORAGE_KEY = 'pdi.antifraude.session';
const AuthContext = createContext<AuthContextValue | null>(null);

// sessionStorage: some ao fechar a aba. Pode falhar (aba anônima, bloqueio): por isso o try.
function loadSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    return new Date(session.expiresAt) > new Date() ? session : null;
  } catch {
    return null;
  }
}

function saveSession(session: Session | null) {
  try {
    if (session) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // sem storage: a sessão vale só enquanto a página estiver aberta
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession);
  const sessionRef = useRef(session);
  sessionRef.current = session;

  const logout = useCallback(() => {
    setSession(null);
    saveSession(null);
  }, []);

  // Configura durante o render (e não num effect): effects dos filhos rodam ANTES
  // dos do pai, e a primeira chamada da lista sairia sem token.
  configureHttp({ getToken: () => sessionRef.current?.accessToken ?? null, onUnauthorized: logout });

  const login = useCallback(async (username: string, password: string) => {
    const response = await antifraudeApi.login(username, password);
    setSession(response);
    saveSession(response);
  }, []);

  return <AuthContext.Provider value={{ session, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>');
  return context;
}

export function RequireAuth({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();
  if (!session) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
}
