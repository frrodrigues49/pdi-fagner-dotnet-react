import { useEffect, useRef } from 'react';

/// Chama `task` agora e a cada `intervalMs`, enquanto `enabled` for true.
/// A tela fica "viva": a análise aparece sozinha quando o Worker a cria.
/// Mudar `restartKey` (ex.: o filtro) dispara uma chamada imediata e reinicia o intervalo.
export function usePolling(
  task: () => void | Promise<void>,
  intervalMs: number,
  enabled = true,
  restartKey?: unknown,
) {
  const taskRef = useRef(task);
  taskRef.current = task;

  useEffect(() => {
    if (!enabled) return;
    void taskRef.current();
    const id = window.setInterval(() => void taskRef.current(), intervalMs);
    return () => window.clearInterval(id);
  }, [intervalMs, enabled, restartKey]);
}
