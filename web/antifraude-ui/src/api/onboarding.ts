import { request } from './http';
import type { Proposal, ProposalInput } from './types';

// A API Onboarding é o "Postman" do organograma: aberta, sem login.
const BASE = '/onboarding/api/v1/proposals';
const anonymous = { anonymous: true };

export const onboardingApi = {
  create: (input: ProposalInput) =>
    request<Proposal>(BASE, { method: 'POST', body: JSON.stringify(input) }, anonymous),

  finalize: (id: string) => request<Proposal>(`${BASE}/${id}/finalize`, { method: 'POST' }, anonymous),

  get: (id: string) => request<Proposal>(`${BASE}/${id}`, {}, anonymous),
};
