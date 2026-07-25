import { createTheme } from '@mui/material/styles'

export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#44d7b6',
    },
    secondary: {
      main: '#6fa8ff',
    },
    background: {
      default: '#08131f',
      paper: '#102235',
    },
  },
  shape: {
    borderRadius: 12,
  },
  typography: {
    fontFamily:
      '"Inter", "Segoe UI", system-ui, -apple-system, BlinkMacSystemFont, sans-serif',
    h1: {
      fontWeight: 700,
      letterSpacing: '-0.035em',
    },
    h2: {
      fontWeight: 650,
    },
  },
})

