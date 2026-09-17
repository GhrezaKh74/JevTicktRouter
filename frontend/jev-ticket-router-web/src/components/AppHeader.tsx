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

/** Replace with the real repository URL when publishing. */
const GITHUB_URL = 'https://github.com/GhrezaKh74/JevTicktRouter';

interface AppHeaderProps {
  readonly health: HealthResponse | undefined;
  readonly isLoading: boolean;
}

/**
 * Top bar: the project name, a badge saying whether Jev is live or mocked, and a link to the repo.
 */
export function AppHeader({ health, isLoading }: AppHeaderProps) {
  return (
    <AppBar position="sticky" color="default" elevation={0}>
      <Toolbar sx={{ gap: 1.5, minHeight: { xs: 60, sm: 64 } }}>
        <HubIcon color="primary" aria-hidden />

        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h1" component="h1" noWrap>
            JevTicketRouter
          </Typography>
          <Typography variant="caption" color="text.secondary" noWrap component="p">
            Structured ticket triage with TypeSafe Jev
          </Typography>
        </Box>

        <Box sx={{ flexGrow: 1 }} />

        {isLoading ? (
          <Skeleton variant="rounded" width={104} height={28} data-testid="mode-badge-skeleton" />
        ) : (
          <ModeBadge mode={health?.jevMode} model={health?.model} />
        )}

        <Tooltip title="View the source on GitHub" describeChild>
          <IconButton
            component="a"
            href={GITHUB_URL}
            target="_blank"
            rel="noopener noreferrer"
            aria-label="View the source on GitHub"
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
  if (mode === undefined) {
    return (
      <Tooltip title="The API could not be reached, so its mode is unknown." describeChild>
        <Chip label="API offline" color="error" variant="outlined" size="small" />
      </Tooltip>
    );
  }

  const isLive = mode === 'Live';

  return (
    <Tooltip
      title={
        isLive
          ? `Calling the TypeSafe API with model ${model ?? 'jev-latest'}.`
          : 'No TYPESAFE_API_KEY is configured, so triage returns deterministic sample answers.'
      }
      describeChild
    >
      <Chip
        label={isLive ? 'Live Jev' : 'Mock mode'}
        color={isLive ? 'success' : 'warning'}
        variant={isLive ? 'filled' : 'outlined'}
        size="small"
      />
    </Tooltip>
  );
}
