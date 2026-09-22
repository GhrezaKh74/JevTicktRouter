import { useTranslation } from 'react-i18next';
import Chip from '@mui/material/Chip';
import Tooltip from '@mui/material/Tooltip';
import CloudIcon from '@mui/icons-material/Cloud';
import DnsIcon from '@mui/icons-material/Dns';
import MemoryIcon from '@mui/icons-material/Memory';
import ScienceIcon from '@mui/icons-material/Science';
import type { AiProvider } from '../api/types';

interface ProviderBadgeProps {
  /** The active provider, or undefined when the API could not be reached. */
  readonly provider: AiProvider | undefined;
  /** The Jev model name, shown only in the Jev tooltip. */
  readonly model?: string | undefined;
  readonly size?: 'small' | 'medium';
  /** Hides the icon where space is tight, such as inside a card header. */
  readonly withIcon?: boolean;
}

/**
 * Says which engine is deciding.
 *
 * Prominent by design: "a model inside our network" and "a third-party cloud API" are very different
 * things to be sending ticket text to, and anyone looking at a routing decision should be able to
 * tell which one produced it without opening a panel.
 */
export function ProviderBadge({
  provider,
  model,
  size = 'small',
  withIcon = true,
}: ProviderBadgeProps) {
  const { t } = useTranslation();

  if (provider === undefined) {
    return (
      <Tooltip title={t('header.modeUnknownHint')} describeChild>
        <Chip label={t('header.modeUnknown')} color="error" variant="outlined" size={size} />
      </Tooltip>
    );
  }

  const presentation = {
    Jev: {
      label: t('header.modeLive'),
      hint: t('header.modeLiveHint', { model: model ?? 'jev-latest' }),
      color: 'success',
      variant: 'filled',
      icon: <CloudIcon />,
    },
    SelfHosted: {
      label: t('header.modeSelfHosted'),
      hint: t('header.modeSelfHostedHint'),
      color: 'secondary',
      variant: 'filled',
      icon: <MemoryIcon />,
    },
    Local: {
      label: t('header.modeLocal'),
      hint: t('header.modeLocalHint'),
      color: 'info',
      variant: 'filled',
      icon: <DnsIcon />,
    },
    Mock: {
      label: t('header.modeMock'),
      hint: t('header.modeMockHint'),
      color: 'warning',
      variant: 'outlined',
      icon: <ScienceIcon />,
    },
  } as const;

  const { label, hint, color, variant, icon } = presentation[provider];

  return (
    <Tooltip title={hint} describeChild>
      <Chip
        label={label}
        color={color}
        variant={variant}
        size={size}
        {...(withIcon ? { icon } : {})}
      />
    </Tooltip>
  );
}
