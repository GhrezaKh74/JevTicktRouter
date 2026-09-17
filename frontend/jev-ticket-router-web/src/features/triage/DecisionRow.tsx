import { useTranslation } from 'react-i18next';
import Box from '@mui/material/Box';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import GavelIcon from '@mui/icons-material/Gavel';
import type { DecisionOrigin } from '../../api/types';

interface DecisionRowProps {
  readonly label: string;
  readonly value: string;
  readonly modelValue: string;
  readonly origin: DecisionOrigin;
  readonly wasOverridden: boolean;
  readonly color?: 'default' | 'primary' | 'success' | 'warning' | 'error' | 'info';
}

/**
 * One final decision, plus an explicit marker when a deterministic rule decided it.
 * Making that visible is the point of the whole panel: the rule engine is the authority, and the UI
 * should never let that be mistaken for a model output.
 */
export function DecisionRow({
  label,
  value,
  modelValue,
  origin,
  wasOverridden,
  color = 'default',
}: DecisionRowProps) {
  const { t } = useTranslation();

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 1.5,
        flexWrap: 'wrap',
      }}
    >
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>

      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
        <Chip label={value} color={color} size="small" />

        {origin === 'BusinessRule' ? (
          <Tooltip
            title={
              wasOverridden
                ? t('decision.ruleOverrodeHint', { value, modelValue })
                : t('decision.ruleConfirmedHint', { modelValue })
            }
            describeChild
          >
            <Chip
              icon={<GavelIcon />}
              label={t('decision.byRule')}
              size="small"
              color="secondary"
              variant={wasOverridden ? 'filled' : 'outlined'}
            />
          </Tooltip>
        ) : (
          <Tooltip title={t('decision.jevHint')} describeChild>
            <Chip label={t('decision.byJev')} size="small" variant="outlined" />
          </Tooltip>
        )}
      </Stack>
    </Box>
  );
}
