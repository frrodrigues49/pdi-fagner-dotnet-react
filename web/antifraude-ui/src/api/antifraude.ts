import { ApiError, request } from './http';
import type { Analysis, AnalysisStatus, LoginResponse } from './types';

const BASE = '/antifraude/api/v1';

export const antifraudeApi = {
  async login(username: string, password: string): Promise<LoginResponse> {
    try {
      return await request<LoginResponse>(
        `${BASE}/auth/login`,
        { method: 'POST', body: JSON.stringify({ username, password }) },
        { anonymous: true },
      );
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) throw new ApiError(401, ['Usuário ou senha inválidos.']);
      throw error;
    }
  },

  list: (status?: AnalysisStatus) =>
    request<Analysis[]>(`${BASE}/analyses${status ? `?status=${status}` : ''}`),

  get: (id: string) => request<Analysis>(`${BASE}/analyses/${id}`),

  approve: (id: string) => request<Analysis>(`${BASE}/analyses/${id}/approve`, { method: 'POST' }),

  reject: (id: string, reason: string) =>
    request<Analysis>(`${BASE}/analyses/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),
};
