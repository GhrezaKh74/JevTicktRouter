import type { en } from './en';

/**
 * Makes translation keys type-safe: `t('result.heading')` compiles, `t('result.headng')` does not.
 */
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation';
    resources: { translation: typeof en };
    returnNull: false;
  }
}
