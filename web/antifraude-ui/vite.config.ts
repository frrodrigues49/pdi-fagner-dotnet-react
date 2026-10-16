import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// Em dev, o Vite faz o mesmo papel do nginx em produção: a tela chama /onboarding/* e
// /antifraude/* na própria origem e o proxy repassa para as APIs. Mesma origem = sem CORS.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/onboarding': {
        target: 'http://localhost:5003',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/onboarding/, ''),
      },
      '/antifraude': {
        target: 'http://localhost:5004',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/antifraude/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
