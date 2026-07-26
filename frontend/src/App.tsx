import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './layout/AppLayout'
import { ProtectedRoute } from './auth/ProtectedRoute'
import { ActivityPage } from './pages/ActivityPage'
import { CamerasPage } from './pages/CamerasPage'
import { CameraManagementPage } from './pages/CameraManagementPage'
import { LoginPage } from './pages/LoginPage'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppLayout />}>
          <Route index element={<CamerasPage />} />
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
