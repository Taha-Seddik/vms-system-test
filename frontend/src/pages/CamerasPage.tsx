import { useEffect, useState, type CSSProperties } from 'react'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Button,
  Grid,
  Link,
  Stack,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { AccessibleCamera } from '../types/auth'
import type { CameraConnectionTest } from '../types/auth'

const hlsBaseUrl =
  import.meta.env.VITE_HLS_BASE_URL ?? 'http://localhost:8888'

export function CamerasPage() {
  const { accessToken, user } = useAuth()
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [testMessage, setTestMessage] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    const load = async () => {
      try {
        const result = await apiRequest<AccessibleCamera[]>(
          '/api/cameras/accessible',
          { accessToken: accessToken ?? undefined },
        )
        if (active) {
          setCameras(result)
          setError(null)
        }
      } catch {
        if (active) {
          setError('Camera access could not be loaded.')
        }
      } finally {
        if (active) {
          setIsLoading(false)
        }
      }
    }
    void load()
    const interval = window.setInterval(() => void load(), 15000)
    return () => {
      active = false
      window.clearInterval(interval)
    }
  }, [accessToken])

  const testConnection = async (camera: AccessibleCamera) => {
    setTestMessage(`Testing ${camera.name}...`)
    try {
      const result = await apiRequest<CameraConnectionTest>(
        `/api/cameras/${camera.id}/test-connection`,
        {
          method: 'POST',
          accessToken: accessToken ?? undefined,
        },
      )
      setTestMessage(
        result.succeeded
          ? `${camera.name} is online (${result.resolution ?? 'video detected'}, ${result.elapsedMilliseconds} ms).`
          : `${camera.name} is offline: ${result.error ?? 'connection failed'}`,
      )
    } catch {
      setTestMessage(`${camera.name} could not be tested.`)
    }
  }

  return (
    <Stack spacing={4}>
      <Box>
        <Chip
          label={`${user?.role} workspace`}
          color="primary"
          size="small"
          sx={{ mb: 2 }}
        />
        <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 42 } }}>
          Accessible cameras
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 720 }}>
          {user?.role === 'Viewer'
            ? `Your account is assigned to ${user.assignedCameraIds.length} cameras. The API filters every response.`
            : 'Your role can access all four foundation cameras.'}
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {testMessage && (
        <Alert severity="info" onClose={() => setTestMessage(null)}>
          {testMessage}
        </Alert>
      )}
      {isLoading ? (
        <Box className="content-loader">
          <CircularProgress />
        </Box>
      ) : (
        <Grid container spacing={2}>
          {cameras.map((camera, index) => (
            <Grid key={camera.id} size={{ xs: 12, sm: 6 }}>
              <Card
                variant="outlined"
                className="camera-card"
                sx={{ '--camera-index': index } as CSSProperties}
              >
                <CardContent sx={{ p: 3 }}>
                  <Stack
                    direction="row"
                    sx={{
                      justifyContent: 'space-between',
                      alignItems: 'flex-start',
                      gap: 2,
                    }}
                  >
                    <Box>
                      <Typography variant="h5" sx={{ fontWeight: 750 }}>
                        {camera.name}
                      </Typography>
                      <Typography color="text.secondary">
                        {camera.location}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {camera.group?.name ?? 'Ungrouped'} ·{' '}
                        {camera.resolution ?? 'Resolution pending'} ·{' '}
                        {camera.framesPerSecond
                          ? `${camera.framesPerSecond} FPS`
                          : 'FPS pending'}
                      </Typography>
                    </Box>
                    <Stack spacing={1} sx={{ alignItems: 'flex-end' }}>
                      <Chip
                        label={camera.connectionStatus ?? 'Unknown'}
                        size="small"
                        color={
                          camera.connectionStatus === 'Online'
                            ? 'success'
                            : camera.connectionStatus === 'Offline'
                              ? 'error'
                              : 'default'
                        }
                      />
                      <Chip label={camera.id} size="small" variant="outlined" />
                    </Stack>
                  </Stack>
                  <Box className="stream-placeholder">
                    <span>HLS feed authorized</span>
                  </Box>
                  <Link
                    href={`${hlsBaseUrl}${camera.hlsUrl}`}
                    target="_blank"
                    rel="noreferrer"
                    sx={{ wordBreak: 'break-all' }}
                  >
                    {`${hlsBaseUrl}${camera.hlsUrl}`}
                  </Link>
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{ mt: 2, alignItems: { sm: 'center' } }}
                  >
                    <Typography
                      variant="caption"
                      color="text.secondary"
                      sx={{ flexGrow: 1 }}
                    >
                      Last heartbeat:{' '}
                      {camera.lastHeartbeatAt
                        ? new Date(camera.lastHeartbeatAt).toLocaleString()
                        : 'Not received yet'}
                    </Typography>
                    {user?.role !== 'Viewer' && (
                      <Button
                        size="small"
                        variant="outlined"
                        onClick={() => void testConnection(camera)}
                      >
                        Test connection
                      </Button>
                    )}
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}

      <Alert severity="info" variant="outlined">
        Live video playback and monitoring controls intentionally arrive in
        Step 5. This step proves authenticated access and assignment filtering.
      </Alert>
    </Stack>
  )
}
