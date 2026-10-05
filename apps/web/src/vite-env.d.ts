/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base path (or absolute URL) the API is reached on. Defaults to /api, which the dev server proxies. */
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
