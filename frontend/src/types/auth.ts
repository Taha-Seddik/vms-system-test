export type AppRole = 'Administrator' | 'Operator' | 'Viewer'

export interface AuthenticatedUser {
  id: string
  username: string
  displayName: string
  role: AppRole
  assignedCameraIds: string[]
  lastLoginAt: string | null
  lastActivityAt: string | null
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: AuthenticatedUser
}

export interface AccessibleCamera {
  id: string
  name: string
  location: string
  hlsUrl: string
  group?: CameraGroupSummary | null
  resolution?: string | null
  framesPerSecond?: number | null
  recordingStatus?: 'NotRecording' | 'Recording'
  connectionStatus?: CameraConnectionStatus
  isEnabled?: boolean
  lastHeartbeatAt?: string | null
  lastCheckedAt?: string | null
}

export type CameraConnectionStatus =
  | 'Unknown'
  | 'Online'
  | 'Offline'
  | 'Disabled'

export interface CameraGroupSummary {
  id: string
  name: string
}

export interface CameraGroup extends CameraGroupSummary {
  description: string | null
  cameraCount: number
  createdAt: string
  updatedAt: string
}

export interface ManagedCamera extends Required<AccessibleCamera> {
  rtspUrl: string
  lastConnectionError: string | null
  createdAt: string
  updatedAt: string
}

export interface CameraConnectionTest {
  cameraId: string
  succeeded: boolean
  status: CameraConnectionStatus
  checkedAt: string
  elapsedMilliseconds: number
  codec: string | null
  resolution: string | null
  framesPerSecond: number | null
  error: string | null
}

export interface ActivityEvent {
  id: string
  type: 'UserLogin' | 'UserLogout'
  timestamp: string
  description: string
}

export interface AuthActivity {
  activeSessions: number
  recentEvents: ActivityEvent[]
}
