import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App';
import { LocaleProvider } from './providers/LocaleProvider';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { refetchOnWindowFocus: false, retry: 1 },
  },
});

const container = document.getElementById('root');

if (!container) {
  throw new Error('Root container #root was not found in index.html.');
}

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      {/* LocaleProvider owns the language, the direction, the Emotion cache, and the theme. */}
      <LocaleProvider>
        <App />
      </LocaleProvider>
    </QueryClientProvider>
  </StrictMode>,
);
