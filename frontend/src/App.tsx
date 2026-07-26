import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './layout/AppLayout'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { ActivityPage } from './pages/ActivityPage'
import { CamerasPage } from './pages/CamerasPage'
import { CameraManagementPage } from './pages/CameraManagementPage'
import { CommandCenterPage } from './pages/CommandCenterPage'
import { LoginPage } from './pages/LoginPage'
import { PlaybackPage } from './pages/PlaybackPage'
import { useAuth } from './auth/AuthContext'

function RoleHome() {
  const { user } = useAuth()
  return (
    <Navigate
      to={user?.role === 'Viewer' ? '/cameras' : '/command-center'}
      replace
    />
  )
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route index element={<RoleHome />} />
          <Route path="cameras" element={<CamerasPage />} />
          <Route
            element={
              <ProtectedRoute
                allowedRoles={['Administrator', 'Operator']}
              />
            }
          >
            <Route path="command-center" element={<CommandCenterPage />} />
            <Route path="playback" element={<PlaybackPage />} />
          </Route>
          <Route
            element={<ProtectedRoute allowedRoles={['Administrator']} />}
          >
            <Route path="activity" element={<ActivityPage />} />
            <Route path="manage/cameras" element={<CameraManagementPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
