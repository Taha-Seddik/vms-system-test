import { useState, type FormEvent } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Container,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const demoAccounts = [
  {
    role: 'Administrator',
    username: 'admin',
    password: 'Admin123!',
    detail: 'All cameras and activity',
  },
  {
    role: 'Operator',
    username: 'operator',
    password: 'Operator123!',
    detail: 'All operational cameras',
  },
  {
    role: 'Viewer',
    username: 'viewer',
    password: 'Viewer123!',
    detail: 'Assigned cameras 1 and 2',
  },
]

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [username, setUsername] = useState('viewer')
  const [password, setPassword] = useState('Viewer123!')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (user) {
    return <Navigate to="/" replace />
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(username, password)
      const destination =
        (location.state as { from?: { pathname?: string } } | null)?.from
          ?.pathname ?? '/'
      navigate(destination, { replace: true })
    } catch (reason) {
      setError(
        reason instanceof ApiError
          ? reason.message
          : 'The API is unavailable. Check that Docker is running.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Box className="login-shell">
      <Container maxWidth="lg">
        <Box className="login-grid">
          <Stack spacing={3} className="login-intro">
            <Chip label="Step 2 · Identity boundary" color="primary" />
            <Typography variant="h1" sx={{ fontSize: { xs: 42, md: 64 } }}>
              The right view for every role.
            </Typography>
            <Typography color="text.secondary" sx={{ fontSize: 19 }}>
              Sign in to a revocable server-side session. Viewer camera
              assignments are enforced by the API, so hiding a camera is never
              just a frontend decision.
            </Typography>
            <Box className="security-note">
              <Typography variant="overline" color="primary">
                Security model
              </Typography>
              <Typography>
                PBKDF2 password hashes · signed JWTs · persisted sessions ·
                role policies · login/logout activity
              </Typography>
            </Box>
          </Stack>

          <Card className="login-card">
            <CardContent sx={{ p: { xs: 3, md: 4 } }}>
              <Typography variant="h4" sx={{ fontWeight: 750 }}>
                Sign in
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>
                Use one of the seeded local assessment accounts.
              </Typography>

              <Stack component="form" spacing={2} onSubmit={submit}>
                {error && <Alert severity="error">{error}</Alert>}
                <TextField
                  label="Username"
                  autoComplete="username"
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  required
                />
                <TextField
                  label="Password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
                <Button
                  variant="contained"
                  size="large"
                  type="submit"
                  disabled={isSubmitting}
                >
                  {isSubmitting ? 'Signing in…' : 'Sign in securely'}
                </Button>
              </Stack>

              <Stack spacing={1} sx={{ mt: 4 }}>
                <Typography variant="overline" color="text.secondary">
                  Demo accounts
                </Typography>
                {demoAccounts.map((account) => (
                  <Button
                    key={account.role}
                    className="account-choice"
                    variant="outlined"
                    onClick={() => {
                      setUsername(account.username)
                      setPassword(account.password)
                    }}
                    sx={{ justifyContent: 'space-between' }}
                  >
                    <span>{account.role}</span>
                    <small>{account.detail}</small>
                  </Button>
                ))}
              </Stack>
            </CardContent>
          </Card>
        </Box>
      </Container>
    </Box>
  )
}
