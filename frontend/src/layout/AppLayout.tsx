import { useState } from 'react'
import {
  AppBar,
  Box,
  Button,
  Chip,
  Container,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const drawerWidth = 280

type NavigationIcon = 'command' | 'cameras' | 'manage' | 'activity'

interface NavigationItem {
  label: string
  description: string
  to: string
  icon: NavigationIcon
}

export function AppLayout() {
  const { user, hasRole, logout } = useAuth()
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false)

  const workspaceItems: NavigationItem[] = hasRole('Viewer')
    ? [
        {
          label: 'My cameras',
          description: 'Assigned camera access',
          to: '/cameras',
          icon: 'cameras',
        },
      ]
    : [
        {
          label: 'Command center',
          description: 'Operational overview',
          to: '/command-center',
          icon: 'command',
        },
        {
          label: 'Cameras',
          description: 'Camera health and access',
          to: '/cameras',
          icon: 'cameras',
        },
      ]

  const administrationItems: NavigationItem[] = hasRole('Administrator')
    ? [
        {
          label: 'Camera management',
          description: 'Cameras and groups',
          to: '/manage/cameras',
          icon: 'manage',
        },
        {
          label: 'User activity',
          description: 'Sessions and sign-ins',
          to: '/activity',
          icon: 'activity',
        },
      ]
    : []

  const closeMobileNavigation = () => setMobileNavigationOpen(false)
  const navigation = (
    <Box className="side-navigation">
      <Box className="side-navigation-brand">
        <Box className="brand-symbol" aria-hidden="true">
          <span />
        </Box>
        <Box>
          <Typography className="brand-name">VMS Control</Typography>
          <Typography className="brand-caption">Security operations</Typography>
        </Box>
      </Box>

      <Box className="side-navigation-content">
        <NavigationSection
          label="Workspace"
          items={workspaceItems}
          onNavigate={closeMobileNavigation}
        />
        {administrationItems.length > 0 && (
          <NavigationSection
            label="Administration"
            items={administrationItems}
            onNavigate={closeMobileNavigation}
          />
        )}
      </Box>

      <Box className="side-navigation-footer">
        <Divider />
        <Stack direction="row" spacing={1.25} sx={{ alignItems: 'center' }}>
          <Box className="user-avatar" aria-hidden="true">
            {user?.displayName.charAt(0).toUpperCase()}
          </Box>
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography noWrap sx={{ fontSize: 14, fontWeight: 750 }}>
              {user?.displayName}
            </Typography>
            <Typography
              noWrap
              color="text.secondary"
              sx={{ fontSize: 11.5 }}
            >
              {user?.role}
            </Typography>
          </Box>
        </Stack>
        <Button
          fullWidth
          className="sign-out-button"
          onClick={() => void logout()}
        >
          Sign out
        </Button>
      </Box>
    </Box>
  )

  return (
    <Box className="app-shell">
      <AppBar className="mobile-app-bar" elevation={0}>
        <Toolbar>
          <IconButton
            edge="start"
            color="inherit"
            aria-label="Open navigation"
            onClick={() => setMobileNavigationOpen(true)}
          >
            <MenuIcon />
          </IconButton>
          <Box sx={{ ml: 1.5, flex: 1 }}>
            <Typography sx={{ fontWeight: 800 }}>VMS Control</Typography>
            <Typography color="text.secondary" variant="caption">
              Security operations
            </Typography>
          </Box>
          <Chip label={user?.role} size="small" color="primary" />
        </Toolbar>
      </AppBar>

      <Drawer
        variant="temporary"
        open={mobileNavigationOpen}
        onClose={closeMobileNavigation}
        ModalProps={{ keepMounted: true }}
        className="mobile-navigation-drawer"
      >
        {navigation}
      </Drawer>
      <Drawer variant="permanent" className="desktop-navigation-drawer" open>
        {navigation}
      </Drawer>

      <Box
        component="main"
        className="application-content"
        sx={{ ml: { md: `${drawerWidth}px` } }}
      >
        <Container maxWidth="xl" sx={{ py: { xs: 3, md: 5 } }}>
          <Outlet />
        </Container>
      </Box>
    </Box>
  )
}

function NavigationSection({
  label,
  items,
  onNavigate,
}: {
  label: string
  items: NavigationItem[]
  onNavigate: () => void
}) {
  return (
    <Box className="navigation-section">
      <Typography className="navigation-section-label">{label}</Typography>
      <List disablePadding>
        {items.map((item) => (
          <ListItemButton
            key={item.to}
            component={NavLink}
            to={item.to}
            onClick={onNavigate}
            className="side-navigation-link"
          >
            <Box className="navigation-icon">
              <NavigationGlyph name={item.icon} />
            </Box>
            <ListItemText
              primary={item.label}
              secondary={item.description}
              slotProps={{
                primary: { sx: { fontSize: 14, fontWeight: 720 } },
                secondary: { sx: { fontSize: 11.5 } },
              }}
            />
          </ListItemButton>
        ))}
      </List>
    </Box>
  )
}

function NavigationGlyph({ name }: { name: NavigationIcon }) {
  const paths: Record<NavigationIcon, React.ReactNode> = {
    command: (
      <>
        <rect x="3" y="3" width="7" height="7" rx="1.5" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" />
      </>
    ),
    cameras: (
      <>
        <rect x="3" y="6" width="14" height="12" rx="2" />
        <path d="m17 10 4-2v8l-4-2z" />
        <circle cx="10" cy="12" r="2.5" />
      </>
    ),
    manage: (
      <>
        <path d="M4 7h10M4 17h16" />
        <circle cx="17" cy="7" r="2.5" />
        <circle cx="8" cy="17" r="2.5" />
      </>
    ),
    activity: (
      <>
        <path d="M3 12h4l2.5-6 4.5 12 2.5-6H21" />
      </>
    ),
  }

  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      {paths[name]}
    </svg>
  )
}

function MenuIcon() {
  return (
    <svg
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  )
}
