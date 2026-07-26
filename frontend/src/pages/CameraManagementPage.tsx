import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { apiRequest, ApiError } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type {
  CameraConnectionTest,
  CameraGroup,
  ManagedCamera,
} from '../types/auth'

interface CameraFormState {
  id: string
  name: string
  location: string
  rtspUrl: string
  hlsPath: string
  groupId: string
  isEnabled: boolean
}

const emptyCamera: CameraFormState = {
  id: '',
  name: '',
  location: '',
  rtspUrl: 'rtsp://mediamtx:8554/',
  hlsPath: '/',
  groupId: '',
  isEnabled: true,
}

export function CameraManagementPage() {
  const { accessToken } = useAuth()
  const [cameras, setCameras] = useState<ManagedCamera[]>([])
  const [groups, setGroups] = useState<CameraGroup[]>([])
  const [cameraForm, setCameraForm] = useState<CameraFormState>(emptyCamera)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [groupName, setGroupName] = useState('')
  const [groupDescription, setGroupDescription] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const authorization = useMemo(
    () => ({ accessToken: accessToken ?? undefined }),
    [accessToken],
  )

  const fetchManagementData = useCallback(
    () =>
      Promise.all([
        apiRequest<ManagedCamera[]>('/api/cameras/manage', authorization),
        apiRequest<CameraGroup[]>('/api/camera-groups', authorization),
      ]),
    [authorization],
  )

  const load = async () => {
    try {
      const [cameraResult, groupResult] = await fetchManagementData()
      setCameras(cameraResult)
      setGroups(groupResult)
      setError(null)
    } catch {
      setError('Camera management data could not be loaded.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    const poll = async () => {
      try {
        const [cameraResult, groupResult] = await fetchManagementData()
        if (active) {
          setCameras(cameraResult)
          setGroups(groupResult)
          setError(null)
          setIsLoading(false)
        }
      } catch {
        if (active) {
          setError('Camera management data could not be loaded.')
          setIsLoading(false)
        }
      }
    }
    void poll()
    const interval = window.setInterval(() => void poll(), 15000)
    return () => {
      active = false
      window.clearInterval(interval)
    }
  }, [fetchManagementData])

  const openCreate = () => {
    setEditingId(null)
    setCameraForm(emptyCamera)
    setDialogOpen(true)
  }

  const openEdit = (camera: ManagedCamera) => {
    setEditingId(camera.id)
    setCameraForm({
      id: camera.id,
      name: camera.name,
      location: camera.location,
      rtspUrl: camera.rtspUrl,
      hlsPath: camera.hlsUrl,
      groupId: camera.group?.id ?? '',
      isEnabled: camera.isEnabled,
    })
    setDialogOpen(true)
  }

  const saveCamera = async () => {
    setIsSaving(true)
    setError(null)
    try {
      const body = editingId
        ? {
            name: cameraForm.name,
            location: cameraForm.location,
            rtspUrl: cameraForm.rtspUrl,
            hlsPath: cameraForm.hlsPath,
            groupId: cameraForm.groupId || null,
          }
        : {
            ...cameraForm,
            groupId: cameraForm.groupId || null,
          }
      await apiRequest<ManagedCamera>(
        editingId ? `/api/cameras/${editingId}` : '/api/cameras',
        {
          method: editingId ? 'PUT' : 'POST',
          body: JSON.stringify(body),
          ...authorization,
        },
      )
      setDialogOpen(false)
      setNotice(editingId ? 'Camera updated.' : 'Camera created.')
      await load()
    } catch (requestError) {
      setError(
        requestError instanceof ApiError
          ? requestError.message
          : 'The camera could not be saved.',
      )
    } finally {
      setIsSaving(false)
    }
  }

  const setEnabled = async (camera: ManagedCamera) => {
    try {
      await apiRequest<ManagedCamera>(
        `/api/cameras/${camera.id}/enabled`,
        {
          method: 'PATCH',
          body: JSON.stringify({ isEnabled: !camera.isEnabled }),
          ...authorization,
        },
      )
      setNotice(`${camera.name} ${camera.isEnabled ? 'disabled' : 'enabled'}.`)
      await load()
    } catch {
      setError('The camera state could not be changed.')
    }
  }

  const testConnection = async (camera: ManagedCamera) => {
    setNotice(`Testing ${camera.name}...`)
    try {
      const result = await apiRequest<CameraConnectionTest>(
        `/api/cameras/${camera.id}/test-connection`,
        { method: 'POST', ...authorization },
      )
      setNotice(
        result.succeeded
          ? `${camera.name}: ${result.codec ?? 'video'} ${result.resolution ?? ''} at ${result.framesPerSecond ?? '?'} FPS (${result.elapsedMilliseconds} ms).`
          : `${camera.name} is offline: ${result.error ?? 'connection failed'}`,
      )
      await load()
    } catch {
      setError('The connection test could not be completed.')
    }
  }

  const deleteCamera = async (camera: ManagedCamera) => {
    if (!window.confirm(`Delete ${camera.name}? This cannot be undone.`)) {
      return
    }
    try {
      await apiRequest<void>(`/api/cameras/${camera.id}`, {
        method: 'DELETE',
        ...authorization,
      })
      setNotice(`${camera.name} deleted.`)
      await load()
    } catch {
      setError('The camera could not be deleted.')
    }
  }

  const createGroup = async () => {
    try {
      await apiRequest<CameraGroup>('/api/camera-groups', {
        method: 'POST',
        body: JSON.stringify({
          name: groupName,
          description: groupDescription || null,
        }),
        ...authorization,
      })
      setGroupName('')
      setGroupDescription('')
      setNotice('Camera group created.')
      await load()
    } catch (requestError) {
      setError(
        requestError instanceof ApiError
          ? requestError.message
          : 'The group could not be created.',
      )
    }
  }

  const renameGroup = async (group: CameraGroup) => {
    const name = window.prompt('Camera group name', group.name)?.trim()
    if (!name) {
      return
    }
    try {
      await apiRequest<CameraGroup>(`/api/camera-groups/${group.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          name,
          description: group.description,
        }),
        ...authorization,
      })
      setNotice('Camera group updated.')
      await load()
    } catch {
      setError('The group could not be updated.')
    }
  }

  const deleteGroup = async (group: CameraGroup) => {
    if (
      !window.confirm(
        `Delete ${group.name}? Its ${group.cameraCount} camera(s) will become ungrouped.`,
      )
    ) {
      return
    }
    try {
      await apiRequest<void>(`/api/camera-groups/${group.id}`, {
        method: 'DELETE',
        ...authorization,
      })
      setNotice('Camera group deleted.')
      await load()
    } catch {
      setError('The group could not be deleted.')
    }
  }

  if (isLoading) {
    return (
      <Box className="content-loader">
        <CircularProgress />
      </Box>
    )
  }

  return (
    <Stack spacing={4}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        sx={{ justifyContent: 'space-between', gap: 2 }}
      >
        <Box>
          <Chip label="Administrator only" color="primary" size="small" />
          <Typography variant="h2" sx={{ mt: 2, fontSize: { xs: 32, md: 42 } }}>
            Camera management
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 1 }}>
            Persisted RTSP sources, grouping, health, and lifecycle controls.
          </Typography>
        </Box>
        <Button variant="contained" onClick={openCreate} sx={{ alignSelf: 'start' }}>
          Add camera
        </Button>
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {notice && (
        <Alert severity="info" onClose={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      <Box className="camera-management-grid">
        {cameras.map((camera) => (
          <Card key={camera.id} variant="outlined">
            <CardContent>
              <Stack
                direction="row"
                sx={{ justifyContent: 'space-between', gap: 2 }}
              >
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 800 }}>
                    {camera.name}
                  </Typography>
                  <Typography color="text.secondary">
                    {camera.location} · {camera.group?.name ?? 'Ungrouped'}
                  </Typography>
                </Box>
                <Chip
                  label={camera.connectionStatus}
                  color={
                    camera.connectionStatus === 'Online'
                      ? 'success'
                      : camera.connectionStatus === 'Offline'
                        ? 'error'
                        : 'default'
                  }
                  size="small"
                />
              </Stack>

              <Stack className="camera-facts" spacing={0.5}>
                <Typography variant="body2">
                  <strong>Source:</strong> {camera.rtspUrl}
                </Typography>
                <Typography variant="body2">
                  <strong>Video:</strong> {camera.resolution ?? 'Pending'} ·{' '}
                  {camera.framesPerSecond ?? '?'} FPS · {camera.recordingStatus}
                </Typography>
                <Typography variant="body2">
                  <strong>Heartbeat:</strong>{' '}
                  {camera.lastHeartbeatAt
                    ? new Date(camera.lastHeartbeatAt).toLocaleString()
                    : 'Not received'}
                </Typography>
              </Stack>

              {camera.lastConnectionError && (
                <Alert severity="warning" sx={{ mt: 2 }}>
                  {camera.lastConnectionError}
                </Alert>
              )}

              <Stack
                direction="row"
                spacing={1}
                sx={{ mt: 2, alignItems: 'center', flexWrap: 'wrap' }}
              >
                <Tooltip title={camera.isEnabled ? 'Disable camera' : 'Enable camera'}>
                  <Switch
                    checked={camera.isEnabled}
                    onChange={() => void setEnabled(camera)}
                    slotProps={{
                      input: { 'aria-label': `Enable ${camera.name}` },
                    }}
                  />
                </Tooltip>
                <Button size="small" onClick={() => void testConnection(camera)}>
                  Test
                </Button>
                <Button size="small" onClick={() => openEdit(camera)}>
                  Edit
                </Button>
                <Button
                  size="small"
                  color="error"
                  onClick={() => void deleteCamera(camera)}
                >
                  Delete
                </Button>
              </Stack>
            </CardContent>
          </Card>
        ))}
      </Box>

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h5" sx={{ fontWeight: 800 }}>
            Camera groups
          </Typography>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={2}
            sx={{ mt: 2 }}
          >
            <TextField
              label="Group name"
              value={groupName}
              onChange={(event) => setGroupName(event.target.value)}
              size="small"
            />
            <TextField
              label="Description"
              value={groupDescription}
              onChange={(event) => setGroupDescription(event.target.value)}
              size="small"
              sx={{ flexGrow: 1 }}
            />
            <Button
              variant="outlined"
              disabled={groupName.trim().length < 2}
              onClick={() => void createGroup()}
            >
              Create group
            </Button>
          </Stack>
          <Stack spacing={1} sx={{ mt: 2 }}>
            {groups.map((group) => (
              <Stack
                key={group.id}
                direction="row"
                className="group-row"
                sx={{ alignItems: 'center', gap: 1 }}
              >
                <Box sx={{ flexGrow: 1 }}>
                  <Typography sx={{ fontWeight: 700 }}>{group.name}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {group.description ?? 'No description'} · {group.cameraCount}{' '}
                    camera(s)
                  </Typography>
                </Box>
                <IconButton
                  aria-label={`Rename ${group.name}`}
                  onClick={() => void renameGroup(group)}
                >
                  <span aria-hidden="true">✎</span>
                </IconButton>
                <IconButton
                  aria-label={`Delete ${group.name}`}
                  color="error"
                  onClick={() => void deleteGroup(group)}
                >
                  <span aria-hidden="true">×</span>
                </IconButton>
              </Stack>
            ))}
          </Stack>
        </CardContent>
      </Card>

      <Dialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>{editingId ? 'Edit camera' : 'Add camera'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField
              label="Camera id"
              value={cameraForm.id}
              disabled={Boolean(editingId)}
              helperText="Lowercase letters, numbers, and hyphens"
              onChange={(event) =>
                setCameraForm({ ...cameraForm, id: event.target.value })
              }
            />
            <TextField
              label="Camera name"
              value={cameraForm.name}
              onChange={(event) =>
                setCameraForm({ ...cameraForm, name: event.target.value })
              }
            />
            <TextField
              label="Location"
              value={cameraForm.location}
              onChange={(event) =>
                setCameraForm({ ...cameraForm, location: event.target.value })
              }
            />
            <TextField
              label="RTSP URL"
              value={cameraForm.rtspUrl}
              onChange={(event) =>
                setCameraForm({ ...cameraForm, rtspUrl: event.target.value })
              }
            />
            <TextField
              label="HLS path"
              value={cameraForm.hlsPath}
              onChange={(event) =>
                setCameraForm({ ...cameraForm, hlsPath: event.target.value })
              }
            />
            <FormControl>
              <InputLabel id="camera-group-label">Camera group</InputLabel>
              <Select
                labelId="camera-group-label"
                label="Camera group"
                value={cameraForm.groupId}
                onChange={(event) =>
                  setCameraForm({ ...cameraForm, groupId: event.target.value })
                }
              >
                <MenuItem value="">Ungrouped</MenuItem>
                {groups.map((group) => (
                  <MenuItem key={group.id} value={group.id}>
                    {group.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            disabled={
              isSaving ||
              !cameraForm.id ||
              !cameraForm.name ||
              !cameraForm.location ||
              !cameraForm.rtspUrl ||
              !cameraForm.hlsPath
            }
            onClick={() => void saveCamera()}
          >
            {isSaving ? 'Saving…' : 'Save camera'}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}
