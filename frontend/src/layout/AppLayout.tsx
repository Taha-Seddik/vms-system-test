import {
  AppBar,
  Box,
  Button,
  Chip,
  Container,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function AppLayout() {
  const { user, hasRole, logout } = useAuth()

  return (
    <Box className="app-shell">
      <AppBar
        position="sticky"
        color="transparent"
        elevation={0}
        sx={{ borderBottom: '1px solid rgba(255,255,255,.08)' }}
      >
        <Toolbar>
          <Container
            maxWidth="lg"
            disableGutters
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 2,
              minHeight: 72,
            }}
          >
            <Box sx={{ flexGrow: 1 }}>
              <Typography variant="h6" sx={{ fontWeight: 800 }}>
                VMS Command Center
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Secure operations workspace
              </Typography>
            </Box>

            <Stack
              component="nav"
              direction="row"
              spacing={1}
              aria-label="Primary navigation"
            >
              {!hasRole('Viewer') && (
                <Button
                  component={NavLink}
                  to="/command-center"
                  color="inherit"
                >
                  Command
                </Button>
              )}
              <Button component={NavLink} to="/cameras" color="inherit">
                Cameras
              </Button>
              {hasRole('Administrator') && (
                <>
                  <Button
                    component={NavLink}
                    to="/manage/cameras"
                    color="inherit"
                  >
                    Manage
                  </Button>
                  <Button component={NavLink} to="/activity" color="inherit">
                    Activity
                  </Button>
                </>
              )}
            </Stack>

            <Chip
              label={`${user?.displayName} · ${user?.role}`}
              color="primary"
              variant="outlined"
              sx={{ display: { xs: 'none', md: 'flex' } }}
            />
            <Button color="inherit" onClick={() => void logout()}>
              Sign out
            </Button>
          </Container>
        </Toolbar>
      </AppBar>

      <Container component="main" maxWidth="lg" sx={{ py: { xs: 4, md: 6 } }}>
        <Outlet />
      </Container>
    </Box>
  )
}
