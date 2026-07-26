export interface AuditLog {
  id: string
  timestamp: string
  userId: string
  actorUsername: string
  action: string
  resourceType: string
  resourceId: string | null
  description: string
}

export interface AuditSearchResult {
  matchingCount: number
  items: AuditLog[]
}
