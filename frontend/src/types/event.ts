import type {
  EventSeverity,
  EventStatus,
  SystemEventType,
} from './dashboard'

export interface SystemEvent {
  id: string
  type: SystemEventType
  timestamp: string
  cameraId: string | null
  cameraName: string | null
  severity: EventSeverity
  description: string
  status: EventStatus
  isActiveAlarm: boolean
  isIncident: boolean
}

export interface EventSearchResult {
  generatedAt: string
  matchingCount: number
  activeAlarmCount: number
  incidentCount: number
  items: SystemEvent[]
}
