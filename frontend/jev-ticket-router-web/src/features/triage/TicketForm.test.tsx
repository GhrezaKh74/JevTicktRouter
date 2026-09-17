import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TicketForm } from './TicketForm';
import { renderWithProviders } from '../../test/renderWithProviders';
import { demoTickets } from './demoTickets';
import { demoLabel } from '../../test/i18nHelpers';

describe('TicketForm', () => {
  it('renders accessible, labelled fields', () => {
    renderWithProviders(<TicketForm onSubmit={vi.fn()} isSubmitting={false} />);

    expect(screen.getByRole('textbox', { name: /title/i })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /description/i })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /requester role/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /triage ticket/i })).toBeInTheDocument();
  });

  it('rejects a ticket that is too short and does not submit', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    renderWithProviders(<TicketForm onSubmit={onSubmit} isSubmitting={false} />);

    await user.type(screen.getByRole('textbox', { name: /title/i }), 'ab');
    await user.type(screen.getByRole('textbox', { name: /description/i }), 'short');
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    expect(await screen.findByText('Title must be at least 3 characters.')).toBeInTheDocument();
    expect(screen.getByText('Description must be at least 10 characters.')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits a valid ticket', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    renderWithProviders(<TicketForm onSubmit={onSubmit} isSubmitting={false} />);

    await user.type(screen.getByRole('textbox', { name: /title/i }), 'Printer is offline');
    await user.type(
      screen.getByRole('textbox', { name: /description/i }),
      'The shared printer on the second floor will not come back online.',
    );
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });

    expect(onSubmit.mock.calls[0]?.[0]).toMatchObject({
      title: 'Printer is offline',
      requesterRole: 'BranchEmployee',
    });
  });

  it('accepts Persian text', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();

    renderWithProviders(<TicketForm onSubmit={onSubmit} isSubmitting={false} />);

    await user.type(screen.getByRole('textbox', { name: /title/i }), 'خطا در سامانه شعبه');
    await user.type(
      screen.getByRole('textbox', { name: /description/i }),
      'هنگام ثبت تراکنش با خطا مواجه می‌شوم و صفحه لود نمی‌شود.',
    );
    await user.click(screen.getByRole('button', { name: /triage ticket/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledTimes(1);
    });
  });

  it('offers exactly three one-click demo tickets', () => {
    renderWithProviders(<TicketForm onSubmit={vi.fn()} isSubmitting={false} />);

    for (const demo of demoTickets) {
      expect(screen.getByRole('button', { name: demoLabel(demo.id) })).toBeInTheDocument();
    }

    expect(demoTickets).toHaveLength(3);
  });

  it('fills the form from a demo ticket', async () => {
    const user = userEvent.setup();

    renderWithProviders(<TicketForm onSubmit={vi.fn()} isSubmitting={false} />);

    const persianDemo = demoTickets[0]!;
    await user.click(screen.getByRole('button', { name: demoLabel(persianDemo.id) }));

    await waitFor(() => {
      expect(screen.getByRole('textbox', { name: /title/i })).toHaveValue(persianDemo.values.title);
    });
  });

  it('disables submission while a triage is in flight', () => {
    renderWithProviders(<TicketForm onSubmit={vi.fn()} isSubmitting />);

    expect(screen.getByRole('button', { name: /analysing/i })).toBeDisabled();
  });

  it('shows validation errors returned by the server', () => {
    renderWithProviders(
      <TicketForm
        onSubmit={vi.fn()}
        isSubmitting={false}
        serverErrors={{ Title: ['The server rejected this title.'] }}
      />,
    );

    expect(screen.getByText('The server rejected this title.')).toBeInTheDocument();
  });

  it('clears the form on reset', async () => {
    const user = userEvent.setup();

    renderWithProviders(<TicketForm onSubmit={vi.fn()} isSubmitting={false} />);

    const title = screen.getByRole('textbox', { name: /title/i });
    await user.type(title, 'Something');
    await user.click(screen.getByRole('button', { name: /clear the form/i }));

    await waitFor(() => {
      expect(title).toHaveValue('');
    });
  });
});
