import { useContext } from 'react';
import { LocaleContext } from './LocaleProvider';

/** Reads the current interface locale and the setter that changes it. */
export function useLocale() {
  const context = useContext(LocaleContext);

  if (!context) {
    throw new Error('useLocale must be used inside a LocaleProvider.');
  }

  return context;
}
