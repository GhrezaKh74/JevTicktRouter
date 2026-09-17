import { useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
  const health = useHealth();
  const triage = useTriageTicket();
  const [toast, setToast] = useState<string | null>(null);

  const handleSubmit = (values: TicketFormValues) => {
    triage.mutate(values, {
      onSuccess: (result) => {
        setToast(
          result.needsHumanReview.value
            ? t('toast.needsReview')
            : t('toast.routed', { team: t(`teams.${result.targetTeam.value}`) }),
        );
      },
      onError: () => {
        setToast(t('toast.failed'));
      },
    });
  };

  const error = triage.error;
  const apiError = error instanceof ApiError ? error : null;

  // Field-level problems are shown inline on the form; only non-validation failures take over the panel.
  const isValidationFailure = apiError?.validationErrors != null;
  const panelError = error && !isValidationFailure ? translateApiError(error, t) : null;

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
          {t('app.footer')}{' '}
          <Link href="/swagger" target="_blank" rel="noopener noreferrer" underline="hover">
            {t('app.apiDocs')}
          </Link>
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

/**
 * Turns a failure into a message in the reader's language.
 *
 * Transport failures are ours to describe, and `ApiError` carries a machine-readable reason for
 * them. Anything the server put in `detail` is shown as the server wrote it: it is generated
 * upstream and translating it here would mean guessing at its content.
 */
function translateApiError(error: Error, t: ReturnType<typeof useTranslation>['t']): string {
  if (error instanceof ApiError && error.reason === 'network') {
    return t('errors.unreachable');
  }

  if (error instanceof ApiError && error.reason === 'unexpected-status') {
    return t('errors.unexpected', { status: error.status });
  }

  return error.message;
}
