import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material'
import { apiRequest, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { HlsVideo } from '../components/HlsVideo'
import { resolveHlsSource } from '../media/hls'
import type { AccessibleCamera } from '../types/auth'
import type {
  Recording,
  RecordingCommand,
} from '../types/recording'

const layoutOptions = [1, 4, 9, 16] as const
type WallLayout = (typeof layoutOptions)[number]

export function CamerasPage() {
  const { accessToken, user } = useAuth()
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [recordings, setRecordings] = useState<Recording[]>([])
  const [layout, setLayout] = useState<WallLayout>(4)
  const [focusedCameraId, setFocusedCameraId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [busyCameraId, setBusyCameraId] = useState<string | null>(null)
  const canRecord = user?.role !== 'Viewer'

  const load = useCallback(async () => {
    try {
      const cameraResult = await apiRequest<AccessibleCamera[]>(
        '/api/cameras/accessible',
        { accessToken: accessToken ?? undefined },
      )
      setCameras(cameraResult)
      setFocusedCameraId((current) =>
        cameraResult.some((camera) => camera.id === current)
          ? current
          : (cameraResult[0]?.id ?? ''),
      )

      if (canRecord) {
        const recordingResult = await apiRequest<Recording[]>(
          '/api/recordings?take=12',
          { accessToken: accessToken ?? undefined },
        )
        setRecordings(recordingResult)
      }
      setError(null)
    } catch {
      setError('Live-monitoring data could not be refreshed.')
    } finally {
      setIsLoading(false)
    }
  }, [accessToken, canRecord])

  useEffect(() => {
    const initialLoad = window.setTimeout(() => void load(), 0)
    const interval = window.setInterval(() => void load(), 5000)
    return () => {
      window.clearTimeout(initialLoad)
      window.clearInterval(interval)
    }
  }, [load])

  const displayedCameras = useMemo(() => {
    if (layout === 1) {
      const focused = cameras.find((camera) => camera.id === focusedCameraId)
      return focused ? [focused] : cameras.slice(0, 1)
    }
    return cameras.slice(0, layout)
  }, [cameras, focusedCameraId, layout])

  const cells = Array.from(
    { length: layout },
    (_, index) => displayedCameras[index] ?? null,
  )

  const runRecordingCommand = async (
    camera: AccessibleCamera,
    action: 'manual' | 'continuous' | 'motion' | 'stop',
  ) => {
    setBusyCameraId(camera.id)
    setNotice(null)
    try {
      const path =
        action === 'stop'
          ? `/api/cameras/${camera.id}/recordings/stop`
          : action === 'motion'
            ? `/api/cameras/${camera.id}/motion/simulate`
            : `/api/cameras/${camera.id}/recordings/${action}/start`
      const response = await apiRequest<RecordingCommand>(path, {
        method: 'POST',
        accessToken: accessToken ?? undefined,
      })
      setNotice(`${camera.name}: ${response.message}`)
      await load()
    } catch (commandError) {
      setNotice(
        commandError instanceof ApiError
          ? commandError.message
          : `${camera.name}: recording command failed.`,
      )
    } finally {
      setBusyCameraId(null)
    }
  }

  if (isLoading) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3}>
      <Box className="live-monitor-header">
        <Box>
          <Typography className="page-eyebrow">
            {user?.role === 'Viewer'
              ? 'Assigned live cameras'
              : 'Security monitoring'}
          </Typography>
          <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 } }}>
            Live monitoring
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.8 }}>
            {user?.role === 'Viewer'
              ? `${cameras.length} assigned cameras returned by the protected API.`
              : 'Live HLS video with manual, continuous, and motion-triggered recording.'}
          </Typography>
        </Box>

        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={1.5}
          sx={{ alignItems: { sm: 'center' } }}
        >
          {layout === 1 && cameras.length > 0 && (
            <FormControl size="small" sx={{ minWidth: 190 }}>
              <InputLabel id="focused-camera-label">Camera</InputLabel>
              <Select
                labelId="focused-camera-label"
                label="Camera"
                value={focusedCameraId}
                onChange={(event) => setFocusedCameraId(event.target.value)}
              >
                {cameras.map((camera) => (
                  <MenuItem key={camera.id} value={camera.id}>
                    {camera.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}
          <ToggleButtonGroup
            exclusive
            size="small"
            value={layout}
            aria-label="Camera wall layout"
            onChange={(_, value: WallLayout | null) => {
              if (value) {
                setLayout(value)
              }
            }}
          >
            {layoutOptions.map((option) => (
              <ToggleButton
                key={option}
                value={option}
                aria-label={`${option} camera layout`}
              >
                {option}
              </ToggleButton>
            ))}
          </ToggleButtonGroup>
        </Stack>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {notice && (
        <Alert severity="info" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      <Box
        className={`live-wall live-wall-${layout}`}
        aria-label={`${layout} camera monitoring layout`}
      >
        {cells.map((camera, index) =>
          camera ? (
            <LiveCameraTile
              key={camera.id}
              camera={camera}
              accessToken={accessToken ?? ''}
              canRecord={canRecord}
              busy={busyCameraId === camera.id}
              onRecordingCommand={(action) =>
                void runRecordingCommand(camera, action)
              }
              onNotice={setNotice}
            />
          ) : (
            <Box className="live-wall-empty" key={`empty-${index}`}>
              <span>Empty camera slot</span>
            </Box>
          ),
        )}
      </Box>

      {canRecord && (
        <RecentRecordings recordings={recordings} />
      )}
    </Stack>
  )
}

function LiveCameraTile({
  camera,
  accessToken,
  canRecord,
  busy,
  onRecordingCommand,
  onNotice,
}: {
  camera: AccessibleCamera
  accessToken: string
  canRecord: boolean
  busy: boolean
  onRecordingCommand: (
    action: 'manual' | 'continuous' | 'motion' | 'stop',
  ) => void
  onNotice: (message: string) => void
}) {
  const tileRef = useRef<HTMLDivElement>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const [zoom, setZoom] = useState(1)
  const source = resolveHlsSource(camera.hlsUrl)
  const online =
    camera.isEnabled !== false && camera.connectionStatus === 'Online'
  const recording = camera.recordingStatus === 'Recording'

  const takeSnapshot = () => {
    const video = videoRef.current
    if (!video || video.videoWidth === 0) {
      onNotice(`${camera.name}: wait for live video before taking a snapshot.`)
      return
    }

    try {
      const canvas = document.createElement('canvas')
      canvas.width = video.videoWidth
      canvas.height = video.videoHeight
      const context = canvas.getContext('2d')
      context?.drawImage(video, 0, 0, canvas.width, canvas.height)
      canvas.toBlob((blob) => {
        if (!blob) {
          onNotice(`${camera.name}: snapshot could not be created.`)
          return
        }
        const url = URL.createObjectURL(blob)
        const anchor = document.createElement('a')
        anchor.href = url
        anchor.download = `${camera.id}-${Date.now()}.png`
        anchor.click()
        URL.revokeObjectURL(url)
        onNotice(`${camera.name}: snapshot downloaded.`)
      }, 'image/png')
    } catch {
      onNotice(`${camera.name}: browser blocked the snapshot.`)
    }
  }

  const enterFullscreen = () => {
    void tileRef.current?.requestFullscreen()
  }

  return (
    <Card
      ref={tileRef}
      className={`live-camera-tile ${recording ? 'is-recording' : ''}`}
      variant="outlined"
    >
      <Box className="live-video-frame">
        {online ? (
          <HlsVideo
            ref={videoRef}
            source={source}
            title={camera.name}
            zoom={zoom}
            accessToken={accessToken}
          />
        ) : (
          <Box className="live-video-offline">Stream unavailable</Box>
        )}
        <Stack className="live-video-status" direction="row" spacing={0.7}>
          <Chip
            label={camera.connectionStatus ?? 'Unknown'}
            size="small"
            color={online ? 'success' : 'error'}
          />
          {recording && <Chip label="REC" size="small" color="error" />}
        </Stack>
      </Box>

      <CardContent className="live-camera-details">
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', gap: 1 }}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography noWrap sx={{ fontWeight: 800 }}>
              {camera.name}
            </Typography>
            <Typography noWrap variant="caption" color="text.secondary">
              {camera.location} · {camera.resolution ?? 'Awaiting metadata'}
            </Typography>
          </Box>
          <Typography variant="caption" color="text.secondary">
            {zoom.toFixed(1)}×
          </Typography>
        </Stack>

        <Stack direction="row" spacing={0.7} sx={{ mt: 1.2, flexWrap: 'wrap' }}>
          <Button size="small" onClick={enterFullscreen}>
            Fullscreen
          </Button>
          <Button
            size="small"
            onClick={() =>
              setZoom((current) =>
                current === 1 ? 1.5 : current === 1.5 ? 2 : 1,
              )
            }
          >
            Zoom
          </Button>
          <Button size="small" onClick={takeSnapshot}>
            Snapshot
          </Button>
        </Stack>

        {canRecord && (
          <Stack
            direction="row"
            spacing={0.7}
            sx={{ mt: 1, flexWrap: 'wrap' }}
          >
            {recording ? (
              <Button
                size="small"
                color="error"
                variant="contained"
                disabled={busy}
                onClick={() => onRecordingCommand('stop')}
              >
                Stop recording
              </Button>
            ) : (
              <>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={busy || !online}
                  onClick={() => onRecordingCommand('manual')}
                >
                  Manual
                </Button>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={busy || !online}
                  onClick={() => onRecordingCommand('continuous')}
                >
                  Continuous
                </Button>
                <Button
                  size="small"
                  variant="outlined"
                  disabled={busy || !online}
                  onClick={() => onRecordingCommand('motion')}
                >
                  Simulate motion
                </Button>
              </>
            )}
          </Stack>
        )}
      </CardContent>
    </Card>
  )
}

function RecentRecordings({ recordings }: { recordings: Recording[] }) {
  return (
    <Card variant="outlined" className="recent-recordings-panel">
      <CardContent>
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', mb: 1.5 }}
        >
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
              Recent recording output
            </Typography>
            <Typography variant="caption" color="text.secondary">
              MP4 metadata from manual, continuous, and event workflows
            </Typography>
          </Box>
          <Chip label={recordings.length} size="small" variant="outlined" />
        </Stack>

        {recordings.length === 0 ? (
          <Box className="dashboard-empty">
            <Typography color="text.secondary" variant="body2">
              No recordings have been created yet.
            </Typography>
          </Box>
        ) : (
          <Box className="recording-output-grid">
            {recordings.map((recording) => (
              <Box className="recording-output-row" key={recording.id}>
                <Box sx={{ minWidth: 0 }}>
                  <Typography noWrap sx={{ fontWeight: 750 }}>
                    {recording.cameraName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {recording.mode} ·{' '}
                    {new Date(recording.startedAt).toLocaleTimeString()}
                  </Typography>
                </Box>
                <Stack direction="row" spacing={0.7}>
                  <Chip
                    label={recording.state}
                    size="small"
                    color={
                      recording.state === 'Completed'
                        ? 'success'
                        : recording.state === 'Failed'
                          ? 'error'
                          : 'warning'
                    }
                  />
                  <Typography variant="caption" color="text.secondary">
                    {formatRecordingSize(recording.fileSizeBytes)}
                  </Typography>
                </Stack>
              </Box>
            ))}
          </Box>
        )}
      </CardContent>
    </Card>
  )
}

function formatRecordingSize(bytes: number | null) {
  if (!bytes) {
    return '—'
  }
  return bytes >= 1024 * 1024
    ? `${(bytes / 1024 / 1024).toFixed(1)} MB`
    : `${Math.round(bytes / 1024)} KB`
}
