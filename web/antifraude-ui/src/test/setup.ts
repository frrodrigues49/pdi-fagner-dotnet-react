import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Sem `globals: true` no Vitest, a Testing Library não registra o cleanup sozinha:
// o componente de um teste continuaria montado no seguinte.
afterEach(() => cleanup());
