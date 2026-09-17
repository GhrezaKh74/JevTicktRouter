import Accordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Typography from '@mui/material/Typography';
import Stack from '@mui/material/Stack';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import type { TriageTicketResponse } from '../../api/types';

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
  const redacted = result.jev.stateSummary.redacted;

  return (
    <Box component="section" aria-label="Developer details">
      <Accordion disableGutters elevation={0} variant="outlined">
        <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="developer-details-content">
          <Typography variant="subtitle2">Developer details</Typography>
        </AccordionSummary>

        <AccordionDetails id="developer-details-content">
          <Stack spacing={2}>
            {redacted && (
              <Alert severity="warning" variant="outlined">
                This ticket was flagged as containing sensitive data, so the server redacted the
                ticket text before sending this response. The raw description is not available here.
              </Alert>
            )}

            <Section title="Jev structured response (sanitised)">
              <Stack direction="row" spacing={1} useFlexGap sx={{ mb: 1, flexWrap: 'wrap' }}>
                <Chip size="small" label={`mode: ${result.jev.mode}`} />
                <Chip size="small" label={`model: ${result.jev.model}`} />
                <Chip size="small" label={`latency: ${result.jev.latencyMs} ms`} />
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

            <Section title={`Deterministic rules applied (${result.appliedRules.length})`}>
              {result.appliedRules.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  No rule changed Jev&apos;s proposal. The model was confident and nothing required
                  escalation.
                </Typography>
              ) : (
                <Stack spacing={1.5} component="ul" sx={{ listStyle: 'none', pl: 0, m: 0 }}>
                  {result.appliedRules.map((rule) => (
                    <Box component="li" key={rule.id}>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {rule.id}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {rule.description}
                      </Typography>
                      <Typography variant="caption" color="secondary.main">
                        {rule.effect}
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              )}
            </Section>

            <Section title="Final API response">
              <CodeBlock value={result} />
            </Section>
          </Stack>
        </AccordionDetails>
      </Accordion>
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
      }}
    >
      <code>{JSON.stringify(value, null, 2)}</code>
    </Box>
  );
}
