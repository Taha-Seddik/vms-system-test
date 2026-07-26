import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
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
import { connectCommandCenter } from '../realtime/commandCenterConnection'
import type { AccessibleCamera } from '../types/auth'
import type {
  EventSeverity,
  EventStatus,
  RealtimeStatus,
  SystemEventType,
} from '../types/dashboard'
import type { EventSearchResult, SystemEvent } from '../types/event'

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
const severities: EventSeverity[] = ['Information', 'Warning', 'Critical']
const statuses: EventStatus[] = ['Open', 'Closed']
const pollIntervalMilliseconds = 30_000

export function EventsPage() {
  const { accessToken } = useAuth()
  const [result, setResult] = useState<EventSearchResult | null>(null)
  const [cameras, setCameras] = useState<AccessibleCamera[]>([])
  const [selected, setSelected] = useState<SystemEvent | null>(null)
  const [cameraId, setCameraId] = useState('')
  const [type, setType] = useState<SystemEventType | ''>('')
  const [severity, setSeverity] = useState<EventSeverity | ''>('')
  const [status, setStatus] = useState<EventStatus | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [realtimeStatus, setRealtimeStatus] =
    useState<RealtimeStatus>('Connecting')

  const query = useMemo(() => {
    const parameters = new URLSearchParams({ take: '200' })
    if (cameraId) parameters.set('cameraId', cameraId)
    if (type) parameters.set('type', type)
    if (severity) parameters.set('severity', severity)
    if (status) parameters.set('status', status)
    if (fromDate) parameters.set('from', `${fromDate}T00:00:00Z`)
    if (toDate) parameters.set('to', `${toDate}T23:59:59Z`)
    return parameters.toString()
  }, [cameraId, fromDate, severity, status, toDate, type])

  const load = useCallback(async () => {
    try {
      const events = await apiRequest<EventSearchResult>(
        `/api/events?${query}`,
        { accessToken: accessToken ?? undefined },
      )
      setResult(events)
      setError(null)
    } catch {
      setError('Events could not be refreshed.')
    }
  }, [accessToken, query])

  useEffect(() => {
    if (!accessToken) return
    void apiRequest<AccessibleCamera[]>('/api/cameras/accessible', {
      accessToken,
    })
      .then(setCameras)
      .catch(() => setError('Camera filter options could not be loaded.'))
  }, [accessToken])

  useEffect(() => {
    let active = true
    const refresh = () => {
      if (active) void load()
    }
    const stopRealtime = accessToken
      ? connectCommandCenter({
          accessToken,
          onChanged: refresh,
          onStatusChanged: (value) => {
            if (active) setRealtimeStatus(value)
          },
        })
      : () => undefined

    refresh()
    const poller = window.setInterval(refresh, pollIntervalMilliseconds)
    return () => {
      active = false
      window.clearInterval(poller)
      stopRealtime()
    }
  }, [accessToken, load])

  const openDetails = async (item: SystemEvent) => {
    setSelected(item)
    try {
      setSelected(
        await apiRequest<SystemEvent>(`/api/events/${item.id}`, {
          accessToken: accessToken ?? undefined,
        }),
      )
    } catch {
      setError('Event details could not be loaded.')
    }
  }

  const closeEvent = async () => {
    if (!selected) return
    try {
      const closed = await apiRequest<SystemEvent>(
        `/api/events/${selected.id}/close`,
        {
          method: 'POST',
          accessToken: accessToken ?? undefined,
        },
      )
      setSelected(closed)
      setNotice('Event closed. It is no longer an active alarm.')
      await load()
    } catch {
      setError('The event could not be closed.')
    }
  }

  const clearFilters = () => {
    setCameraId('')
    setType('')
    setSeverity('')
    setStatus('')
    setFromDate('')
    setToDate('')
  }

  if (!result && !error) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3}>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        sx={{ justifyContent: 'space-between', gap: 2 }}
      >
        <Box>
          <Typography className="page-eyebrow">Operational history</Typography>
          <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 } }}>
            Events
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.8 }}>
            Investigate system activity, active alarms, and incidents.
          </Typography>
        </Box>
        <Chip
          label={
            realtimeStatus === 'Live'
              ? 'Live updates connected'
              : `${realtimeStatus} · 30s fallback`
          }
          color={realtimeStatus === 'Live' ? 'success' : 'warning'}
          variant="outlined"
          sx={{ alignSelf: { xs: 'flex-start', md: 'center' } }}
        />
      </Stack>

      {error && <Alert severity="error">{error}</Alert>}
      {notice && (
        <Alert severity="success" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      {result && (
        <>
          <Box className="event-summary-grid">
            <SummaryCard
              label="Matching events"
              value={result.matchingCount}
              detail="Current filters"
            />
            <SummaryCard
              label="Active alarms"
              value={result.activeAlarmCount}
              detail="Open warning or critical"
              tone="alarm"
            />
            <SummaryCard
              label="Incidents"
              value={result.incidentCount}
              detail="Operational events"
            />
          </Box>

          <Card variant="outlined">
            <CardContent>
              <Stack
                direction={{ xs: 'column', md: 'row' }}
                spacing={1.4}
                className="event-filters"
              >
                <FilterSelect
                  label="Camera"
                  value={cameraId}
                  onChange={setCameraId}
                  options={cameras.map((camera) => ({
                    value: camera.id,
                    label: camera.name,
                  }))}
                />
                <FilterSelect
                  label="Event type"
                  value={type}
                  onChange={(value) =>
                    setType(value as SystemEventType | '')
                  }
                  options={eventTypes.map((value) => ({
                    value,
                    label: formatType(value),
                  }))}
                />
                <FilterSelect
                  label="Severity"
                  value={severity}
                  onChange={(value) =>
                    setSeverity(value as EventSeverity | '')
                  }
                  options={severities.map((value) => ({
                    value,
                    label: value,
                  }))}
                />
                <FilterSelect
                  label="Status"
                  value={status}
                  onChange={(value) =>
                    setStatus(value as EventStatus | '')
                  }
                  options={statuses.map((value) => ({
                    value,
                    label: value,
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
                <Button onClick={clearFilters}>Clear</Button>
              </Stack>
            </CardContent>
          </Card>

          <Card variant="outlined">
            <CardContent>
              <Stack
                direction="row"
                sx={{ justifyContent: 'space-between', mb: 1.5 }}
              >
                <Typography variant="h6" sx={{ fontWeight: 800 }}>
                  Live event panel
                </Typography>
                <Chip label={`${result.items.length} shown`} size="small" />
              </Stack>
              {result.items.length === 0 ? (
                <Box className="dashboard-empty">
                  <Typography color="text.secondary">
                    No events match the current filters.
                  </Typography>
                </Box>
              ) : (
                <Stack spacing={1}>
                  {result.items.map((item) => (
                    <Button
                      className="event-row"
                      key={item.id}
                      onClick={() => void openDetails(item)}
                    >
                      <EventRow event={item} />
                    </Button>
                  ))}
                </Stack>
              )}
            </CardContent>
          </Card>
        </>
      )}

      <EventDetails
        event={selected}
        onClosePanel={() => setSelected(null)}
        onCloseEvent={() => void closeEvent()}
      />
    </Stack>
  )
}

function SummaryCard({
  label,
  value,
  detail,
  tone,
}: {
  label: string
  value: number
  detail: string
  tone?: 'alarm'
}) {
  return (
    <Card variant="outlined" className={tone === 'alarm' ? 'event-alarm' : ''}>
      <CardContent>
        <Typography variant="overline" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h3" sx={{ fontWeight: 800 }}>
          {value}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {detail}
        </Typography>
      </CardContent>
    </Card>
  )
}

function FilterSelect({
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
  const labelId = `event-${label.toLowerCase().replace(' ', '-')}-label`
  return (
    <FormControl size="small" sx={{ minWidth: 150 }}>
      <InputLabel id={labelId}>{label}</InputLabel>
      <Select
        labelId={labelId}
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

function EventRow({ event }: { event: SystemEvent }) {
  return (
    <>
      <Box className="event-row-main">
        <Stack direction="row" spacing={0.7} sx={{ flexWrap: 'wrap' }}>
          <Chip
            label={formatType(event.type)}
            size="small"
            color={severityColor(event.severity)}
          />
          <Chip
            label={event.status}
            size="small"
            variant={event.status === 'Open' ? 'filled' : 'outlined'}
          />
          {event.isIncident && (
            <Chip label="Incident" size="small" variant="outlined" />
          )}
        </Stack>
        <Typography sx={{ mt: 0.8, fontWeight: 700 }}>
          {event.description}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {event.cameraName ?? 'System-wide'} · {event.severity}
        </Typography>
      </Box>
      <Typography variant="caption" color="text.secondary">
        {new Date(event.timestamp).toLocaleString()}
      </Typography>
    </>
  )
}

function EventDetails({
  event,
  onClosePanel,
  onCloseEvent,
}: {
  event: SystemEvent | null
  onClosePanel: () => void
  onCloseEvent: () => void
}) {
  return (
    <Drawer anchor="right" open={event !== null} onClose={onClosePanel}>
      {event && (
        <Box className="event-detail-panel">
          <Stack
            direction="row"
            sx={{ justifyContent: 'space-between', gap: 2 }}
          >
            <Box>
              <Typography className="page-eyebrow">Event detail</Typography>
              <Typography variant="h5" sx={{ fontWeight: 800 }}>
                {formatType(event.type)}
              </Typography>
            </Box>
            <Button onClick={onClosePanel}>Close panel</Button>
          </Stack>
          <Divider sx={{ my: 2 }} />
          <Stack spacing={2}>
            <Detail label="Timestamp" value={new Date(event.timestamp).toLocaleString()} />
            <Detail label="Camera" value={event.cameraName ?? 'System-wide'} />
            <Detail label="Severity" value={event.severity} />
            <Detail label="Status" value={event.status} />
            <Detail label="Description" value={event.description} />
            <Detail
              label="Classification"
              value={
                event.isActiveAlarm
                  ? 'Active alarm and incident'
                  : event.isIncident
                    ? 'Incident'
                    : 'User activity'
              }
            />
            {event.status === 'Open' && (
              <Button
                variant="contained"
                color="success"
                onClick={onCloseEvent}
              >
                Close event
              </Button>
            )}
          </Stack>
        </Box>
      )}
    </Drawer>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <Box>
      <Typography variant="overline" color="text.secondary">
        {label}
      </Typography>
      <Typography sx={{ overflowWrap: 'anywhere' }}>{value}</Typography>
    </Box>
  )
}

function formatType(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function severityColor(severity: EventSeverity) {
  if (severity === 'Critical') return 'error'
  if (severity === 'Warning') return 'warning'
  return 'default'
}
