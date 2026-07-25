import {
  Box,
  Card,
  CardContent,
  Chip,
  Container,
  Grid,
  Link,
  Stack,
  Typography,
} from '@mui/material'

const hlsBaseUrl =
  import.meta.env.VITE_HLS_BASE_URL ?? 'http://localhost:8888'
const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080'

const cameras = [
  { id: 'camera-1', name: 'Entrance', accent: '#44d7b6' },
  { id: 'camera-2', name: 'Loading Bay', accent: '#6fa8ff' },
  { id: 'camera-3', name: 'Parking', accent: '#f8c15c' },
  { id: 'camera-4', name: 'Warehouse', accent: '#e9799a' },
]

function App() {
  return (
    <Box component="main" className="app-shell">
      <Container maxWidth="lg">
        <Stack spacing={5}>
          <Box component="header">
            <Chip
              color="primary"
              label="Foundation ready"
              size="small"
              sx={{ mb: 2 }}
            />
            <Typography
              variant="h1"
              sx={{ fontSize: { xs: 40, md: 64 } }}
            >
              VMS Command Center
            </Typography>
            <Typography
              color="text.secondary"
              sx={{
                fontSize: { xs: 17, md: 20 },
                maxWidth: 720,
                mt: 2,
              }}
            >
              The repository, API, frontend, database, and local media pipeline
              are prepared. Operational workflows arrive in the approved gated
              steps.
            </Typography>
          </Box>

          <Box component="section" aria-labelledby="streams-heading">
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              sx={{
                justifyContent: 'space-between',
                alignItems: { xs: 'flex-start', sm: 'center' },
                gap: 2,
                mb: 2,
              }}
            >
              <Box>
                <Typography
                  id="streams-heading"
                  variant="h2"
                  sx={{ fontSize: 28 }}
                >
                  Generated camera feeds
                </Typography>
                <Typography color="text.secondary">
                  Four RTSP publishers are exposed as HLS by MediaMTX.
                </Typography>
              </Box>
              <Link href={`${apiBaseUrl}/health`} target="_blank" rel="noreferrer">
                API health
              </Link>
            </Stack>

            <Grid container spacing={2}>
              {cameras.map((camera) => (
                <Grid key={camera.id} size={{ xs: 12, sm: 6 }}>
                  <Card
                    variant="outlined"
                    sx={{
                      height: '100%',
                      borderColor: 'rgba(255,255,255,0.10)',
                      borderTop: `3px solid ${camera.accent}`,
                    }}
                  >
                    <CardContent>
                      <Stack
                        direction="row"
                        sx={{
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          gap: 2,
                        }}
                      >
                        <Box>
                          <Typography sx={{ fontWeight: 700 }}>
                            {camera.name}
                          </Typography>
                          <Typography variant="body2" color="text.secondary">
                            {camera.id}
                          </Typography>
                        </Box>
                        <Chip label="HLS configured" variant="outlined" />
                      </Stack>
                      <Link
                        href={`${hlsBaseUrl}/${camera.id}/index.m3u8`}
                        target="_blank"
                        rel="noreferrer"
                        sx={{
                          display: 'inline-block',
                          mt: 3,
                          wordBreak: 'break-all',
                        }}
                      >
                        {`${hlsBaseUrl}/${camera.id}/index.m3u8`}
                      </Link>
                    </CardContent>
                  </Card>
                </Grid>
              ))}
            </Grid>
          </Box>

          <Typography component="footer" color="text.secondary" variant="body2">
            Step 1 — Repository and media foundation
          </Typography>
        </Stack>
      </Container>
    </Box>
  )
}

export default App
