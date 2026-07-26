import { useEffect, useState, type CSSProperties } from 'react'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  Link,
  Stack,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { AccessibleCamera } from '../types/auth'

const hlsBaseUrl =
  import.meta.env.VITE_HLS_BASE_URL ?? 'http://localhost:8888'

export function CamerasPage() {
  const { accessToken, user } = useAuth()
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const load = async () => {
      try {
        const result = await apiRequest<AccessibleCamera[]>(
          '/api/cameras/accessible',
          { accessToken: accessToken ?? undefined },
        )
        setCameras(result)
      } catch {
        setError('Camera access could not be loaded.')
      } finally {
        setIsLoading(false)
      }
    }
    void load()
  }, [accessToken])

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
                    </Box>
                    <Chip label={camera.id} size="small" variant="outlined" />
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
