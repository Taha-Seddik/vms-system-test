import type {
  AppRole,
  CameraConnectionStatus,
} from './auth'
import type {
  CameraRecordingStatus,
  EventSeverity,
  EventStatus,
  SystemEventType,
} from './dashboard'
import type {
  RecordingMode,
  RecordingState,
} from './recording'

export interface SearchCamera {
  id: string
  name: string
  location: string
  cameraGroupId: string | null
  cameraGroupName: string | null
  status: CameraConnectionStatus
  recordingStatus: CameraRecordingStatus
}

export interface SearchRecording {
  id: string
  cameraId: string
  cameraName: string
  cameraGroupId: string | null
  cameraGroupName: string | null
  mode: RecordingMode
  status: RecordingState
  startedAt: string
  durationSeconds: number | null
}

export interface SearchEvent {
  id: string
  type: SystemEventType
  timestamp: string
  cameraId: string | null
  cameraName: string | null
  severity: EventSeverity
  status: EventStatus
  description: string
}

export interface SearchUser {
  id: string
  username: string
  displayName: string
  role: AppRole
  isEnabled: boolean
  createdAt: string
}

export interface GlobalSearchResult {
  generatedAt: string
  cameras: SearchCamera[]
  recordings: SearchRecording[]
  events: SearchEvent[]
  users: SearchUser[]
}
