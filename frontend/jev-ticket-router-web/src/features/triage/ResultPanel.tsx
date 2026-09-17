import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardHeader from '@mui/material/CardHeader';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import LinearProgress from '@mui/material/LinearProgress';
import Skeleton from '@mui/material/Skeleton';
import Chip from '@mui/material/Chip';
import InsightsIcon from '@mui/icons-material/Insights';
import type { TicketPriority, TriageTicketResponse } from '../../api/types';
import { DecisionRow } from './DecisionRow';
import { ConfidenceMeter } from './ConfidenceMeter';
import { DeveloperDetails } from './DeveloperDetails';

const PRIORITY_COLORS: Record<TicketPriority, 'success' | 'info' | 'warning' | 'error'> = {
  Low: 'success',
  Medium: 'info',
  High: 'warning',
  Critical: 'error',
};

interface ResultPanelProps {
  readonly result: TriageTicketResponse | undefined;
  readonly isLoading: boolean;
  readonly error: string | null;
  readonly confidenceThreshold: number;
}

/** The right-hand column: the final routing decision, or the loading/empty/error state. */
export function ResultPanel({ result, isLoading, error, confidenceThreshold }: ResultPanelProps) {
  if (isLoading) {
    return <LoadingState />;
  }

  if (error !== null) {
    return <ErrorState message={error} />;
  }

  if (!result) {
    return <EmptyState />;
  }

  return <Decision result={result} confidenceThreshold={confidenceThreshold} />;
}

function Decision({
  result,
  confidenceThreshold,
}: {
  readonly result: TriageTicketResponse;
  readonly confidenceThreshold: number;
}) {
  const needsReview = result.needsHumanReview.value;
  const sensitive = result.containsSensitiveData.value;

  return (
    <Card component="section" aria-labelledby="result-heading" aria-live="polite">
      <CardHeader
        id="result-heading"
        title="Routing decision"
        subheader={`Ticket ${result.ticketId}`}
        slotProps={{ title: { variant: 'h2' }, subheader: { variant: 'caption' } }}
        action={
          <Chip
            size="small"
            label={result.jev.mode === 'Live' ? 'Live Jev' : 'Mock mode'}
            color={result.jev.mode === 'Live' ? 'success' : 'warning'}
            variant="outlined"
          />
        }
      />

      <CardContent>
        <Stack spacing={2.5}>
          <Alert severity={needsReview ? 'warning' : 'success'} variant="outlined">
            <AlertTitle>{needsReview ? 'Held for human review' : 'Ready to auto-route'}</AlertTitle>
            {result.routingSummary}
          </Alert>

          <Stack spacing={1.5}>
            <DecisionRow
              label="Category"
              value={result.category.value}
              modelValue={result.category.modelValue}
              origin={result.category.origin}
              wasOverridden={result.category.wasOverridden}
              color={result.category.value === 'SecurityConcern' ? 'error' : 'primary'}
            />
            <DecisionRow
              label="Target team"
              value={result.targetTeam.value}
              modelValue={result.targetTeam.modelValue}
              origin={result.targetTeam.origin}
              wasOverridden={result.targetTeam.wasOverridden}
              color="primary"
            />
            <DecisionRow
              label="Priority"
              value={result.priority.value}
              modelValue={result.priority.modelValue}
              origin={result.priority.origin}
              wasOverridden={result.priority.wasOverridden}
              color={PRIORITY_COLORS[result.priority.value]}
            />
            <DecisionRow
              label="Sensitive data"
              value={sensitive ? 'Detected' : 'None detected'}
              modelValue={result.containsSensitiveData.modelValue ? 'Detected' : 'None detected'}
              origin={result.containsSensitiveData.origin}
              wasOverridden={result.containsSensitiveData.wasOverridden}
              color={sensitive ? 'error' : 'default'}
            />
            <DecisionRow
              label="Human review"
              value={needsReview ? 'Required' : 'Not required'}
              modelValue={result.needsHumanReview.modelValue ? 'Required' : 'Not required'}
              origin={result.needsHumanReview.origin}
              wasOverridden={result.needsHumanReview.wasOverridden}
              color={needsReview ? 'warning' : 'default'}
            />
          </Stack>

          <Divider />

          <Box>
            <Typography variant="subtitle2" gutterBottom>
              Jev confidence
            </Typography>
            <Stack spacing={1.5}>
              <ConfidenceMeter
                label="Category"
                confidence={result.category.confidence}
                threshold={confidenceThreshold}
              />
              <ConfidenceMeter
                label="Target team"
                confidence={result.targetTeam.confidence}
                threshold={confidenceThreshold}
              />
              <ConfidenceMeter
                label="Priority"
                confidence={result.priority.confidence}
                threshold={confidenceThreshold}
              />
            </Stack>
          </Box>

          {sensitive && (
            <Alert severity="error" variant="outlined">
              Sensitive data was detected. The ticket text has been redacted from the structured
              logs and from the developer details below.
            </Alert>
          )}

          <DeveloperDetails result={result} />
        </Stack>
      </CardContent>
    </Card>
  );
}

function LoadingState() {
  return (
    <Card component="section" aria-busy="true" aria-live="polite">
      <CardHeader
        title="Analysing ticket…"
        subheader="Asking Jev five questions in a single batched call"
        slotProps={{ title: { variant: 'h2' }, subheader: { variant: 'caption' } }}
      />
      <CardContent>
        <LinearProgress aria-label="Analysing ticket" sx={{ mb: 2.5 }} />
        <Stack spacing={1.5}>
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} variant="rounded" height={28} />
          ))}
          <Skeleton variant="rounded" height={96} />
        </Stack>
      </CardContent>
    </Card>
  );
}

function ErrorState({ message }: { readonly message: string }) {
  return (
    <Card component="section" aria-live="assertive">
      <CardHeader title="Triage failed" slotProps={{ title: { variant: 'h2' } }} />
      <CardContent>
        <Alert severity="error" variant="outlined">
          <AlertTitle>Could not triage this ticket</AlertTitle>
          {message}
        </Alert>
      </CardContent>
    </Card>
  );
}

function EmptyState() {
  return (
    <Card
      component="section"
      sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: 320 }}
    >
      <CardContent>
        <Stack spacing={1.5} sx={{ alignItems: 'center', textAlign: 'center', maxWidth: 380 }}>
          <InsightsIcon sx={{ fontSize: 44, color: 'text.secondary' }} aria-hidden />
          <Typography variant="h2">No ticket analysed yet</Typography>
          <Typography variant="body2" color="text.secondary">
            Submit a ticket, or pick one of the samples. Jev answers five scoped questions in one
            call, then deterministic .NET rules decide the final routing.
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
