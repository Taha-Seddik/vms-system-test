import { useCallback, useEffect, useState } from 'react'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  LinearProgress,
  Stack,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { connectCommandCenter } from '../realtime/commandCenterConnection'
import type {
  CommandCenterSnapshot,
  DashboardCamera,
  DashboardEvent,
  RealtimeStatus,
} from '../types/dashboard'

const pollIntervalMilliseconds = 30_000

export function CommandCenterPage() {
  const { accessToken } = useAuth()
  const [dashboard, setDashboard] = useState<CommandCenterSnapshot | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [realtimeStatus, setRealtimeStatus] =
    useState<RealtimeStatus>('Connecting')

  const fetchSnapshot = useCallback(
    () =>
      apiRequest<CommandCenterSnapshot>('/api/command-center', {
        accessToken: accessToken ?? undefined,
      }),
    [accessToken],
  )

  useEffect(() => {
    let active = true

    const load = async () => {
      try {
        const result = await fetchSnapshot()
        if (active) {
          setDashboard(result)
          setError(null)
        }
      } catch {
        if (active) {
          setError('Command-center data could not be refreshed.')
        }
      }
    }

    const stopRealtime = accessToken
      ? connectCommandCenter({
          accessToken,
          onChanged: () => void load(),
          onStatusChanged: (status) => {
            if (active) {
              setRealtimeStatus(status)
            }
          },
        })
      : () => undefined

    void load()
    const poller = window.setInterval(
      () => void load(),
      pollIntervalMilliseconds,
    )

    return () => {
      active = false
      window.clearInterval(poller)
      stopRealtime()
    }
  }, [accessToken, fetchSnapshot])

  if (!dashboard && !error) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3.5}>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        sx={{ justifyContent: 'space-between', gap: 2 }}
      >
        <Box>
          <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
            <Chip label="Security operations" color="primary" size="small" />
            <Chip
              label={
                realtimeStatus === 'Live'
                  ? 'Realtime connected'
                  : `${realtimeStatus} · 30s fallback`
              }
              color={realtimeStatus === 'Live' ? 'success' : 'warning'}
              size="small"
              variant="outlined"
            />
          </Stack>
          <Typography variant="h2" sx={{ fontSize: { xs: 34, md: 48 } }}>
            Command center
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 760 }}>
            Live operational health, alarms, incidents, users, recordings, and
            storage from one automatically refreshed view.
          </Typography>
        </Box>
        {dashboard && (
          <Typography color="text.secondary" variant="caption">
            Snapshot {new Date(dashboard.generatedAt).toLocaleTimeString()}
          </Typography>
        )}
      </Stack>

      {error && <Alert severity="warning">{error}</Alert>}
      {dashboard && (
        <>
          <MetricGrid dashboard={dashboard} />

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, lg: 8 }}>
              <DashboardPanel
                title="Camera wall status"
                subtitle="Live playback joins these tiles in Step 5"
              >
                <Box className="command-camera-wall">
                  {dashboard.cameraHealth.map((camera) => (
                    <CameraWallTile key={camera.id} camera={camera} />
                  ))}
                </Box>
              </DashboardPanel>
            </Grid>
            <Grid size={{ xs: 12, lg: 4 }}>
              <StoragePanel dashboard={dashboard} />
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Active alarms"
                count={dashboard.activeAlarms.length}
              >
                <EventList
                  events={dashboard.activeAlarms}
                  empty="No open warning or critical alarms."
                />
              </DashboardPanel>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Offline cameras"
                count={dashboard.offlineCameras.length}
              >
                {dashboard.offlineCameras.length === 0 ? (
                  <EmptyPanel message="All enabled cameras are reporting." />
                ) : (
                  <Stack spacing={1}>
                    {dashboard.offlineCameras.map((camera) => (
                      <Box className="dashboard-list-row" key={camera.id}>
                        <Box>
                          <Typography sx={{ fontWeight: 750 }}>
                            {camera.name}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {camera.location} ·{' '}
                            {camera.lastConnectionError ?? 'No response'}
                          </Typography>
                        </Box>
                        <Chip label="Offline" color="error" size="small" />
                      </Box>
                    ))}
                  </Stack>
                )}
              </DashboardPanel>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Recording failures"
                count={dashboard.recordingFailures.length}
              >
                <EventList
                  events={dashboard.recordingFailures}
                  empty="No recording failures have been reported."
                />
              </DashboardPanel>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Recent incidents"
                count={dashboard.recentIncidents.length}
              >
                <EventList
                  events={dashboard.recentIncidents}
                  empty="No operational incidents have been reported."
                />
              </DashboardPanel>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Operator activity"
                count={dashboard.operatorActivity.length}
              >
                {dashboard.operatorActivity.length === 0 ? (
                  <EmptyPanel message="No recent Operator activity." />
                ) : (
                  <Stack spacing={1}>
                    {dashboard.operatorActivity.map((activity) => (
                      <Box className="dashboard-list-row" key={activity.id}>
                        <Box>
                          <Typography sx={{ fontWeight: 700 }}>
                            {activity.displayName}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {activity.description}
                          </Typography>
                        </Box>
                        <Timestamp value={activity.timestamp} />
                      </Box>
                    ))}
                  </Stack>
                )}
              </DashboardPanel>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Recent events"
                count={dashboard.recentEvents.length}
              >
                <EventList
                  events={dashboard.recentEvents}
                  empty="No system events yet."
                />
              </DashboardPanel>
            </Grid>
          </Grid>
        </>
      )}
    </Stack>
  )
}

