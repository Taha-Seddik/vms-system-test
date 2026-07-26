import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ThemeProvider } from '@mui/material'
import { MemoryRouter } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './auth/AuthContext'
import { theme } from './theme'

vi.mock('./realtime/commandCenterConnection', () => ({
  connectCommandCenter: () => () => undefined,
}))

const viewerLogin = {
  accessToken: 'viewer-token',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'viewer-id',
    username: 'viewer',
    displayName: 'Assigned Camera Viewer',
    role: 'Viewer',
    assignedCameraIds: ['camera-1', 'camera-2'],
    lastLoginAt: '2026-07-26T00:00:00Z',
    lastActivityAt: '2026-07-26T00:00:00Z',
  },
}

const administratorLogin = {
  ...viewerLogin,
  accessToken: 'admin-token',
  user: {
    ...viewerLogin.user,
    id: 'admin-id',
    username: 'admin',
    displayName: 'System Administrator',
    role: 'Administrator',
    assignedCameraIds: [],
  },
}

const activeAlarmEvents = Array.from({ length: 6 }, (_, index) => ({
  id: `alarm-${index + 1}`,
  type: 'CameraOffline',
  timestamp: `2026-07-26T12:0${index}:00Z`,
  cameraId: `camera-${(index % 4) + 1}`,
  cameraName: `Camera ${(index % 4) + 1}`,
  severity: index === 0 ? 'Critical' : 'Warning',
  description: `Alarm event ${index + 1}`,
  status: 'Open',
}))

const commandCenterSnapshot = {
  generatedAt: '2026-07-26T12:00:00Z',
  metrics: {
    totalCameras: 4,
    onlineCameras: 4,
    offlineCameras: 0,
    disabledCameras: 0,
    activeLiveStreams: 4,
    activeRecordings: 0,
    activeUsers: 2,
    systemUptimeSeconds: 3600,
  },
  storage: {
    path: '/var/lib/vms/recordings',
    status: 'Healthy',
    totalBytes: 1_000_000,
    availableBytes: 600_000,
    usedBytes: 400_000,
    recordingBytes: 0,
    usedPercent: 40,
    error: null,
  },
  cameraHealth: [
    {
      id: 'camera-1',
      name: 'Entrance',
      location: 'Main entrance',
      group: 'Perimeter',
      connectionStatus: 'Online',
      recordingStatus: 'NotRecording',
      isEnabled: true,
      resolution: '640x360',
      framesPerSecond: 10,
      lastHeartbeatAt: '2026-07-26T12:00:00Z',
      lastCheckedAt: '2026-07-26T12:00:00Z',
      lastConnectionError: null,
    },
  ],
  offlineCameras: [],
  recentEvents: [],
  recordingFailures: [],
  activeAlarms: activeAlarmEvents,
  recentIncidents: [],
  operatorActivity: [],
}

function renderApp(initialPath = '/') {
  return render(
    <ThemeProvider theme={theme}>
      <MemoryRouter initialEntries={[initialPath]}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </MemoryRouter>
    </ThemeProvider>,
  )
}

