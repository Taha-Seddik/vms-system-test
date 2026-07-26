import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { AuditSearchResult } from '../types/audit'
import type { AuthActivity } from '../types/auth'
import type { ManagedUser } from '../types/user'

const resourceTypes = [
  'Session',
  'Camera',
  'CameraGroup',
  'Recording',
  'Event',
  'User',
]
const actions = [
  'Login',
  'Logout',
  'Created',
  'Updated',
  'Deleted',
  'Executed',
  'Closed',
]

export function ActivityPage() {
  const { accessToken } = useAuth()
  const [audit, setAudit] = useState<AuditSearchResult | null>(null)
  const [activity, setActivity] = useState<AuthActivity | null>(null)
  const [users, setUsers] = useState<ManagedUser[]>([])
  const [userId, setUserId] = useState('')
  const [resourceType, setResourceType] = useState('')
  const [action, setAction] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [error, setError] = useState<string | null>(null)

  const query = useMemo(() => {
    const parameters = new URLSearchParams({ take: '200' })
    if (userId) parameters.set('userId', userId)
    if (resourceType) parameters.set('resourceType', resourceType)
    if (action) parameters.set('action', action)
    if (fromDate) parameters.set('from', `${fromDate}T00:00:00Z`)
    if (toDate) parameters.set('to', `${toDate}T23:59:59Z`)
    return parameters.toString()
  }, [action, fromDate, resourceType, toDate, userId])

  const load = useCallback(async () => {
    try {
      const [logs, authActivity, userRows] = await Promise.all([
        apiRequest<AuditSearchResult>(`/api/audit-logs?${query}`, {
          accessToken: accessToken ?? undefined,
        }),
        apiRequest<AuthActivity>('/api/auth/activity', {
          accessToken: accessToken ?? undefined,
        }),
        apiRequest<ManagedUser[]>('/api/users', {
          accessToken: accessToken ?? undefined,
        }),
      ])
      setAudit(logs)
      setActivity(authActivity)
      setUsers(userRows)
      setError(null)
    } catch {
      setError('Audit activity could not be loaded.')
    }
  }, [accessToken, query])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  return (
    <Stack spacing={3}>
      <Box>
        <Chip label="Administrator only" color="secondary" size="small" />
        <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 }, mt: 2 }}>
          Audit activity
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          Trace successful write operations and authentication activity.
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {!audit && !error ? (
        <Box className="content-loader">
          <CircularProgress />
        </Box>
      ) : (
        audit && (
          <>
            <Box className="audit-summary-grid">
              <SummaryCard
                label="Matching audit records"
                value={audit.matchingCount}
              />
              <SummaryCard
                label="Recently active sessions"
                value={activity?.activeSessions ?? 0}
              />
            </Box>

            <Card variant="outlined">
              <CardContent>
                <Box className="audit-filter-grid">
                  <AuditSelect
                    label="Actor"
                    value={userId}
                    onChange={setUserId}
                    options={users.map((user) => ({
                      value: user.id,
                      label: user.username,
                    }))}
                  />
                  <AuditSelect
                    label="Resource"
                    value={resourceType}
                    onChange={setResourceType}
                    options={resourceTypes.map((value) => ({
                      value,
                      label: value,
                    }))}
                  />
                  <AuditSelect
                    label="Action"
                    value={action}
                    onChange={setAction}
                    options={actions.map((value) => ({
                      value,
                      label: value,
                    }))}
                  />
                  <TextField
                    label="From date"
                    type="date"
                    size="small"
                    value={fromDate}
                    onChange={(event) => setFromDate(event.target.value)}
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                  <TextField
                    label="To date"
                    type="date"
                    size="small"
                    value={toDate}
                    onChange={(event) => setToDate(event.target.value)}
                    slotProps={{ inputLabel: { shrink: true } }}
                  />
                </Box>
              </CardContent>
            </Card>

            <Stack spacing={1}>
              {audit.items.map((item) => (
                <Card key={item.id} variant="outlined">
                  <CardContent>
                    <Stack
                      direction={{ xs: 'column', sm: 'row' }}
                      sx={{ justifyContent: 'space-between', gap: 1.5 }}
                    >
                      <Box>
                        <Stack direction="row" spacing={0.7} sx={{ mb: 1 }}>
                          <Chip label={item.action} size="small" color="primary" />
                          <Chip
                            label={item.resourceType}
                            size="small"
                            variant="outlined"
                          />
                        </Stack>
                        <Typography sx={{ fontWeight: 750 }}>
                          {item.actorUsername}
                        </Typography>
                        <Typography color="text.secondary" variant="body2">
                          {item.description}
                        </Typography>
                      </Box>
                      <Typography color="text.secondary" variant="caption">
                        {new Date(item.timestamp).toLocaleString()}
                      </Typography>
                    </Stack>
                  </CardContent>
                </Card>
              ))}
              {audit.items.length === 0 && (
                <Card variant="outlined">
                  <CardContent>
                    <Typography color="text.secondary">
                      No audit records match the selected filters.
                    </Typography>
                  </CardContent>
                </Card>
              )}
            </Stack>
          </>
        )
      )}
    </Stack>
  )
}

function SummaryCard({
  label,
  value,
}: {
  label: string
  value: number
}) {
  return (
    <Card variant="outlined">
      <CardContent>
        <Typography variant="overline" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h3" sx={{ fontWeight: 800 }}>
          {value}
        </Typography>
      </CardContent>
    </Card>
  )
}

function AuditSelect({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
}) {
  const id = `audit-${label.toLowerCase()}`
  return (
    <FormControl size="small">
      <InputLabel id={id}>{label}</InputLabel>
      <Select
        labelId={id}
        label={label}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <MenuItem value="">All {label.toLowerCase()}s</MenuItem>
        {options.map((option) => (
          <MenuItem key={option.value} value={option.value}>
            {option.label}
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  )
}
