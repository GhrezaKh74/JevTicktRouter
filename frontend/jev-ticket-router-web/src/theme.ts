import { createTheme, type Theme } from '@mui/material/styles';

/**
 * A dark theme tuned for a dense, single-screen operations dashboard: a near-black surface stack so
 * cards separate without borders, and a restrained accent so the priority and status chips carry the
 * colour signal instead of competing with the chrome.
 *
 * Built per direction: MUI needs `direction` on the theme to flip its own logical spacing, and the
 * Persian face is set first in the font stack so Persian text gets a proper Arabic-script font
 * rather than a fallback with the wrong metrics.
 */
export function createAppTheme(direction: 'ltr' | 'rtl' = 'ltr'): Theme {
  return createTheme({
    direction,
    palette: {
      mode: 'dark',
      primary: { main: '#7aa2f7' },
      secondary: { main: '#bb9af7' },
      success: { main: '#7bc96f' },
      warning: { main: '#e0af68' },
      error: { main: '#f7768e' },
      info: { main: '#7dcfff' },
      background: { default: '#0d1117', paper: '#151b23' },
      divider: 'rgba(255, 255, 255, 0.09)',
      text: { primary: '#e6edf3', secondary: '#9aa7b4' },
    },
    shape: { borderRadius: 10 },
    typography: {
      fontFamily: [
        'Vazirmatn',
        'Inter',
        '-apple-system',
        'BlinkMacSystemFont',
        'Segoe UI',
        'Roboto',
        'Helvetica Neue',
        'Arial',
        'sans-serif',
      ].join(','),
      h1: { fontSize: '1.35rem', fontWeight: 600 },
      h2: { fontSize: '1.1rem', fontWeight: 600 },
      h3: { fontSize: '0.95rem', fontWeight: 600 },
      subtitle2: { fontWeight: 600, letterSpacing: '0.02em' },
      button: { textTransform: 'none', fontWeight: 600 },
    },
    components: {
      MuiCard: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            border: '1px solid rgba(255, 255, 255, 0.07)',
          },
        },
      },
      MuiAppBar: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            borderBottom: '1px solid rgba(255, 255, 255, 0.07)',
          },
        },
      },
      MuiChip: { styleOverrides: { root: { fontWeight: 600 } } },
      MuiLinearProgress: { styleOverrides: { root: { height: 6, borderRadius: 3 } } },
      MuiAccordion: {
        styleOverrides: {
          root: {
            backgroundImage: 'none',
            '&::before': { display: 'none' },
          },
        },
      },
    },
  });
}

/** The default left-to-right theme, used where no direction is in play (tests, storybook). */
export const theme = createAppTheme('ltr');
