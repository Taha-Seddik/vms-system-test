import type { AppRole } from './auth'

export interface UserCamera {
  id: string
  name: string
}

export interface ManagedUser {
  id: string
  username: string
  displayName: string
  role: AppRole
  isEnabled: boolean
  assignedCameras: UserCamera[]
  createdAt: string
  lastLoginAt: string | null
  lastActivityAt: string | null
}
