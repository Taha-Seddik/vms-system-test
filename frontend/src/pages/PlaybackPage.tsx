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
  Slider,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { apiRequest, apiRequestBlob } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { ProtectedImage } from '../components/ProtectedImage'
import type { AccessibleCamera } from '../types/auth'
import type {
  Recording,
  RecordingDetails,
  RecordingMode,
} from '../types/recording'

const speedOptions = [0.5, 1, 1.5, 2, 4]

export function PlaybackPage() {
  const { accessToken } = useAuth()
  const [recordings, setRecordings] = useState<Recording[]>([])
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [selectedId, setSelectedId] = useState('')
  const [cameraId, setCameraId] = useState('')
  const [mode, setMode] = useState<RecordingMode | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const query = useMemo(() => {
    const parameters = new URLSearchParams({
      state: 'Completed',
      take: '100',
    })
    if (cameraId) parameters.set('cameraId', cameraId)
    if (mode) parameters.set('mode', mode)
    if (fromDate) parameters.set('from', `${fromDate}T00:00:00Z`)
    if (toDate) parameters.set('to', `${toDate}T23:59:59Z`)
    return parameters.toString()
  }, [cameraId, fromDate, mode, toDate])

  const load = useCallback(async () => {
    try {
      const [recordingRows, cameraRows] = await Promise.all([
        apiRequest<Recording[]>(`/api/recordings?${query}`, {
          accessToken: accessToken ?? undefined,
        }),
        apiRequest<AccessibleCamera[]>('/api/cameras/accessible', {
          accessToken: accessToken ?? undefined,
        }),
      ])
      setRecordings(recordingRows)
      setCameras(cameraRows)
      setSelectedId((current) =>
        recordingRows.some((item) => item.id === current)
          ? current
          : (recordingRows[0]?.id ?? ''),
      )
      setError(null)
    } catch {
      setError('The recording library could not be loaded.')
    } finally {
      setIsLoading(false)
    }
  }, [accessToken, query])

  useEffect(() => {
    const initialLoad = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(initialLoad)
  }, [load])

  if (isLoading) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography className="page-eyebrow">Recorded evidence</Typography>
        <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 } }}>
          Playback
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 0.8 }}>
          Browse completed recordings, inspect keyframes, seek, capture,
          change speed, and download the original MP4.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Card variant="outlined">
        <CardContent>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={1.5}
            className="playback-filters"
          >
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="playback-camera-label">Camera</InputLabel>
              <Select
                labelId="playback-camera-label"
                value={cameraId}
                label="Camera"
                onChange={(event) => setCameraId(event.target.value)}
              >
                <MenuItem value="">All cameras</MenuItem>
                {cameras.map((camera) => (
                  <MenuItem key={camera.id} value={camera.id}>
                    {camera.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" sx={{ minWidth: 170 }}>
              <InputLabel id="playback-mode-label">Recording type</InputLabel>
              <Select
                labelId="playback-mode-label"
                value={mode}
                label="Recording type"
                onChange={(event) =>
                  setMode(event.target.value as RecordingMode | '')
                }
              >
                <MenuItem value="">All types</MenuItem>
                <MenuItem value="Manual">Manual</MenuItem>
                <MenuItem value="Continuous">Continuous</MenuItem>
                <MenuItem value="Event">Event / motion</MenuItem>
              </Select>
            </FormControl>
            <TextField
              label="From date"
              type="date"
              size="small"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="To date"
              type="date"
              size="small"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>
        </CardContent>
      </Card>

      {recordings.length === 0 ? (
        <Card variant="outlined">
          <CardContent>
            <Typography sx={{ fontWeight: 800 }}>
              No playable recordings
            </Typography>
            <Typography color="text.secondary">
              Create a recording or change the current filters.
            </Typography>
          </CardContent>
        </Card>
      ) : (
        <Box className="playback-workspace">
          <RecordingBrowser
            recordings={recordings}
            selectedId={selectedId}
            onSelect={setSelectedId}
          />
          {selectedId && (
            <RecordingPlayer
              key={selectedId}
              recordingId={selectedId}
              accessToken={accessToken!}
            />
          )}
        </Box>
      )}
    </Stack>
  )
}

function RecordingBrowser({
  recordings,
  selectedId,
  onSelect,
}: {
  recordings: Recording[]
  selectedId: string
  onSelect: (id: string) => void
}) {
  return (
    <Card variant="outlined" className="recording-browser">
      <CardContent>
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', mb: 1.5 }}
        >
          <Typography variant="h6" sx={{ fontWeight: 800 }}>
            Recordings
          </Typography>
          <Chip label={recordings.length} size="small" />
        </Stack>
        <Stack spacing={1}>
          {recordings.map((recording) => (
            <Button
              key={recording.id}
              className="recording-browser-row"
              variant={recording.id === selectedId ? 'contained' : 'outlined'}
              onClick={() => onSelect(recording.id)}
            >
              <span>
                <strong>{recording.cameraName}</strong>
                <small>
                  {recording.mode} ·{' '}
                  {new Date(recording.startedAt).toLocaleString()}
                </small>
              </span>
              <span>{formatDuration(recording.durationSeconds ?? 0)}</span>
            </Button>
          ))}
        </Stack>
      </CardContent>
    </Card>
  )
}

function RecordingPlayer({
  recordingId,
  accessToken,
}: {
  recordingId: string
  accessToken: string
}) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const [details, setDetails] = useState<RecordingDetails | null>(null)
  const [source, setSource] = useState<string | null>(null)
  const [currentTime, setCurrentTime] = useState(0)
  const [duration, setDuration] = useState(0)
  const [speed, setSpeed] = useState(1)
  const [isPlaying, setIsPlaying] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    let mediaUrl: string | null = null
    let active = true
    void Promise.all([
      apiRequest<RecordingDetails>(`/api/recordings/${recordingId}`, {
        accessToken,
      }),
      apiRequestBlob(`/api/recordings/${recordingId}/media`, { accessToken }),
    ])
      .then(([result, blob]) => {
        if (!active) return
        mediaUrl = URL.createObjectURL(blob)
        setDetails(result)
        setSource(mediaUrl)
        setNotice(null)
      })
      .catch(() => {
        if (active) setNotice('This recording could not be opened.')
      })

    return () => {
      active = false
      if (mediaUrl) URL.revokeObjectURL(mediaUrl)
    }
  }, [accessToken, recordingId])

  const seek = (value: number) => {
    if (videoRef.current) {
      videoRef.current.currentTime = value
      setCurrentTime(value)
    }
  }

  const togglePlayback = () => {
    const video = videoRef.current
    if (!video) return
    if (video.paused) {
      void video.play()
    } else {
      video.pause()
    }
  }

  const changeSpeed = (value: number) => {
    setSpeed(value)
    if (videoRef.current) videoRef.current.playbackRate = value
  }

  const takeSnapshot = () => {
    const video = videoRef.current
    if (!video || video.videoWidth === 0 || !details) {
      setNotice('Wait for the recording frame before taking a snapshot.')
      return
    }
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    canvas.getContext('2d')?.drawImage(video, 0, 0)
    canvas.toBlob((blob) => {
      if (!blob) return
      downloadBlob(
        blob,
        `${details.recording.cameraId}-${Math.floor(video.currentTime)}s.png`,
      )
      setNotice('Playback snapshot downloaded.')
    }, 'image/png')
  }

  const downloadRecording = async () => {
    if (!details) return
    try {
      const blob = await apiRequestBlob(
        `/api/recordings/${recordingId}/download`,
        { accessToken },
      )
      downloadBlob(
        blob,
        `${details.recording.cameraId}-${new Date(
          details.recording.startedAt,
        ).toISOString().replaceAll(':', '-')}.mp4`,
      )
      setNotice('Recording download started.')
    } catch {
      setNotice('The recording could not be downloaded.')
    }
  }

  if (!details || !source) {
    return (
      <Card variant="outlined" className="recording-player-card">
        <CardContent className="content-loader">
          {notice ? (
            <Alert severity="error">{notice}</Alert>
          ) : (
            <CircularProgress />
          )}
        </CardContent>
      </Card>
    )
  }

  const mediaDuration = duration || details.recording.durationSeconds || 0

  return (
    <Card variant="outlined" className="recording-player-card">
      <CardContent>
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          sx={{ justifyContent: 'space-between', gap: 1, mb: 1.5 }}
        >
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 850 }}>
              {details.recording.cameraName}
            </Typography>
            <Typography color="text.secondary" variant="body2">
              {details.recording.mode} recording ·{' '}
              {new Date(details.recording.startedAt).toLocaleString()}
            </Typography>
          </Box>
          <Chip
            color={details.recording.triggerEventId ? 'warning' : 'primary'}
            label={
              details.recording.triggerEventId ? 'Event linked' : 'Recorded'
            }
          />
        </Stack>

        {notice && (
          <Alert severity="info" sx={{ mb: 1.5 }}>
            {notice}
          </Alert>
        )}

        <video
          ref={videoRef}
          className="playback-video"
          src={source}
          controls
          playsInline
          onLoadedMetadata={(event) =>
            setDuration(event.currentTarget.duration)
          }
          onTimeUpdate={(event) =>
            setCurrentTime(event.currentTarget.currentTime)
          }
          onPlay={() => setIsPlaying(true)}
          onPause={() => setIsPlaying(false)}
        />

        <Box className="playback-timeline">
          <Slider
            aria-label="Recording timeline"
            min={0}
            max={Math.max(mediaDuration, 0.1)}
            step={0.1}
            value={Math.min(currentTime, Math.max(mediaDuration, 0.1))}
            onChange={(_, value) => seek(value as number)}
          />
          <Typography variant="caption" color="text.secondary">
            {formatDuration(currentTime)} / {formatDuration(mediaDuration)}
          </Typography>
        </Box>

        <Stack direction="row" spacing={1} className="playback-controls">
          <Button variant="contained" onClick={togglePlayback}>
            {isPlaying ? 'Pause' : 'Play'}
          </Button>
          <Button onClick={() => seek(Math.max(0, currentTime - 10))}>
            Back 10s
          </Button>
          <Button
            onClick={() => seek(Math.min(mediaDuration, currentTime + 10))}
          >
            Forward 10s
          </Button>
          <FormControl size="small" sx={{ minWidth: 115 }}>
            <InputLabel id="playback-speed-label">Speed</InputLabel>
            <Select
              labelId="playback-speed-label"
              label="Speed"
              value={speed}
              onChange={(event) => changeSpeed(Number(event.target.value))}
            >
              {speedOptions.map((value) => (
                <MenuItem key={value} value={value}>
                  {value}×
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Button onClick={takeSnapshot}>Snapshot</Button>
          <Button onClick={() => void downloadRecording()}>Download MP4</Button>
        </Stack>

        <Box sx={{ mt: 3 }}>
          <Typography variant="h6" sx={{ fontWeight: 800 }}>
            Keyframe timeline
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Select a preview to jump directly to that recording time.
          </Typography>
          <Box className="keyframe-strip">
            {details.keyframes.map((keyframe) => (
              <Button
                key={keyframe.id}
                className="keyframe-button"
                onClick={() => seek(keyframe.timestampSeconds)}
                aria-label={`Seek to ${formatDuration(
                  keyframe.timestampSeconds,
                )}`}
              >
                <ProtectedImage
                  path={`/api/recordings/${recordingId}/keyframes/${keyframe.id}`}
                  accessToken={accessToken}
                  alt={`${details.recording.cameraName} at ${formatDuration(
                    keyframe.timestampSeconds,
                  )}`}
                />
                <span>{formatDuration(keyframe.timestampSeconds)}</span>
              </Button>
            ))}
          </Box>
        </Box>
      </CardContent>
    </Card>
  )
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 0)
}

function formatDuration(seconds: number) {
  const safeSeconds = Math.max(0, Math.floor(seconds))
  const minutes = Math.floor(safeSeconds / 60)
  const remainder = safeSeconds % 60
  return `${minutes}:${remainder.toString().padStart(2, '0')}`
}