function MetricGrid({ dashboard }: { dashboard: CommandCenterSnapshot }) {
  const { metrics, storage } = dashboard
  const cards = [
    {
      label: 'Cameras',
      value: metrics.totalCameras,
      detail: `${metrics.onlineCameras} online · ${metrics.offlineCameras} offline`,
      tone: 'blue',
    },
    {
      label: 'Live streams',
      value: metrics.activeLiveStreams,
      detail: 'Enabled and probe-confirmed',
      tone: 'green',
    },
    {
      label: 'Recordings',
      value: metrics.activeRecordings,
      detail: 'Active recording processes',
      tone: 'amber',
    },
    {
      label: 'Active users',
      value: metrics.activeUsers,
      detail: 'Distinct users in last 5 minutes',
      tone: 'violet',
    },
    {
      label: 'Storage used',
      value: `${storage.usedPercent.toFixed(1)}%`,
      detail: formatBytes(storage.availableBytes) + ' available',
      tone: 'blue',
    },
    {
      label: 'System uptime',
      value: formatDuration(metrics.systemUptimeSeconds),
      detail: 'Since API process start',
      tone: 'green',
    },
  ]

  return (
    <Box className="dashboard-metrics">
      {cards.map((card) => (
        <Card className={`dashboard-metric tone-${card.tone}`} key={card.label}>
          <CardContent>
            <Typography variant="overline" color="text.secondary">
              {card.label}
            </Typography>
            <Typography variant="h3" sx={{ mt: 0.5, fontWeight: 800 }}>
              {card.value}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {card.detail}
            </Typography>
          </CardContent>
        </Card>
      ))}
    </Box>
  )
}

function DashboardPanel({
  title,
  subtitle,
  count,
  children,
}: {
  title: string
  subtitle?: string
  count?: number
  children: React.ReactNode
}) {
  return (
    <Card variant="outlined" className="dashboard-panel">
      <CardContent>
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', gap: 1, mb: 2 }}
        >
          <Box>
            <Typography variant="h6" sx={{ fontWeight: 800 }}>
              {title}
            </Typography>
            {subtitle && (
              <Typography variant="caption" color="text.secondary">
                {subtitle}
              </Typography>
            )}
          </Box>
          {count !== undefined && (
            <Chip label={count} size="small" variant="outlined" />
          )}
        </Stack>
        {children}
      </CardContent>
    </Card>
  )
}