describe('authentication and authorization UI', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('redirects an anonymous visitor to the login screen', () => {
    renderApp()

    expect(
      screen.getByRole('heading', { name: /the right view for every role/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /sign in securely/i }))
      .toBeInTheDocument()
  })

  it('signs in a Viewer and renders only cameras returned by the API', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) {
        return Response.json(viewerLogin)
      }
      if (url.endsWith('/api/cameras/accessible')) {
        return Response.json([
          {
            id: 'camera-1',
            name: 'Entrance',
            location: 'Main entrance',
            hlsUrl: 'http://localhost:8888/camera-1/index.m3u8',
          },
          {
            id: 'camera-2',
            name: 'Loading Bay',
            location: 'Loading bay',
            hlsUrl: 'http://localhost:8888/camera-2/index.m3u8',
          },
        ])
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/login')
    fireEvent.click(screen.getByRole('button', { name: /sign in securely/i }))

    expect(
      await screen.findByRole('heading', { name: /accessible cameras/i }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Entrance')).toBeInTheDocument()
    expect(screen.getByText('Loading Bay')).toBeInTheDocument()
    expect(screen.queryByText('Warehouse')).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /activity/i }))
      .not.toBeInTheDocument()
  })

  it('rejects a Viewer navigation attempt to the administrator page', async () => {
    sessionStorage.setItem('vms-auth-session', JSON.stringify(viewerLogin))
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(viewerLogin.user)
      }
      if (url.endsWith('/api/cameras/accessible')) {
        return Response.json([])
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/activity')

    await waitFor(() =>
      expect(
        screen.getByRole('heading', { name: /accessible cameras/i }),
      ).toBeInTheDocument(),
    )
    expect(
      screen.queryByRole('heading', { name: /authentication activity/i }),
    ).not.toBeInTheDocument()
  })

  it('loads the Administrator camera-management workspace', async () => {
    sessionStorage.setItem(
      'vms-auth-session',
      JSON.stringify(administratorLogin),
    )
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(administratorLogin.user)
      }
      if (url.endsWith('/api/cameras/manage')) {
        return Response.json([
          {
            id: 'camera-1',
            name: 'Entrance',
            location: 'Main entrance',
            rtspUrl: 'rtsp://mediamtx:8554/camera-1',
            hlsUrl: '/camera-1/index.m3u8',
            group: { id: 'group-1', name: 'Perimeter' },
            resolution: '640x360',
            framesPerSecond: 10,
            recordingStatus: 'NotRecording',
            connectionStatus: 'Online',
            isEnabled: true,
            lastHeartbeatAt: '2026-07-26T00:00:00Z',
            lastCheckedAt: '2026-07-26T00:00:00Z',
            lastConnectionError: null,
            createdAt: '2026-07-26T00:00:00Z',
            updatedAt: '2026-07-26T00:00:00Z',
          },
        ])
      }
      if (url.endsWith('/api/camera-groups')) {
        return Response.json([
          {
            id: 'group-1',
            name: 'Perimeter',
            description: 'Public entrances',
            cameraCount: 1,
            createdAt: '2026-07-26T00:00:00Z',
            updatedAt: '2026-07-26T00:00:00Z',
          },
        ])
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/manage/cameras')

    expect(
      await screen.findByRole('heading', { name: /camera management/i }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Entrance')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /add camera/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('link', { name: /manage/i })).toBeInTheDocument()
  })

  it('loads the command center with required operational metrics', async () => {
    sessionStorage.setItem(
      'vms-auth-session',
      JSON.stringify(administratorLogin),
    )
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(administratorLogin.user)
      }
      if (url.endsWith('/api/command-center')) {
        return Response.json(commandCenterSnapshot)
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/command-center')

    expect(
      await screen.findByRole('heading', {
        name: 'Command center',
        level: 2,
      }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Live streams')).toBeInTheDocument()
    expect(screen.getByText('Active users')).toBeInTheDocument()
    expect(screen.getByText('Storage health')).toBeInTheDocument()
    expect(screen.getByText('Active alarms')).toBeInTheDocument()
    expect(screen.getByText('Recording failures')).toBeInTheDocument()
    expect(screen.getByText('Recent incidents')).toBeInTheDocument()
    expect(screen.getByText('Operator activity')).toBeInTheDocument()
    expect(
      screen.getByRole('link', {
        name: /command center operational overview/i,
      }),
    ).toBeInTheDocument()
    expect(screen.getByText('Alarm event 5')).toBeInTheDocument()
    expect(screen.queryByText('Alarm event 6')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /view all 6/i }))

    expect(await screen.findByText('Showing all records')).toBeInTheDocument()
    expect(screen.getByText('Alarm event 6')).toBeInTheDocument()
    fireEvent.click(
      screen.getByRole('button', { name: /close detail panel/i }),
    )
  })
})
