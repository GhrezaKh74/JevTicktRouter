import { useTranslation } from 'react-i18next';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import GitHubIcon from '@mui/icons-material/GitHub';
import HubIcon from '@mui/icons-material/Hub';
import type { HealthResponse } from '../api/types';
import { LanguageSwitcher } from './LanguageSwitcher';

/** Replace with the real repository URL when publishing. */
const GITHUB_URL = 'https://github.com/GhrezaKh74/JevTicktRouter';

interface AppHeaderProps {
  readonly health: HealthResponse | undefined;
  readonly isLoading: boolean;
}

/**
 * Top bar: the project name, a badge saying whether Jev is live or mocked, a language switcher, and
 * a link to the repo.
 */
export function AppHeader({ health, isLoading }: AppHeaderProps) {
  const { t } = useTranslation();

  return (
    <AppBar position="sticky" color="default" elevation={0}>
      <Toolbar sx={{ gap: 1.5, minHeight: { xs: 60, sm: 64 } }}>
        <HubIcon color="primary" aria-hidden />

        <Box sx={{ minWidth: 0 }}>
          {/* The product name is a proper noun: it stays in Latin script in every locale. */}
          <Typography variant="h1" component="h1" noWrap lang="en" dir="ltr">
            {t('app.name')}
          </Typography>
          <Typography variant="caption" color="text.secondary" noWrap component="p">
            {t('app.tagline')}
          </Typography>
        </Box>

        <Box sx={{ flexGrow: 1 }} />

        {isLoading ? (
          <Skeleton variant="rounded" width={104} height={28} data-testid="mode-badge-skeleton" />
        ) : (
          <ModeBadge mode={health?.jevMode} model={health?.model} />
        )}

        <LanguageSwitcher />

        <Tooltip title={t('header.github')} describeChild>
          <IconButton
            component="a"
            href={GITHUB_URL}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={t('header.github')}
            size="small"
          >
            <GitHubIcon />
          </IconButton>
        </Tooltip>
      </Toolbar>
    </AppBar>
  );
}

function ModeBadge({
  mode,
  model,
}: {
  readonly mode: 'Live' | 'Mock' | undefined;
  readonly model: string | undefined;
}) {
  const { t } = useTranslation();

  if (mode === undefined) {
    return (
      <Tooltip title={t('header.modeUnknownHint')} describeChild>
        <Chip label={t('header.modeUnknown')} color="error" variant="outlined" size="small" />
      </Tooltip>
    );
  }

  const isLive = mode === 'Live';

  return (
    <Tooltip
      title={
        isLive
          ? t('header.modeLiveHint', { model: model ?? 'jev-latest' })
          : t('header.modeMockHint')
      }
      describeChild
    >
      <Chip
        label={isLive ? t('header.modeLive') : t('header.modeMock')}
        color={isLive ? 'success' : 'warning'}
        variant={isLive ? 'filled' : 'outlined'}
        size="small"
      />
    </Tooltip>
  );
}
