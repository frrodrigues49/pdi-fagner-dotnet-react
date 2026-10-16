export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly errors: string[],
  ) {
    super(errors[0] ?? `Erro ${status}`);
    this.name = 'ApiError';
  }
}

interface HttpConfig {
  getToken: () => string | null;
  onUnauthorized: () => void;
}

let config: HttpConfig = {
  getToken: () => null,
  onUnauthorized: () => {},
};

/// Chamado pelo AuthProvider: o cliente HTTP não conhece React, só recebe
/// "como pegar o token" e "o que fazer quando a sessão expirar".
export function configureHttp(next: HttpConfig) {
  config = next;
}

export interface RequestOptions {
  /// Não envia o JWT e não trata 401 como "sessão expirada" (login, API Onboarding).
  anonymous?: boolean;
}

export async function request<T>(url: string, init: RequestInit = {}, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

  const token = options.anonymous ? null : config.getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const response = await fetch(url, { ...init, headers });

  if (response.status === 401 && !options.anonymous) {
    config.onUnauthorized();
    throw new ApiError(401, ['Sua sessão expirou. Entre novamente.']);
  }

  if (!response.ok) {
    const body = await readJson<{ errors?: string[] }>(response);
    throw new ApiError(response.status, body?.errors?.length ? body.errors : [`Erro ${response.status}`]);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function readJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T;
  } catch {
    return null;
  }
}
