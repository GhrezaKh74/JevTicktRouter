/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Lets the dev server call the API on its own origin, so no CORS setup is needed while developing.
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5217',
        changeOrigin: true,
      },
    },
  },
  build: {
    rollupOptions: {
      output: {
        // MUI and the React runtime change far less often than app code, so giving them their own
        // chunks keeps the cached vendor bundle valid across ordinary deploys.
        manualChunks: (id: string) => {
          if (!id.includes('node_modules')) {
            return undefined;
          }

          if (id.includes('@mui') || id.includes('@emotion')) {
            return 'mui';
          }

          if (id.includes('react-dom') || id.includes('/react/') || id.includes('scheduler')) {
            return 'react';
          }

          return undefined;
        },
      },
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      exclude: ['src/main.tsx', 'src/test/**', '**/*.d.ts'],
    },
  },
});
