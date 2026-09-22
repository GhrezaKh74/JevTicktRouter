import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { ProviderBadge } from './ProviderBadge';
import { renderWithProviders } from '../test/renderWithProviders';
import { en, fa } from '../i18n';
import { AI_PROVIDERS } from '../api/types';

/** What each provider must be called in the UI, keyed so a new provider cannot be forgotten. */
const PROVIDER_LABELS: Record<(typeof AI_PROVIDERS)[number], string> = {
  Jev: en.header.modeLive,
  SelfHosted: en.header.modeSelfHosted,
  Local: en.header.modeLocal,
  Mock: en.header.modeMock,
};

describe('ProviderBadge', () => {
  it('names the cloud provider', () => {
    renderWithProviders(<ProviderBadge provider="Jev" model="jev-1.13.0" />);

    expect(screen.getByText(en.header.modeLive)).toBeInTheDocument();
  });

  it('names the self-hosted provider', () => {
    renderWithProviders(<ProviderBadge provider="SelfHosted" />);

    expect(screen.getByText(en.header.modeSelfHosted)).toBeInTheDocument();
  });

  it('names the local provider', () => {
    renderWithProviders(<ProviderBadge provider="Local" />);

    expect(screen.getByText(en.header.modeLocal)).toBeInTheDocument();
  });

  it('names mock mode', () => {
    renderWithProviders(<ProviderBadge provider="Mock" />);

    expect(screen.getByText(en.header.modeMock)).toBeInTheDocument();
  });

  it('says when the API could not be reached', () => {
    renderWithProviders(<ProviderBadge provider={undefined} />);

    expect(screen.getByText(en.header.modeUnknown)).toBeInTheDocument();
  });

  it.each(AI_PROVIDERS)('renders a label for the %s provider', (provider) => {
    renderWithProviders(<ProviderBadge provider={provider} />);

    // A provider added without a translation would render an empty chip; this catches that.
    expect(screen.getByText(PROVIDER_LABELS[provider])).toBeInTheDocument();
  });

  it('gives every provider its own distinct label', () => {
    const labels = new Set(AI_PROVIDERS.map((provider) => PROVIDER_LABELS[provider]));

    expect(labels.size).toBe(AI_PROVIDERS.length);
    expect([...labels].every((label) => label.length > 0)).toBe(true);
  });

  it('explains that local decisions stay inside the network', () => {
    renderWithProviders(<ProviderBadge provider="Local" />);

    // The reassurance is the point of the badge, so it must actually be stated.
    expect(en.header.modeLocalHint).toMatch(/leaves the organisation/i);
    expect(fa.header.modeLocalHint).toContain('خارج نمی‌شود');
  });

  it('translates the provider label', () => {
    renderWithProviders(<ProviderBadge provider="Local" />, { locale: 'fa' });

    expect(screen.getByText(fa.header.modeLocal)).toBeInTheDocument();
  });
});
