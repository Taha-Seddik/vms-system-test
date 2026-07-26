import { useCallback, useEffect, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type {
  AccessibleCamera,
  CameraGroup,
} from '../types/auth'
import type { SystemEventType } from '../types/dashboard'
import type { GlobalSearchResult } from '../types/search'

const eventTypes: SystemEventType[] = [
  'CameraOffline',
  'MotionDetected',
  'RecordingStarted',
  'RecordingStopped',
  'StorageFull',
  'CameraReconnected',
  'UserLogin',
  'UserLogout',
  'RecordingFailure',
]

const statuses = [
  'Online',
  'Offline',
  'Disabled',
  'Recording',
  'NotRecording',
  'Completed',
  'Failed',
  'Open',
  'Closed',
  'Enabled',
]

export function SearchPage() {
  const { accessToken, user } = useAuth()
  const [result, setResult] = useState<GlobalSearchResult | null>(null)
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [groups, setGroups] = useState<CameraGroup[]>([])
  const [term, setTerm] = useState('')
  const [cameraId, setCameraId] = useState('')
  const [groupId, setGroupId] = useState('')
  const [status, setStatus] = useState('')
  const [eventType, setEventType] = useState<SystemEventType | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const search = useCallback(async () => {
    setLoading(true)
    try {
      const parameters = new URLSearchParams({ take: '20' })
      if (term.trim()) parameters.set('q', term.trim())
      if (cameraId) parameters.set('cameraId', cameraId)
      if (groupId) parameters.set('cameraGroupId', groupId)
      if (status) parameters.set('status', status)
      if (eventType) parameters.set('eventType', eventType)
      if (fromDate) parameters.set('from', `${fromDate}T00:00:00Z`)
      if (toDate) parameters.set('to', `${toDate}T23:59:59Z`)

      setResult(
        await apiRequest<GlobalSearchResult>(
          `/api/search?${parameters.toString()}`,
          { accessToken: accessToken ?? undefined },
        ),
      )
      setError(null)
    } catch {
      setError('Search could not be completed.')
    } finally {
      setLoading(false)
    }
  }, [
    accessToken,
    cameraId,
    eventType,
    fromDate,
    groupId,
    status,
    term,
    toDate,
  ])

  useEffect(() => {
    if (!accessToken) return
    void Promise.all([
      apiRequest<AccessibleCamera[]>('/api/cameras/accessible', {
        accessToken,
      }),
      apiRequest<CameraGroup[]>('/api/camera-groups', { accessToken }),
    ])
      .then(([cameraRows, groupRows]) => {
        setCameras(cameraRows)
        setGroups(groupRows)
      })
      .catch(() => setError('Search filters could not be loaded.'))
    const timer = window.setTimeout(() => void search(), 0)
    // Initial search intentionally runs once for the authenticated session.
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [accessToken])

  const clear = () => {
    setTerm('')
    setCameraId('')
    setGroupId('')
    setStatus('')
    setEventType('')
    setFromDate('')
    setToDate('')
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography className="page-eyebrow">Cross-resource discovery</Typography>
        <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 } }}>
          Search
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 0.8 }}>
          Search cameras, recordings, events
          {user?.role === 'Administrator' ? ', and users' : ''} from one place.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Card variant="outlined">
        <CardContent>
          <Stack spacing={1.5}>
            <TextField
              label="Search names, locations, and descriptions"
              value={term}
              onChange={(event) => setTerm(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') void search()
              }}
            />
            <Box className="search-filter-grid">
              <SearchSelect
                label="Camera"
                value={cameraId}
                onChange={setCameraId}
                options={cameras.map((camera) => ({
                  value: camera.id,
                  label: camera.name,
                }))}
              />
              <SearchSelect
                label="Camera group"
                value={groupId}
                onChange={setGroupId}
                options={groups.map((group) => ({
                  value: group.id,
                  label: group.name,
                }))}
              />
              <SearchSelect
                label="Status"
                value={status}
                onChange={setStatus}
                options={statuses.map((value) => ({ value, label: value }))}
              />
              <SearchSelect
                label="Event type"
                value={eventType}
                onChange={(value) =>
                  setEventType(value as SystemEventType | '')
                }
                options={eventTypes.map((value) => ({
                  value,
                  label: formatType(value),
                }))}
              />
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
            </Box>
            <Stack direction="row" spacing={1}>
              <Button
                variant="contained"
                disabled={loading}
                onClick={() => void search()}
              >
                {loading ? 'Searching…' : 'Search'}
              </Button>
              <Button onClick={clear}>Clear filters</Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      {result && (
        <Box className="search-results-grid">
          <SearchSection title="Cameras" count={result.cameras.length}>
            {result.cameras.map((camera) => (
              <ResultRow
                key={camera.id}
                title={camera.name}
                detail={`${camera.location} · ${camera.cameraGroupName ?? 'No group'}`}
                chips={[camera.status, camera.recordingStatus]}
              />
            ))}
          </SearchSection>
          <SearchSection title="Recordings" count={result.recordings.length}>
            {result.recordings.map((recording) => (
              <ResultRow
                key={recording.id}
                title={`${recording.cameraName} · ${recording.mode}`}
                detail={new Date(recording.startedAt).toLocaleString()}
                chips={[recording.status]}
              />
            ))}
          </SearchSection>
          <SearchSection title="Events" count={result.events.length}>
            {result.events.map((item) => (
              <ResultRow
                key={item.id}
                title={formatType(item.type)}
                detail={`${item.cameraName ?? 'System-wide'} · ${item.description}`}
                chips={[item.severity, item.status]}
              />
            ))}
          </SearchSection>
          {user?.role === 'Administrator' && (
            <SearchSection title="Users" count={result.users.length}>
              {result.users.map((item) => (
                <ResultRow
                  key={item.id}
                  title={item.displayName}
                  detail={`@${item.username}`}
                  chips={[item.role, item.isEnabled ? 'Enabled' : 'Disabled']}
                />
              ))}
            </SearchSection>
          )}
        </Box>
      )}
    </Stack>
  )
}

function SearchSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
}) {
  const id = `search-${label.toLowerCase().replace(' ', '-')}`
  return (
    <FormControl size="small">
      <InputLabel id={id}>{label}</InputLabel>
      <Select
        labelId={id}
        label={label}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <MenuItem value="">All {label.toLowerCase()}s</MenuItem>
        {options.map((option) => (
          <MenuItem key={option.value} value={option.value}>
            {option.label}
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  )
}

function SearchSection({
  title,
  count,
  children,
}: {
  title: string
  count: number
  children: React.ReactNode
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', mb: 1.5 }}
        >
          <Typography variant="h6" sx={{ fontWeight: 800 }}>
            {title}
          </Typography>
          <Chip label={count} size="small" />
        </Stack>
        {count === 0 ? (
          <Typography color="text.secondary" variant="body2">
            No matching {title.toLowerCase()}.
          </Typography>
        ) : (
          <Stack spacing={1}>{children}</Stack>
        )}
      </CardContent>
    </Card>
  )
}

function ResultRow({
  title,
  detail,
  chips,
}: {
  title: string
  detail: string
  chips: string[]
}) {
  return (
    <Box className="search-result-row">
      <Box sx={{ minWidth: 0 }}>
        <Typography sx={{ fontWeight: 750 }}>{title}</Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          className="preview-description"
        >
          {detail}
        </Typography>
      </Box>
      <Stack direction="row" spacing={0.5}>
        {chips.map((chip) => (
          <Chip key={chip} label={chip} size="small" variant="outlined" />
        ))}
      </Stack>
    </Box>
  )
}

function formatType(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}
