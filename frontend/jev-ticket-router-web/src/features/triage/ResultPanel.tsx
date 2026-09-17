import { useTranslation } from 'react-i18next';
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
import InsightsIcon from '@mui/icons-material/Insights';
import type { TicketPriority, TriageTicketResponse } from '../../api/types';
import { DecisionRow } from './DecisionRow';
import { ConfidenceMeter } from './ConfidenceMeter';
import { DeveloperDetails } from './DeveloperDetails';
import { ProviderBadge } from '../../components/ProviderBadge';

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
  const { t } = useTranslation();

  const needsReview = result.needsHumanReview.value;
  const sensitive = result.containsSensitiveData.value;

  const team = t(`teams.${result.targetTeam.value}`);
  const priority = t(`priorities.${result.priority.value}`);

  // The routing sentence is composed here rather than taken from the server's `routingSummary`,
  // which is English prose. The server's version stays in the raw response below.
  const summary = needsReview
    ? t('result.summaryEscalated', { team, priority })
    : t('result.summaryAuto', { team, priority });

  return (
    <Card component="section" aria-labelledby="result-heading" aria-live="polite">
      <CardHeader
        id="result-heading"
        title={t('result.heading')}
        subheader={t('result.ticketId', { id: result.ticketId })}
        slotProps={{ title: { variant: 'h2' }, subheader: { variant: 'caption' } }}
        action={
          <ProviderBadge provider={result.jev.provider} model={result.jev.model} withIcon={false} />
        }
      />

      <CardContent>
        <Stack spacing={2.5}>
          <Alert severity={needsReview ? 'warning' : 'success'} variant="outlined">
            <AlertTitle>{needsReview ? t('result.escalated') : t('result.autoRouted')}</AlertTitle>
            {summary}
          </Alert>

          <Stack spacing={1.5}>
            <DecisionRow
              label={t('result.category')}
              value={t(`categories.${result.category.value}`)}
              modelValue={t(`categories.${result.category.modelValue}`)}
              origin={result.category.origin}
              wasOverridden={result.category.wasOverridden}
              color={result.category.value === 'SecurityConcern' ? 'error' : 'primary'}
            />
            <DecisionRow
              label={t('result.targetTeam')}
              value={team}
              modelValue={t(`teams.${result.targetTeam.modelValue}`)}
              origin={result.targetTeam.origin}
              wasOverridden={result.targetTeam.wasOverridden}
              color="primary"
            />
            <DecisionRow
              label={t('result.priority')}
              value={priority}
              modelValue={t(`priorities.${result.priority.modelValue}`)}
              origin={result.priority.origin}
              wasOverridden={result.priority.wasOverridden}
              color={PRIORITY_COLORS[result.priority.value]}
            />
            <DecisionRow
              label={t('result.sensitiveData')}
              value={sensitive ? t('result.sensitiveDetected') : t('result.sensitiveNone')}
              modelValue={
                result.containsSensitiveData.modelValue
                  ? t('result.sensitiveDetected')
                  : t('result.sensitiveNone')
              }
              origin={result.containsSensitiveData.origin}
              wasOverridden={result.containsSensitiveData.wasOverridden}
              color={sensitive ? 'error' : 'default'}
            />
            <DecisionRow
              label={t('result.humanReview')}
              value={needsReview ? t('result.reviewRequired') : t('result.reviewNotRequired')}
              modelValue={
                result.needsHumanReview.modelValue
                  ? t('result.reviewRequired')
                  : t('result.reviewNotRequired')
              }
              origin={result.needsHumanReview.origin}
              wasOverridden={result.needsHumanReview.wasOverridden}
              color={needsReview ? 'warning' : 'default'}
            />
          </Stack>

          <Divider />

          <Box>
            <Typography variant="subtitle2" gutterBottom>
              {t('result.confidenceHeading')}
            </Typography>
            <Stack spacing={1.5}>
              <ConfidenceMeter
                label={t('result.category')}
                confidence={result.category.confidence}
                threshold={confidenceThreshold}
              />
              <ConfidenceMeter
                label={t('result.targetTeam')}
                confidence={result.targetTeam.confidence}
                threshold={confidenceThreshold}
              />
              <ConfidenceMeter
                label={t('result.priority')}
                confidence={result.priority.confidence}
                threshold={confidenceThreshold}
              />
            </Stack>
          </Box>

          {sensitive && (
            <Alert severity="error" variant="outlined">
              {t('result.sensitiveWarning')}
            </Alert>
          )}

          <DeveloperDetails result={result} />
        </Stack>
      </CardContent>
    </Card>
  );
}

function LoadingState() {
  const { t } = useTranslation();

  return (
    <Card component="section" aria-busy="true" aria-live="polite">
      <CardHeader
        title={t('result.loadingHeading')}
        subheader={t('result.loadingSubheading')}
        slotProps={{ title: { variant: 'h2' }, subheader: { variant: 'caption' } }}
      />
      <CardContent>
        <LinearProgress aria-label={t('result.loadingAria')} sx={{ mb: 2.5 }} />
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
  const { t } = useTranslation();

  return (
    <Card component="section" aria-live="assertive">
      <CardHeader title={t('result.errorHeading')} slotProps={{ title: { variant: 'h2' } }} />
      <CardContent>
        <Alert severity="error" variant="outlined">
          <AlertTitle>{t('result.errorTitle')}</AlertTitle>
          {message}
        </Alert>
      </CardContent>
    </Card>
  );
}

function EmptyState() {
  const { t } = useTranslation();

  return (
    <Card
      component="section"
      sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: 320 }}
    >
      <CardContent>
        <Stack spacing={1.5} sx={{ alignItems: 'center', textAlign: 'center', maxWidth: 380 }}>
          <InsightsIcon sx={{ fontSize: 44, color: 'text.secondary' }} aria-hidden />
          <Typography variant="h2">{t('result.emptyHeading')}</Typography>
          <Typography variant="body2" color="text.secondary">
            {t('result.emptyBody')}
          </Typography>
        </Stack>
      </CardContent>
    </Card>
  );
}
