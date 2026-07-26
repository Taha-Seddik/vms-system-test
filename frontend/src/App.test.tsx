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
    URL.createObjectURL = vi.fn(() => 'blob:vms-test')
    URL.revokeObjectURL = vi.fn()
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
      await screen.findByRole('heading', { name: /live monitoring/i }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Entrance')).toBeInTheDocument()
    expect(screen.getByText('Loading Bay')).toBeInTheDocument()
    expect(screen.queryByText('Warehouse')).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /activity/i }))
      .not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /16 camera layout/i }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^manual$/i }))
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
        screen.getByRole('heading', { name: /live monitoring/i }),
      ).toBeInTheDocument(),
    )
    expect(
      screen.queryByRole('heading', { name: /authentication activity/i }),
    ).not.toBeInTheDocument()
  })

  it('shows recording controls to an Administrator on the live wall', async () => {
    sessionStorage.setItem(
      'vms-auth-session',
      JSON.stringify(administratorLogin),
    )
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(administratorLogin.user)
      }
      if (url.endsWith('/api/cameras/accessible')) {
        return Response.json([
          {
            id: 'camera-1',
            name: 'Entrance',
            location: 'Main entrance',
            hlsUrl: '/camera-1/index.m3u8',
            connectionStatus: 'Online',
            recordingStatus: 'NotRecording',
            isEnabled: true,
            resolution: '640x360',
          },
        ])
      }
      if (url.includes('/api/recordings?')) {
        return Response.json([])
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/cameras')

    expect(
      await screen.findByRole('heading', { name: /live monitoring/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^manual$/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^continuous$/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('button', { name: /simulate motion/i }))
      .toBeInTheDocument()
    expect(screen.getByText(/no recordings have been created/i))
      .toBeInTheDocument()
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

  it('filters, inspects, and closes an active event', async () => {
    sessionStorage.setItem(
      'vms-auth-session',
      JSON.stringify(administratorLogin),
    )
    const openEvent = {
      id: 'event-storage-full',
      type: 'StorageFull',
      timestamp: '2026-07-26T12:00:00Z',
      cameraId: null,
      cameraName: null,
      severity: 'Critical',
      description: 'Recording storage reached its critical threshold.',
      status: 'Open',
      isActiveAlarm: true,
      isIncident: true,
    }
    const closedEvent = {
      ...openEvent,
      status: 'Closed',
      isActiveAlarm: false,
    }
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, options) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(administratorLogin.user)
      }
      if (url.endsWith('/api/cameras/accessible')) {
        return Response.json([])
      }
      if (url.includes('/api/events?')) {
        return Response.json({
          generatedAt: '2026-07-26T12:00:00Z',
          matchingCount: 1,
          activeAlarmCount: options ? 0 : 1,
          incidentCount: 1,
          items: [openEvent],
        })
      }
      if (
        url.endsWith('/api/events/event-storage-full/close')
        && options?.method === 'POST'
      ) {
        return Response.json(closedEvent)
      }
      if (url.endsWith('/api/events/event-storage-full')) {
        return Response.json(openEvent)
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/events')

    expect(
      await screen.findByRole('heading', { name: 'Events', level: 2 }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Live event panel')).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: /events alarms and incidents/i }),
    ).toBeInTheDocument()

    fireEvent.click(
      screen.getByRole('button', {
        name: /storage full.*recording storage reached/i,
      }),
    )
    expect(await screen.findByText('Event detail')).toBeInTheDocument()
    expect(screen.getByText('Active alarm and incident')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Close event' }))
    expect(
      await screen.findByText(/event closed.*no longer an active alarm/i),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Close event' }),
    ).not.toBeInTheDocument()
  })

  it('provides recording playback controls and clickable keyframes', async () => {
    sessionStorage.setItem(
      'vms-auth-session',
      JSON.stringify(administratorLogin),
    )
    const recording = {
      id: 'recording-1',
      cameraId: 'camera-1',
      cameraName: 'Entrance',
      mode: 'Manual',
      state: 'Completed',
      startedAt: '2026-07-26T12:00:00Z',
      endedAt: '2026-07-26T12:01:05Z',
      durationSeconds: 65,
      fileSizeBytes: 500_000,
      failureReason: null,
      triggerEventId: null,
    }
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input)
      if (url.endsWith('/api/auth/me')) {
        return Response.json(administratorLogin.user)
      }
      if (url.includes('/api/recordings?')) {
        return Response.json([recording])
      }
      if (url.endsWith('/api/cameras/accessible')) {
        return Response.json([
          {
            id: 'camera-1',
            name: 'Entrance',
            location: 'Main entrance',
            hlsUrl: '/camera-1/index.m3u8',
          },
        ])
      }
      if (url.endsWith('/api/recordings/recording-1')) {
        return Response.json({
          recording,
          keyframes: [
            { id: 'keyframe-0', timestampSeconds: 0 },
            { id: 'keyframe-30', timestampSeconds: 30 },
            { id: 'keyframe-60', timestampSeconds: 60 },
          ],
        })
      }
      if (
        url.endsWith('/api/recordings/recording-1/media')
        || url.endsWith('/api/recordings/recording-1/download')
        || url.includes('/keyframes/')
      ) {
        return new Response(new Blob(['media']), {
          headers: {
            'Content-Type': url.includes('/keyframes/')
              ? 'image/jpeg'
              : 'video/mp4',
          },
        })
      }
      return new Response(null, { status: 404 })
    })

    renderApp('/playback')

    expect(
      await screen.findByRole('heading', { name: 'Playback', level: 2 }),
    ).toBeInTheDocument()
    expect(await screen.findByText('Keyframe timeline')).toBeInTheDocument()
    expect(
      screen.getByRole('slider', { name: /recording timeline/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /forward 10s/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('button', { name: /snapshot/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('button', { name: /download mp4/i }))
      .toBeInTheDocument()
    expect(screen.getByRole('button', { name: /seek to 0:30/i }))
      .toBeInTheDocument()

    const video = document.querySelector('video')!
    fireEvent.click(screen.getByRole('button', { name: /seek to 0:30/i }))
    expect(video.currentTime).toBe(30)

    Object.defineProperty(video, 'videoWidth', {
      configurable: true,
      value: 640,
    })
    Object.defineProperty(video, 'videoHeight', {
      configurable: true,
      value: 360,
    })
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      drawImage: vi.fn(),
    } as unknown as CanvasRenderingContext2D)
    vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(
      (callback) => callback(new Blob(['snapshot'], { type: 'image/png' })),
    )
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(
      () => undefined,
    )
    fireEvent.click(screen.getByRole('button', { name: /snapshot/i }))
    expect(await screen.findByText(/playback snapshot downloaded/i))
      .toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /download mp4/i }))
    expect(await screen.findByText(/recording download started/i))
      .toBeInTheDocument()

    fireEvent.mouseDown(screen.getByRole('combobox', { name: /speed/i }))
    fireEvent.click(await screen.findByRole('option', { name: '4×' }))
    expect(video.playbackRate).toBe(4)

    expect(
      screen.getByRole('link', { name: /playback recordings and keyframes/i }),
    ).toBeInTheDocument()
  })
})
