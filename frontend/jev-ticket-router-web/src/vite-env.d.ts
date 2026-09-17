/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base URL of the JevTicketRouter API. Empty in development, where Vite proxies `/api`. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
