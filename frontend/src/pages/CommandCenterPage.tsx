import { useCallback, useEffect, useState } from 'react'
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
  Grid,
  IconButton,
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
  OperatorActivity,
  RealtimeStatus,
} from '../types/dashboard'

const pollIntervalMilliseconds = 30_000
const previewLimit = 5

type DetailPanel =
  | {
      kind: 'events'
      title: string
      description: string
      items: DashboardEvent[]
    }
  | {
      kind: 'cameras'
      title: string
      description: string
      items: DashboardCamera[]
    }
  | {
      kind: 'activity'
      title: string
      description: string
      items: OperatorActivity[]
    }

export function CommandCenterPage() {
  const { accessToken } = useAuth()
  const [dashboard, setDashboard] = useState<CommandCenterSnapshot | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [realtimeStatus, setRealtimeStatus] =
    useState<RealtimeStatus>('Connecting')
  const [detailPanel, setDetailPanel] = useState<DetailPanel | null>(null)

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

  const openEvents = (
    title: string,
    description: string,
    items: DashboardEvent[],
  ) => setDetailPanel({ kind: 'events', title, description, items })

  const openCameras = (
    title: string,
    description: string,
    items: DashboardCamera[],
  ) => setDetailPanel({ kind: 'cameras', title, description, items })

  const openActivity = (
    title: string,
    description: string,
    items: OperatorActivity[],
  ) => setDetailPanel({ kind: 'activity', title, description, items })

  if (!dashboard && !error) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3.5}>
      <Box className="dashboard-hero">
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          sx={{ justifyContent: 'space-between', gap: 3 }}
        >
          <Box>
            <Typography className="page-eyebrow">
              Security operations
            </Typography>
            <Typography variant="h2" sx={{ fontSize: { xs: 34, md: 46 } }}>
              Command center
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 700 }}>
              A focused view of camera health, incidents, users, recordings,
              and storage across the VMS.
            </Typography>
          </Box>
          <Stack
            sx={{
              alignItems: { xs: 'flex-start', md: 'flex-end' },
              justifyContent: 'space-between',
              gap: 1.25,
            }}
          >
            <Chip
              label={
                realtimeStatus === 'Live'
                  ? 'Live updates connected'
                  : `${realtimeStatus} · 30s fallback`
              }
              color={realtimeStatus === 'Live' ? 'success' : 'warning'}
              size="small"
              variant="outlined"
            />
            {dashboard && (
              <Typography color="text.secondary" variant="caption">
                Last refreshed{' '}
                {new Date(dashboard.generatedAt).toLocaleTimeString()}
              </Typography>
            )}
          </Stack>
        </Stack>
      </Box>

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
                onOpen={
                  dashboard.activeAlarms.length > 0
                    ? () =>
                        openEvents(
                          'Active alarms',
                          'Every open warning or critical event requiring attention.',
                          dashboard.activeAlarms,
                        )
                    : undefined
                }
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
                onOpen={
                  dashboard.offlineCameras.length > 0
                    ? () =>
                        openCameras(
                          'Offline cameras',
                          'Cameras that failed their most recent connection probe.',
                          dashboard.offlineCameras,
                        )
                    : undefined
                }
              >
                <OfflineCameraList cameras={dashboard.offlineCameras} />
              </DashboardPanel>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Recording failures"
                count={dashboard.recordingFailures.length}
                onOpen={
                  dashboard.recordingFailures.length > 0
                    ? () =>
                        openEvents(
                          'Recording failures',
                          'Recent recording jobs that could not start or complete.',
                          dashboard.recordingFailures,
                        )
                    : undefined
                }
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
                onOpen={
                  dashboard.recentIncidents.length > 0
                    ? () =>
                        openEvents(
                          'Recent incidents',
                          'Operational events, excluding authentication activity.',
                          dashboard.recentIncidents,
                        )
                    : undefined
                }
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
                onOpen={
                  dashboard.operatorActivity.length > 0
                    ? () =>
                        openActivity(
                          'Operator activity',
                          'Recent authentication activity by Operator accounts.',
                          dashboard.operatorActivity,
                        )
                    : undefined
                }
              >
                <OperatorActivityList
                  activity={dashboard.operatorActivity}
                />
              </DashboardPanel>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <DashboardPanel
                title="Recent events"
                count={dashboard.recentEvents.length}
                onOpen={
                  dashboard.recentEvents.length > 0
                    ? () =>
                        openEvents(
                          'Recent events',
                          'The latest authentication and operational events.',
                          dashboard.recentEvents,
                        )
                    : undefined
                }
              >
                <EventList
                  events={dashboard.recentEvents}
                  empty="No system events yet."
                />
              </DashboardPanel>
            </Grid>
          </Grid>
          <DashboardDetailDrawer
            panel={detailPanel}
            onClose={() => setDetailPanel(null)}
          />
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
      code: 'CAM',
      value: metrics.totalCameras,
      detail: `${metrics.onlineCameras} online · ${metrics.offlineCameras} offline`,
      tone: 'blue',
    },
    {
      label: 'Live streams',
      code: 'LIVE',
      value: metrics.activeLiveStreams,
      detail: 'Enabled and probe-confirmed',
      tone: 'green',
    },
    {
      label: 'Recordings',
      code: 'REC',
      value: metrics.activeRecordings,
      detail: 'Active recording processes',
      tone: 'amber',
    },
    {
      label: 'Active users',
      code: 'USR',
      value: metrics.activeUsers,
      detail: 'Distinct users in last 5 minutes',
      tone: 'violet',
    },
    {
      label: 'Storage used',
      code: 'STO',
      value: `${storage.usedPercent.toFixed(1)}%`,
      detail: formatBytes(storage.availableBytes) + ' available',
      tone: 'blue',
    },
    {
      label: 'System uptime',
      code: 'UP',
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
            <Stack
              direction="row"
              sx={{ justifyContent: 'space-between', alignItems: 'center' }}
            >
              <Typography variant="overline" color="text.secondary">
                {card.label}
              </Typography>
              <Box className="metric-code">{card.code}</Box>
            </Stack>
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
  onOpen,
  children,
}: {
  title: string
  subtitle?: string
  count?: number
  onOpen?: () => void
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
          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
            {count !== undefined && (
              <Chip label={count} size="small" variant="outlined" />
            )}
            {onOpen && (
              <Button size="small" onClick={onOpen}>
                {count !== undefined && count > previewLimit
                  ? `View all ${count}`
                  : 'View details'}
              </Button>
            )}
          </Stack>
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
        {storage.error && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {storage.error}
          </Alert>
        )}
      </Stack>
    </DashboardPanel>
  )
}

