import { LOCALE_TAGS, type Locale } from './config';

/**
 * Formats a 0-1 ratio as a whole percentage in the reader's locale, so Persian shows ۸۷٪ rather
 * than 87%.
 */
export function formatPercent(value: number, locale: Locale): string {
  return new Intl.NumberFormat(LOCALE_TAGS[locale], {
    style: 'percent',
    maximumFractionDigits: 0,
  }).format(value);
}

/** Formats an integer in the reader's locale, giving Persian its own digits. */
export function formatNumber(value: number, locale: Locale): string {
  return new Intl.NumberFormat(LOCALE_TAGS[locale]).format(value);
}