function CameraWallTile({ camera }: { camera: DashboardCamera }) {
  return (
    <Box className={`command-camera-tile status-${camera.connectionStatus}`}>
      <Box className="command-camera-visual">
        <span>{camera.connectionStatus}</span>
      </Box>
      <Stack
        direction="row"
        sx={{ justifyContent: 'space-between', gap: 1, mt: 1.2 }}
      >
        <Box>
          <Typography sx={{ fontWeight: 750 }}>{camera.name}</Typography>
          <Typography variant="caption" color="text.secondary">
            {camera.location} · {camera.resolution ?? 'Awaiting metadata'}
          </Typography>
        </Box>
        <Chip
          label={camera.recordingStatus === 'Recording' ? 'REC' : 'Idle'}
          size="small"
          color={
            camera.recordingStatus === 'Recording' ? 'error' : 'default'
          }
        />
      </Stack>
    </Box>
  )
}

function StoragePanel({ dashboard }: { dashboard: CommandCenterSnapshot }) {
  const storage = dashboard.storage
  return (
    <DashboardPanel title="Storage health">
      <Stack spacing={2}>
        <Stack
          direction="row"
          sx={{ justifyContent: 'space-between', alignItems: 'center' }}
        >
          <Typography variant="h3" sx={{ fontWeight: 800 }}>
            {storage.usedPercent.toFixed(1)}%
          </Typography>
          <Chip
            label={storage.status}
            size="small"
            color={
              storage.status === 'Healthy'
                ? 'success'
                : storage.status === 'Warning'
                  ? 'warning'
                  : 'error'
            }
          />
        </Stack>
        <LinearProgress
          variant="determinate"
          value={Math.min(storage.usedPercent, 100)}
          color={
            storage.status === 'Healthy'
              ? 'success'
              : storage.status === 'Warning'
                ? 'warning'
                : 'error'
          }
          sx={{ height: 10, borderRadius: 10 }}
        />
        <Box>
          <Typography variant="body2">
            {formatBytes(storage.usedBytes)} of {formatBytes(storage.totalBytes)}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Recording files: {formatBytes(storage.recordingBytes)}
          </Typography>
        </Box>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ overflowWrap: 'anywhere' }}
        >
          {storage.path}
        </Typography>
      </Stack>
    </DashboardPanel>
  )
}

function EventList({
  events,
  empty,
}: {
  events: DashboardEvent[]
  empty: string
}) {
  if (events.length === 0) {
    return <EmptyPanel message={empty} />
  }

  return (
    <Stack spacing={1}>
      {events.map((event) => (
        <Box className="dashboard-list-row" key={event.id}>
          <Box sx={{ minWidth: 0 }}>
            <Stack direction="row" spacing={0.7} sx={{ mb: 0.4 }}>
              <Chip
                label={event.type.replace(/([a-z])([A-Z])/g, '$1 $2')}
                size="small"
                color={
                  event.severity === 'Critical'
                    ? 'error'
                    : event.severity === 'Warning'
                      ? 'warning'
                      : 'default'
                }
              />
              {event.status === 'Open' && (
                <Chip label="Open" size="small" variant="outlined" />
              )}
            </Stack>
            <Typography variant="body2" sx={{ overflowWrap: 'anywhere' }}>
              {event.description}
            </Typography>
          </Box>
          <Timestamp value={event.timestamp} />
        </Box>
      ))}
    </Stack>
  )
}

function EmptyPanel({ message }: { message: string }) {
  return (
    <Box className="dashboard-empty">
      <Typography color="text.secondary" variant="body2">
        {message}
      </Typography>
    </Box>
  )
}

function Timestamp({ value }: { value: string }) {
  return (
    <Typography
      variant="caption"
      color="text.secondary"
      sx={{ flexShrink: 0 }}
    >
      {new Date(value).toLocaleTimeString()}
    </Typography>
  )
}

function formatDuration(totalSeconds: number) {
  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  if (days > 0) {
    return `${days}d ${hours}h`
  }
  if (hours > 0) {
    return `${hours}h ${minutes}m`
  }
  return `${minutes}m`
}

function formatBytes(bytes: number) {
  if (bytes === 0) {
    return '0 B'
  }
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const index = Math.min(
    Math.floor(Math.log(bytes) / Math.log(1024)),
    units.length - 1,
  )
  return `${(bytes / 1024 ** index).toFixed(index > 2 ? 1 : 0)} ${units[index]}`
}