function EventList({
  events,
  empty,
  showAll = false,
}: {
  events: DashboardEvent[]
  empty: string
  showAll?: boolean
}) {
  if (events.length === 0) {
    return <EmptyPanel message={empty} />
  }

  return (
    <Stack spacing={1}>
      {(showAll ? events : events.slice(0, previewLimit)).map((event) => (
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
            <Typography
              className={showAll ? undefined : 'preview-description'}
              variant="body2"
              sx={{ overflowWrap: 'anywhere' }}
            >
              {event.description}
            </Typography>
          </Box>
          <Timestamp value={event.timestamp} />
        </Box>
      ))}
    </Stack>
  )
}

function OfflineCameraList({
  cameras,
  showAll = false,
}: {
  cameras: DashboardCamera[]
  showAll?: boolean
}) {
  if (cameras.length === 0) {
    return <EmptyPanel message="All enabled cameras are reporting." />
  }

  return (
    <Stack spacing={1}>
      {(showAll ? cameras : cameras.slice(0, previewLimit)).map((camera) => (
        <Box className="dashboard-list-row" key={camera.id}>
          <Box sx={{ minWidth: 0 }}>
            <Typography sx={{ fontWeight: 750 }}>{camera.name}</Typography>
            <Typography
              className={showAll ? undefined : 'preview-description'}
              variant="caption"
              color="text.secondary"
            >
              {camera.location} · {camera.lastConnectionError ?? 'No response'}
            </Typography>
          </Box>
          <Chip label="Offline" color="error" size="small" />
        </Box>
      ))}
    </Stack>
  )
}

function OperatorActivityList({
  activity,
  showAll = false,
}: {
  activity: OperatorActivity[]
  showAll?: boolean
}) {
  if (activity.length === 0) {
    return <EmptyPanel message="No recent Operator activity." />
  }

  return (
    <Stack spacing={1}>
      {(showAll ? activity : activity.slice(0, previewLimit)).map((item) => (
        <Box className="dashboard-list-row" key={item.id}>
          <Box sx={{ minWidth: 0 }}>
            <Typography sx={{ fontWeight: 700 }}>
              {item.displayName} · {item.action}
            </Typography>
            <Typography
              className={showAll ? undefined : 'preview-description'}
              variant="caption"
              color="text.secondary"
            >
              {item.description}
            </Typography>
          </Box>
          <Timestamp value={item.timestamp} detailed={showAll} />
        </Box>
      ))}
    </Stack>
  )
}

function DashboardDetailDrawer({
  panel,
  onClose,
}: {
  panel: DetailPanel | null
  onClose: () => void
}) {
  return (
    <Drawer
      anchor="right"
      open={panel !== null}
      onClose={onClose}
      className="dashboard-detail-drawer"
    >
      {panel && (
        <Box className="dashboard-detail-panel">
          <Box className="detail-panel-header">
            <Box>
              <Typography className="page-eyebrow">Operational detail</Typography>
              <Typography variant="h5" sx={{ fontWeight: 800 }}>
                {panel.title}
              </Typography>
            </Box>
            <IconButton aria-label="Close detail panel" onClick={onClose}>
              <CloseIcon />
            </IconButton>
          </Box>
          <Typography color="text.secondary" sx={{ px: 3, pb: 2 }}>
            {panel.description}
          </Typography>
          <Divider />
          <Box className="detail-panel-summary">
            <Typography variant="overline" color="text.secondary">
              Showing all records
            </Typography>
            <Chip label={panel.items.length} size="small" color="primary" />
          </Box>
          <Box className="detail-panel-content">
            {panel.kind === 'events' && (
              <EventList
                events={panel.items}
                empty="No records are currently available."
                showAll
              />
            )}
            {panel.kind === 'cameras' && (
              <OfflineCameraList cameras={panel.items} showAll />
            )}
            {panel.kind === 'activity' && (
              <OperatorActivityList activity={panel.items} showAll />
            )}
          </Box>
        </Box>
      )}
    </Drawer>
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

function Timestamp({
  value,
  detailed = false,
}: {
  value: string
  detailed?: boolean
}) {
  return (
    <Typography
      variant="caption"
      color="text.secondary"
      sx={{ flexShrink: 0 }}
    >
      {detailed
        ? new Date(value).toLocaleString()
        : new Date(value).toLocaleTimeString()}
    </Typography>
  )
}

function CloseIcon() {
  return (
    <svg
      width="22"
      height="22"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <path d="m6 6 12 12M18 6 6 18" />
    </svg>
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
