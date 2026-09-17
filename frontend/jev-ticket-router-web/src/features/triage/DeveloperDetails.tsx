import { useTranslation } from 'react-i18next';
import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import type { AppliedRule, TriageTicketResponse } from '../../api/types';
import { en } from '../../i18n';

/** Rule ids the UI has its own translation for. */
type KnownRuleId = keyof typeof en.rules;

function isKnownRule(id: string): id is KnownRuleId {
  return Object.hasOwn(en.rules, id);
}

interface DeveloperDetailsProps {
  readonly result: TriageTicketResponse;
}

/**
 * The collapsible panel a developer opens to see what actually happened: the sanitised Jev output,
 * the rules that fired, and the raw API response.
 *
 * The response the server sends is already redacted when sensitive data was detected, so there is no
 * unredacted copy on the client to leak here. The banner makes that explicit rather than silent.
 */
export function DeveloperDetails({ result }: DeveloperDetailsProps) {
  const { t } = useTranslation();
  const redacted = result.jev.stateSummary.redacted;

  return (
    <Box component="section" aria-label={t('devDetails.heading')}>
      <Accordion disableGutters elevation={0} variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="developer-details-content">
          <Typography variant="subtitle2">{t('devDetails.heading')}</Typography>
        </AccordionSummary>

        <AccordionDetails id="developer-details-content">
          <Stack spacing={2}>
            {redacted && (
              <Alert severity="warning" variant="outlined">
                {t('devDetails.redactedBanner')}
              </Alert>
            )}

            <Section title={t('devDetails.jevResponse')}>
              <Stack direction="row" spacing={1} useFlexGap sx={{ mb: 1, flexWrap: 'wrap' }}>
                <Chip
                  size="small"
                  color="secondary"
                  variant="outlined"
                  label={t('devDetails.provider', { value: result.jev.provider })}
                />
                <Chip size="small" label={t('devDetails.model', { value: result.jev.model })} />
                <Chip
                  size="small"
                  label={t('devDetails.latency', { value: result.jev.latencyMs })}
                />
              </Stack>
              <CodeBlock
                value={{
                  category: {
                    choice: result.category.modelValue,
                    confidence: result.category.confidence,
                  },
                  target_team: {
                    choice: result.targetTeam.modelValue,
                    confidence: result.targetTeam.confidence,
                  },
                  priority: {
                    score: result.jev.priorityScore,
                    level: result.priority.modelValue,
                    confidence: result.priority.confidence,
                  },
                  contains_sensitive_data: { noul: result.jev.sensitiveDataProbability },
                  needs_human_review: { noul: result.jev.humanReviewProbability },
                }}
              />
            </Section>

            <Section title={t('devDetails.rulesApplied', { count: result.appliedRules.length })}>
              {result.appliedRules.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  {t('devDetails.noRules')}
                </Typography>
              ) : (
                <Stack spacing={1.5} component="ul" sx={{ listStyle: 'none', pl: 0, m: 0 }}>
                  {result.appliedRules.map((rule) => (
                    <RuleItem key={rule.id} rule={rule} />
                  ))}
                </Stack>
              )}
            </Section>

            <Section title={t('devDetails.finalResponse')}>
              <CodeBlock value={result} />
            </Section>
          </Stack>
        </AccordionDetails>
      </Accordion>
    </Box>
  );
}

/**
 * One applied rule. The id and the effect are the server's audit record and stay exactly as sent —
 * the effect carries computed values and belongs in a log, not in prose. Only the description, which
 * is keyed by the stable rule id, is translated.
 */
function RuleItem({ rule }: { readonly rule: AppliedRule }) {
  const { t } = useTranslation();

  return (
    <Box component="li">
      <Typography variant="body2" sx={{ fontWeight: 600 }} dir="ltr">
        {rule.id}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {isKnownRule(rule.id) ? t(`rules.${rule.id}`) : rule.description}
      </Typography>
      <Typography variant="caption" color="secondary.main" dir="ltr" sx={{ display: 'block' }}>
        {rule.effect}
      </Typography>
    </Box>
  );
}

function Section({
  title,
  children,
}: {
  readonly title: string;
  readonly children: React.ReactNode;
}) {
  return (
    <Box>
      <Typography variant="subtitle2" gutterBottom>
        {title}
      </Typography>
      {children}
    </Box>
  );
}

function CodeBlock({ value }: { readonly value: unknown }) {
  return (
    <Box
      component="pre"
      dir="ltr"
      sx={{
        m: 0,
        p: 1.5,
        borderRadius: 1,
        bgcolor: 'rgba(0, 0, 0, 0.35)',
        border: '1px solid',
        borderColor: 'divider',
        overflowX: 'auto',
        maxHeight: 320,
        fontSize: '0.75rem',
        lineHeight: 1.6,
        textAlign: 'left',
      }}
    >
      <code>{JSON.stringify(value, null, 2)}</code>
    </Box>
  );
}
