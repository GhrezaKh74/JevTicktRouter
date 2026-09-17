import { describe, expect, it } from 'vitest';
import { screen } from '@testing-library/react';
import { ProviderBadge } from './ProviderBadge';
import { renderWithProviders } from '../test/renderWithProviders';
import { en, fa } from '../i18n';
import { AI_PROVIDERS } from '../api/types';

describe('ProviderBadge', () => {
  it('names the cloud provider', () => {
    renderWithProviders(<ProviderBadge provider="Jev" model="jev-1.13.0" />);

    expect(screen.getByText(en.header.modeLive)).toBeInTheDocument();
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

  it('gives every provider a distinct label', () => {
    const labels = new Set([en.header.modeLive, en.header.modeLocal, en.header.modeMock]);

    expect(labels.size).toBe(AI_PROVIDERS.length);
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
