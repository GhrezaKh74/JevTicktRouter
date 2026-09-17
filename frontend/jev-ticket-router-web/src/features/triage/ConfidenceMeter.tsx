import Box from '@mui/material/Box';
import LinearProgress from '@mui/material/LinearProgress';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';

interface ConfidenceMeterProps {
  readonly label: string;
  /** 0-1, or null when the underlying question type reports no confidence. */
  readonly confidence: number | null;
  /** Below this, the deterministic rules escalate the ticket. */
  readonly threshold: number;
}

/**
 * Shows one confidence score against the escalation threshold. Colour is not the only signal: the
 * numeric value and the "below threshold" wording carry the same information for anyone who cannot
 * distinguish the colours.
 */
export function ConfidenceMeter({ label, confidence, threshold }: ConfidenceMeterProps) {
  if (confidence === null) {
    return (
      <Box>
        <Row label={label} value="not reported" />
        <Tooltip
          title="Yes/no (noul) answers return a probability but no confidence value."
          describeChild
        >
          <LinearProgress
            variant="determinate"
            value={0}
            aria-label={`${label}: no confidence reported`}
          />
        </Tooltip>
      </Box>
    );
  }

  const percent = Math.round(confidence * 100);
  const isBelowThreshold = confidence < threshold;

  return (
    <Box>
      <Row
        label={label}
        value={`${percent}%${isBelowThreshold ? ' · below threshold' : ''}`}
        emphasis={isBelowThreshold}
      />
      <Tooltip
        title={`Jev reported ${percent}% confidence. The escalation threshold is ${Math.round(
          threshold * 100,
        )}%.`}
        describeChild
      >
        <LinearProgress
          variant="determinate"
          value={percent}
          color={isBelowThreshold ? 'warning' : 'success'}
          aria-label={`${label} confidence`}
          aria-valuenow={percent}
          aria-valuemin={0}
          aria-valuemax={100}
        />
      </Tooltip>
    </Box>
  );
}

function Row({
  label,
  value,
  emphasis = false,
}: {
  readonly label: string;
  readonly value: string;
  readonly emphasis?: boolean;
}) {
  return (
    <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 0.5, gap: 1 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography
        variant="caption"
        color={emphasis ? 'warning.main' : 'text.primary'}
        sx={{ fontWeight: 600 }}
      >
        {value}
      </Typography>
    </Box>
  );
}
