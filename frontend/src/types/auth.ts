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
