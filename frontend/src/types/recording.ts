export type RecordingMode = 'Manual' | 'Continuous' | 'Event'
export type RecordingState = 'Recording' | 'Completed' | 'Failed'

export interface Recording {
  id: string
  cameraId: string
  cameraName: string
  mode: RecordingMode
  state: RecordingState
  startedAt: string
  endedAt: string | null
  durationSeconds: number | null
  fileSizeBytes: number | null
  failureReason: string | null
  triggerEventId: string | null
}

export interface RecordingCommand {
  message: string
  recording: Recording
}

export interface RecordingKeyframe {
  id: string
  timestampSeconds: number
}

export interface RecordingDetails {
  recording: Recording
  keyframes: RecordingKeyframe[]
}
