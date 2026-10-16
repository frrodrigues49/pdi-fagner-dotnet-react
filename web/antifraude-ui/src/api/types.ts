export type AnalysisStatus = 'Pending' | 'Approved' | 'Rejected';

export interface Analysis {
  id: string;
  proposalId: string;
  status: AnalysisStatus;
  fullName: string;
  taxId: string;
  email: string;
  phone: string;
  birthDate: string;
  street: string;
  city: string;
  state: string;
  postalCode: string;
  motherName: string;
  rejectionReason: string | null;
  decidedBy: string | null;
  createdAt: string;
  decidedAt: string | null;
}

export type ProposalStatus = 'Draft' | 'Finalized' | 'Approved' | 'AccountsRequested' | 'Rejected';

export interface ProposalInput {
  fullName: string;
  taxId: string;
  email: string;
  phone: string;
  birthDate: string;
  street: string;
  city: string;
  state: string;
  postalCode: string;
  motherName: string;
}

export interface Proposal extends Partial<ProposalInput> {
  id: string;
  status: ProposalStatus;
  version: number;
  createdAt: string;
  finalizedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  accountsRequestedAt: string | null;
  rejectionReason: string | null;
  rejectedAt: string | null;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  username: string;
  displayName: string;
}
