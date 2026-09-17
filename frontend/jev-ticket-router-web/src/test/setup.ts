import '@testing-library/jest-dom/vitest';
import { afterEach, beforeEach, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import { i18n } from '../i18n';

beforeEach(() => {
  window.localStorage.clear();
});

afterEach(async () => {
  cleanup();
  vi.restoreAllMocks();

  // Each test seeds its own locale; leaving i18next on the previous one leaks across files.
  await i18n.changeLanguage('en');
  document.documentElement.removeAttribute('dir');
  document.documentElement.removeAttribute('lang');
});
