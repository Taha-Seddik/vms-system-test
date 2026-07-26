import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  CircularProgress,
  Divider,
  Drawer,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import { ApiError, apiRequest } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { AppRole, ManagedCamera } from '../types/auth'
import type { ManagedUser } from '../types/user'

interface UserForm {
  username: string
  displayName: string
  password: string
  role: AppRole
  isEnabled: boolean
  assignedCameraIds: string[]
}

const emptyForm: UserForm = {
  username: '',
  displayName: '',
  password: '',
  role: 'Viewer',
  isEnabled: true,
  assignedCameraIds: [],
}

export function UsersPage() {
  const { accessToken, user: currentUser } = useAuth()
  const [users, setUsers] = useState<ManagedUser[]>([])
  const [cameras, setCameras] = useState<ManagedCamera[]>([])
  const [search, setSearch] = useState('')
  const [role, setRole] = useState<AppRole | ''>('')
  const [enabled, setEnabled] = useState('')
  const [editing, setEditing] = useState<ManagedUser | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [form, setForm] = useState<UserForm>(emptyForm)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const query = useMemo(() => {
    const parameters = new URLSearchParams()
    if (search.trim()) parameters.set('search', search.trim())
    if (role) parameters.set('role', role)
    if (enabled) parameters.set('isEnabled', enabled)
    return parameters.toString()
  }, [enabled, role, search])

  const load = useCallback(async () => {
    try {
      const [userRows, cameraRows] = await Promise.all([
        apiRequest<ManagedUser[]>(`/api/users?${query}`, {
          accessToken: accessToken ?? undefined,
        }),
        apiRequest<ManagedCamera[]>('/api/cameras/manage', {
          accessToken: accessToken ?? undefined,
        }),
      ])
      setUsers(userRows)
      setCameras(cameraRows)
      setError(null)
    } catch (reason) {
      setError(getError(reason, 'Users could not be loaded.'))
    } finally {
      setLoading(false)
    }
  }, [accessToken, query])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 150)
    return () => window.clearTimeout(timer)
  }, [load])

  const openCreate = () => {
    setEditing(null)
    setForm(emptyForm)
    setDrawerOpen(true)
  }

  const openEdit = (item: ManagedUser) => {
    setEditing(item)
    setForm({
      username: item.username,
      displayName: item.displayName,
      password: '',
      role: item.role,
      isEnabled: item.isEnabled,
      assignedCameraIds: item.assignedCameras.map((camera) => camera.id),
    })
    setDrawerOpen(true)
  }

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      const assignedCameraIds =
        form.role === 'Viewer' ? form.assignedCameraIds : []
      if (editing) {
        await apiRequest<ManagedUser>(`/api/users/${editing.id}`, {
          method: 'PUT',
          accessToken: accessToken ?? undefined,
          body: JSON.stringify({
            displayName: form.displayName,
            role: form.role,
            isEnabled: form.isEnabled,
            assignedCameraIds,
            newPassword: form.password || null,
          }),
        })
        setNotice(`${form.username} was updated.`)
      } else {
        await apiRequest<ManagedUser>('/api/users', {
          method: 'POST',
          accessToken: accessToken ?? undefined,
          body: JSON.stringify({
            username: form.username,
            displayName: form.displayName,
            password: form.password,
            role: form.role,
            assignedCameraIds,
          }),
        })
        setNotice(`${form.username} was created.`)
      }
      setDrawerOpen(false)
      await load()
    } catch (reason) {
      setError(getError(reason, 'The user could not be saved.'))
    } finally {
      setSaving(false)
    }
  }

  const remove = async (item: ManagedUser) => {
    if (!window.confirm(`Delete ${item.username}? This cannot be undone.`)) {
      return
    }

    try {
      await apiRequest<void>(`/api/users/${item.id}`, {
        method: 'DELETE',
        accessToken: accessToken ?? undefined,
      })
      setNotice(`${item.username} was deleted.`)
      await load()
    } catch (reason) {
      setError(getError(reason, 'The user could not be deleted.'))
    }
  }

  const toggleAssignment = (cameraId: string) => {
    setForm((current) => ({
      ...current,
      assignedCameraIds: current.assignedCameraIds.includes(cameraId)
        ? current.assignedCameraIds.filter((id) => id !== cameraId)
        : [...current.assignedCameraIds, cameraId],
    }))
  }

  if (loading) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={3}>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        sx={{ justifyContent: 'space-between', gap: 2 }}
      >
        <Box>
          <Typography className="page-eyebrow">Administration</Typography>
          <Typography variant="h2" sx={{ fontSize: { xs: 32, md: 44 } }}>
            Users and permissions
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.8 }}>
            Manage roles, account state, passwords, and Viewer camera access.
          </Typography>
        </Box>
        <Button variant="contained" onClick={openCreate} sx={{ alignSelf: 'center' }}>
          Add user
        </Button>
      </Stack>

      {error && <Alert severity="error">{error}</Alert>}
      {notice && (
        <Alert severity="success" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      <Card variant="outlined">
        <CardContent>
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5}>
            <TextField
              label="Search users"
              size="small"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <SelectFilter
              label="Role"
              value={role}
              onChange={(value) => setRole(value as AppRole | '')}
              options={['Administrator', 'Operator', 'Viewer']}
            />
            <SelectFilter
              label="Status"
              value={enabled}
              onChange={setEnabled}
              options={[
                { value: 'true', label: 'Enabled' },
                { value: 'false', label: 'Disabled' },
              ]}
            />
          </Stack>
        </CardContent>
      </Card>

      <Box className="user-grid">
        {users.map((item) => (
          <Card variant="outlined" key={item.id}>
            <CardContent>
              <Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}>
                <Box sx={{ minWidth: 0 }}>
                  <Typography variant="h6" sx={{ fontWeight: 800 }}>
                    {item.displayName}
                  </Typography>
                  <Typography color="text.secondary">@{item.username}</Typography>
                </Box>
                <Chip
                  label={item.isEnabled ? 'Enabled' : 'Disabled'}
                  color={item.isEnabled ? 'success' : 'default'}
                  size="small"
                />
              </Stack>
              <Divider sx={{ my: 2 }} />
              <Stack direction="row" spacing={1} sx={{ mb: 1.5 }}>
                <Chip label={item.role} size="small" color="primary" />
                {item.role === 'Viewer' && (
                  <Chip
                    label={`${item.assignedCameras.length} cameras`}
                    size="small"
                    variant="outlined"
                  />
                )}
              </Stack>
              {item.role === 'Viewer' && (
                <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                  {item.assignedCameras.map((camera) => camera.name).join(', ')}
                </Typography>
              )}
              <Stack direction="row" spacing={1}>
                <Button variant="outlined" onClick={() => openEdit(item)}>
                  Edit
                </Button>
                <Button
                  color="error"
                  disabled={item.id === currentUser?.id}
                  onClick={() => void remove(item)}
                >
                  Delete
                </Button>
              </Stack>
            </CardContent>
          </Card>
        ))}
      </Box>

      <Drawer anchor="right" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        <Box className="user-editor">
          <Typography className="page-eyebrow">
            {editing ? 'Edit account' : 'New account'}
          </Typography>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>
            {editing ? editing.username : 'Create user'}
          </Typography>
          <Divider sx={{ my: 2 }} />
          <Stack spacing={2}>
            <TextField
              label="Username"
              value={form.username}
              disabled={editing !== null}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  username: event.target.value,
                }))
              }
            />
            <TextField
              label="Display name"
              value={form.displayName}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  displayName: event.target.value,
                }))
              }
            />
            <TextField
              label={editing ? 'New password (optional)' : 'Password'}
              type="password"
              value={form.password}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  password: event.target.value,
                }))
              }
            />
            <FormControl>
              <InputLabel id="user-role-label">Role</InputLabel>
              <Select
                labelId="user-role-label"
                label="Role"
                value={form.role}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    role: event.target.value as AppRole,
                    assignedCameraIds:
                      event.target.value === 'Viewer'
                        ? current.assignedCameraIds
                        : [],
                  }))
                }
              >
                <MenuItem value="Administrator">Administrator</MenuItem>
                <MenuItem value="Operator">Operator</MenuItem>
                <MenuItem value="Viewer">Viewer</MenuItem>
              </Select>
            </FormControl>
            {editing && (
              <FormControlLabel
                control={
                  <Switch
                    checked={form.isEnabled}
                    disabled={editing.id === currentUser?.id}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        isEnabled: event.target.checked,
                      }))
                    }
                  />
                }
                label="Account enabled"
              />
            )}
            {form.role === 'Viewer' && (
              <Box>
                <Typography sx={{ fontWeight: 800, mb: 0.5 }}>
                  Assigned cameras
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  A Viewer needs at least one assignment before they can sign in.
                </Typography>
                <Stack sx={{ mt: 1 }}>
                  {cameras.map((camera) => (
                    <FormControlLabel
                      key={camera.id}
                      control={
                        <Checkbox
                          checked={form.assignedCameraIds.includes(camera.id)}
                          onChange={() => toggleAssignment(camera.id)}
                        />
                      }
                      label={`${camera.name} · ${camera.location}`}
                    />
                  ))}
                </Stack>
              </Box>
            )}
            <Stack direction="row" spacing={1}>
              <Button
                variant="contained"
                disabled={
                  saving
                  || !form.username.trim()
                  || !form.displayName.trim()
                  || (!editing && !form.password)
                }
                onClick={() => void save()}
              >
                {saving ? 'Saving…' : 'Save user'}
              </Button>
              <Button onClick={() => setDrawerOpen(false)}>Cancel</Button>
            </Stack>
          </Stack>
        </Box>
      </Drawer>
    </Stack>
  )
}

function SelectFilter({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: (string | { value: string; label: string })[]
}) {
  const id = `user-filter-${label.toLowerCase()}`
  return (
    <FormControl size="small" sx={{ minWidth: 160 }}>
      <InputLabel id={id}>{label}</InputLabel>
      <Select
        labelId={id}
        label={label}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <MenuItem value="">All {label.toLowerCase()}s</MenuItem>
        {options.map((option) => {
          const value = typeof option === 'string' ? option : option.value
          const text = typeof option === 'string' ? option : option.label
          return (
            <MenuItem key={value} value={value}>
              {text}
            </MenuItem>
          )
        })}
      </Select>
    </FormControl>
  )
}

function getError(reason: unknown, fallback: string) {
  return reason instanceof ApiError ? reason.message : fallback
}
