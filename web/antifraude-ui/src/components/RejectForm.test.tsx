import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RejectForm } from './RejectForm';

describe('RejectForm', () => {
  it('só habilita o botão com pelo menos 3 caracteres', async () => {
    render(<RejectForm onSubmit={vi.fn()} />);
    const button = screen.getByRole('button', { name: 'Rejeitar' });

    expect(button).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Motivo da rejeição'), '  ab ');
    expect(button).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Motivo da rejeição'), 'c');
    expect(button).toBeEnabled();
  });

  it('envia o motivo sem espaços nas pontas', async () => {
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<RejectForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText('Motivo da rejeição'), '  Selfie divergente  ');
    await userEvent.click(screen.getByRole('button', { name: 'Rejeitar' }));

    expect(onSubmit).toHaveBeenCalledWith('Selfie divergente');
  });

  it('mostra o erro devolvido pela API', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new Error('Analysis cannot be rejected from Approved.'));
    render(<RejectForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText('Motivo da rejeição'), 'Documento ilegível');
    await userEvent.click(screen.getByRole('button', { name: 'Rejeitar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('cannot be rejected from Approved');
  });
});
