import { useState } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import Typography from '@mui/material/Typography';
import Link from '@mui/material/Link';
import { AppHeader } from './components/AppHeader';
import { TicketForm } from './features/triage/TicketForm';
import { ResultPanel } from './features/triage/ResultPanel';
import { useHealth, useTriageTicket } from './api/queries';
import { ApiError } from './api/client';
import type { TicketFormValues } from './features/triage/ticketSchema';

/** Fallback threshold used for the confidence meters until /api/health responds. */
const DEFAULT_CONFIDENCE_THRESHOLD = 0.75;

export default function App() {
  const health = useHealth();
  const triage = useTriageTicket();
  const [toast, setToast] = useState<string | null>(null);

  const handleSubmit = (values: TicketFormValues) => {
    triage.mutate(values, {
      onSuccess: (result) => {
        setToast(
          result.needsHumanReview.value
            ? 'Triaged. This ticket needs a human review.'
            : `Triaged. Routed to ${result.targetTeam.value}.`,
        );
      },
      onError: () => {
        setToast('Triage failed. See the details on the right.');
      },
    });
  };

  const error = triage.error;
  const apiError = error instanceof ApiError ? error : null;

  // Field-level problems are shown inline on the form; only non-validation failures take over the panel.
  const isValidationFailure = apiError?.validationErrors != null;
  const panelError = error && !isValidationFailure ? error.message : null;

  return (
    <Box sx={{ minHeight: '100dvh', display: 'flex', flexDirection: 'column' }}>
      <AppHeader health={health.data} isLoading={health.isLoading} />

      <Container maxWidth="xl" component="main" sx={{ py: { xs: 2, md: 3 }, flexGrow: 1 }}>
        <Box
          sx={{
            display: 'grid',
            gap: { xs: 2, md: 3 },
            alignItems: 'start',
            gridTemplateColumns: { xs: '1fr', md: 'minmax(340px, 5fr) 7fr' },
          }}
        >
          <TicketForm
            onSubmit={handleSubmit}
            isSubmitting={triage.isPending}
            serverErrors={apiError?.validationErrors ?? null}
          />

          <ResultPanel
            result={triage.data}
            isLoading={triage.isPending}
            error={panelError}
            confidenceThreshold={health.data?.minimumConfidence ?? DEFAULT_CONFIDENCE_THRESHOLD}
          />
        </Box>
      </Container>

      <Box component="footer" sx={{ px: 3, py: 2, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          Jev makes the structured calls; deterministic .NET rules make the final decision. Read the{' '}
          <Link href="/swagger" target="_blank" rel="noopener noreferrer" underline="hover">
            API documentation
          </Link>
          .
        </Typography>
      </Box>

      <Snackbar
        open={toast !== null}
        autoHideDuration={5000}
        onClose={() => {
          setToast(null);
        }}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity={triage.isError ? 'error' : 'success'}
          variant="filled"
          onClose={() => {
            setToast(null);
          }}
        >
          {toast}
        </Alert>
      </Snackbar>
    </Box>
  );
}
