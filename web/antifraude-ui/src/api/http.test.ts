import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, configureHttp, request } from './http';

const fetchMock = vi.fn();

function respond(status: number, body?: unknown) {
  fetchMock.mockResolvedValueOnce(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    }),
  );
}

describe('request', () => {
  const onUnauthorized = vi.fn();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    configureHttp({ getToken: () => 'token-123', onUnauthorized });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
    onUnauthorized.mockReset();
  });

  it('envia o JWT e o Content-Type quando há body', async () => {
    respond(200, { ok: true });

    await request('/x', { method: 'POST', body: '{}' });

    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer token-123');
    expect(headers.get('Content-Type')).toBe('application/json');
  });

  it('não envia o JWT em chamadas anônimas', async () => {
    respond(200, {});

    await request('/x', {}, { anonymous: true });

    expect((fetchMock.mock.calls[0][1].headers as Headers).has('Authorization')).toBe(false);
  });

  it('401 encerra a sessão', async () => {
    respond(401);

    await expect(request('/x')).rejects.toMatchObject({ status: 401 });
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('401 anônimo (ex.: senha errada no login) NÃO encerra a sessão', async () => {
    respond(401);

    await expect(request('/x', {}, { anonymous: true })).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('repassa os erros de validação da API', async () => {
    respond(400, { errors: ['TaxId must be a valid CPF.', 'Phone must have 10 or 11 digits.'] });

    const error = await request('/x').catch((e: ApiError) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).errors).toHaveLength(2);
    expect((error as ApiError).message).toBe('TaxId must be a valid CPF.');
  });

  it('204 devolve undefined', async () => {
    respond(204);

    await expect(request('/x')).resolves.toBeUndefined();
  });
});
