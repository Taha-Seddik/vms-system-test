import type {
  CameraConnectionStatus,
} from './auth'

export type CameraRecordingStatus = 'NotRecording' | 'Recording'
export type EventSeverity = 'Information' | 'Warning' | 'Critical'
export type EventStatus = 'Open' | 'Closed'
export type SystemEventType =
  | 'UserLogin'
  | 'UserLogout'
  | 'CameraOffline'
  | 'CameraReconnected'
  | 'MotionDetected'
  | 'RecordingStarted'
  | 'RecordingStopped'
  | 'StorageFull'
  | 'RecordingFailure'

export interface DashboardMetrics {
  totalCameras: number
  onlineCameras: number
  offlineCameras: number
  disabledCameras: number
  activeLiveStreams: number
  activeRecordings: number
  activeUsers: number
  systemUptimeSeconds: number
}

export interface StorageHealth {
  path: string
  status: 'Healthy' | 'Warning' | 'Critical' | 'Unavailable'
  totalBytes: number
  availableBytes: number
  usedBytes: number
  recordingBytes: number
  usedPercent: number
  error: string | null
}

export interface DashboardCamera {
  id: string
  name: string
  location: string
  hlsUrl: string
  group: string | null
  connectionStatus: CameraConnectionStatus
  recordingStatus: CameraRecordingStatus
  isEnabled: boolean
  resolution: string | null
  framesPerSecond: number | null
  lastHeartbeatAt: string | null
  lastCheckedAt: string | null
  lastConnectionError: string | null
}

export interface DashboardEvent {
  id: string
  type: SystemEventType
  timestamp: string
  cameraId: string | null
  cameraName: string | null
  severity: EventSeverity
  description: string
  status: EventStatus
}

export interface OperatorActivity {
  id: string
  userId: string
  displayName: string
  type: SystemEventType | null
  action: string
  timestamp: string
  description: string
}

export interface CommandCenterSnapshot {
  generatedAt: string
  metrics: DashboardMetrics
  storage: StorageHealth
  cameraHealth: DashboardCamera[]
  offlineCameras: DashboardCamera[]
  recentEvents: DashboardEvent[]
  recordingFailures: DashboardEvent[]
  activeAlarms: DashboardEvent[]
  recentIncidents: DashboardEvent[]
  operatorActivity: OperatorActivity[]
}

export type RealtimeStatus =
  | 'Connecting'
  | 'Live'
  | 'Reconnecting'
  | 'Polling'
