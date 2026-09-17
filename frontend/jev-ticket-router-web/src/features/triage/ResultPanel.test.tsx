import { describe, expect, it } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ResultPanel } from './ResultPanel';
import { renderWithProviders } from '../../test/renderWithProviders';
import { cleanResult, escalatedResult } from '../../test/fixtures';
import { en } from '../../i18n';

describe('ResultPanel', () => {
  it('shows the empty state before anything has been triaged', () => {
    renderWithProviders(
      <ResultPanel result={undefined} isLoading={false} error={null} confidenceThreshold={0.75} />,
    );

    expect(screen.getByText('No ticket analysed yet')).toBeInTheDocument();
  });

  it('shows a loading state while analysing', () => {
    renderWithProviders(
      <ResultPanel result={undefined} isLoading error={null} confidenceThreshold={0.75} />,
    );

    expect(screen.getByText('Analysing ticket…')).toBeInTheDocument();
    expect(screen.getByLabelText('Analysing ticket')).toBeInTheDocument();
  });

  it('shows a readable error state', () => {
    renderWithProviders(
      <ResultPanel
        result={undefined}
        isLoading={false}
        error="Could not reach the triage API."
        confidenceThreshold={0.75}
      />,
    );

    expect(screen.getByText('Could not triage this ticket')).toBeInTheDocument();
    expect(screen.getByText('Could not reach the triage API.')).toBeInTheDocument();
  });

  it('renders every decision field for a clean result', () => {
    renderWithProviders(
      <ResultPanel
        result={cleanResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    expect(screen.getByText(en.categories.AccessRequest)).toBeInTheDocument();
    expect(screen.getByText(en.teams.IdentityAccess)).toBeInTheDocument();
    expect(screen.getByText(en.priorities.Low)).toBeInTheDocument();
    expect(screen.getByText(en.result.sensitiveNone)).toBeInTheDocument();
    expect(screen.getByText(en.result.reviewNotRequired)).toBeInTheDocument();
    expect(screen.getByText(en.result.autoRouted)).toBeInTheDocument();
    // The summary sentence is composed in the UI from the decided fields, not taken from the server.
    expect(
      screen.getByText('Auto-routed to Identity & Access at Low priority.'),
    ).toBeInTheDocument();
  });

  it('shows each confidence score as a percentage', () => {
    renderWithProviders(
      <ResultPanel
        result={cleanResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    expect(screen.getByText('94%')).toBeInTheDocument();
    expect(screen.getByText('91%')).toBeInTheDocument();
    expect(screen.getByText('88%')).toBeInTheDocument();
  });

  it('marks a confidence below the threshold', () => {
    renderWithProviders(
      <ResultPanel
        result={escalatedResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    // Priority confidence is 0.61, under the 0.75 escalation threshold.
    expect(screen.getByText('61% · below threshold')).toBeInTheDocument();
  });

  it('says when human review is required and attributes it to a rule', () => {
    renderWithProviders(
      <ResultPanel
        result={escalatedResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    expect(screen.getByText(en.result.escalated)).toBeInTheDocument();
    expect(screen.getByText(en.result.reviewRequired)).toBeInTheDocument();
    expect(screen.getAllByText(en.decision.byRule).length).toBeGreaterThan(0);
  });

  it('attributes a rule-confirmed field to the rule, not to Jev', () => {
    // Jev also asked for a review here, so nothing was overridden, but the rule engine still had the
    // final say. The badge must say so rather than crediting the model.
    const confirmed = {
      ...escalatedResult,
      needsHumanReview: {
        value: true,
        modelValue: true,
        confidence: null,
        origin: 'BusinessRule',
        wasOverridden: false,
      },
    } as typeof escalatedResult;

    renderWithProviders(
      <ResultPanel result={confirmed} isLoading={false} error={null} confidenceThreshold={0.75} />,
    );

    expect(screen.getAllByText(en.decision.byRule).length).toBeGreaterThan(0);
  });

  it('warns that sensitive data was detected', () => {
    renderWithProviders(
      <ResultPanel
        result={escalatedResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    expect(screen.getByText(en.result.sensitiveDetected)).toBeInTheDocument();
    expect(screen.getByText(/has been redacted from the structured logs/i)).toBeInTheDocument();
  });

  it('never shows the raw description when sensitive data was detected', async () => {
    const user = userEvent.setup();

    renderWithProviders(
      <ResultPanel
        result={escalatedResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    await user.click(screen.getByRole('button', { name: /developer details/i }));

    const details = screen.getByLabelText('Developer details');

    expect(within(details).getByText(/the server redacted the ticket text/i)).toBeInTheDocument();
    expect(details.textContent).not.toContain('phishing');
    expect(details.textContent).toContain('[REDACTED: sensitive data detected]');
  });

  it('lists the deterministic rules that fired in the developer details', async () => {
    const user = userEvent.setup();

    renderWithProviders(
      <ResultPanel
        result={escalatedResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    await user.click(screen.getByRole('button', { name: /developer details/i }));

    expect(screen.getByText('Deterministic rules applied (2)')).toBeInTheDocument();
    expect(screen.getByText('SECURITY_OR_CRITICAL_ESCALATION')).toBeInTheDocument();
    expect(screen.getByText('SENSITIVE_DATA_REDACTION')).toBeInTheDocument();
  });

  it('says plainly when no rule changed the outcome', async () => {
    const user = userEvent.setup();

    renderWithProviders(
      <ResultPanel
        result={cleanResult}
        isLoading={false}
        error={null}
        confidenceThreshold={0.75}
      />,
    );

    await user.click(screen.getByRole('button', { name: /developer details/i }));

    expect(screen.getByText(en.devDetails.noRules)).toBeInTheDocument();
  });
});
