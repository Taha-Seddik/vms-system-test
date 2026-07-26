import { useEffect, useState } from 'react'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Stack,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { AuthActivity } from '../types/auth'

export function ActivityPage() {
  const { accessToken } = useAuth()
  const [activity, setActivity] = useState<AuthActivity | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    apiRequest<AuthActivity>('/api/auth/activity', {
      accessToken: accessToken ?? undefined,
    })
      .then(setActivity)
      .catch(() => setError('User activity could not be loaded.'))
  }, [accessToken])

  return (
    <Stack spacing={4}>
      <Box>
        <Chip label="Administrator only" color="secondary" size="small" />
        <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 42 }, mt: 2 }}>
          Authentication activity
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Active sessions and the most recent login/logout events.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {!activity && !error ? (
        <Box className="content-loader">
          <CircularProgress />
        </Box>
      ) : (
        activity && (
          <>
            <Card className="metric-card">
              <CardContent>
                <Typography variant="overline" color="text.secondary">
                  Active sessions
                </Typography>
                <Typography variant="h2">{activity.activeSessions}</Typography>
                <Typography color="text.secondary">
                  Valid sessions active within the last five minutes
                </Typography>
              </CardContent>
            </Card>

            <Stack spacing={1.5}>
              {activity.recentEvents.map((event) => (
                <Card key={event.id} variant="outlined">
                  <CardContent>
                    <Stack
                      direction={{ xs: 'column', sm: 'row' }}
                      sx={{ justifyContent: 'space-between', gap: 1 }}
                    >
                      <Box>
                        <Chip
                          label={
                            event.type === 'UserLogin' ? 'Login' : 'Logout'
                          }
                          size="small"
                          color={
                            event.type === 'UserLogin' ? 'primary' : 'default'
                          }
                          sx={{ mb: 1 }}
                        />
                        <Typography>{event.description}</Typography>
                      </Box>
                      <Typography color="text.secondary" variant="body2">
                        {new Date(event.timestamp).toLocaleString()}
                      </Typography>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          </>
        )
      )}
    </Stack>
  )
}
