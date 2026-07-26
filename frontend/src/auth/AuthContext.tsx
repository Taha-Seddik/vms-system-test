import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { apiRequest } from '../api/client'
import type {
  AppRole,
  AuthenticatedUser,
  LoginResponse,
} from '../types/auth'

const sessionKey = 'vms-auth-session'

interface StoredSession {
  accessToken: string
  expiresAt: string
  user: AuthenticatedUser
}

interface AuthContextValue {
  accessToken: string | null
  user: AuthenticatedUser | null
  isLoading: boolean
  login: (username: string, password: string) => Promise<void>
  logout: () => Promise<void>
  hasRole: (...roles: AppRole[]) => boolean
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function readStoredSession(): StoredSession | null {
  const value = sessionStorage.getItem(sessionKey)
  if (!value) {
    return null
  }

  try {
    const session = JSON.parse(value) as StoredSession
    if (
      !session.accessToken ||
      !session.user ||
      new Date(session.expiresAt).getTime() <= Date.now()
    ) {
      sessionStorage.removeItem(sessionKey)
      return null
    }
    return session
  } catch {
    sessionStorage.removeItem(sessionKey)
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(
    readStoredSession,
  )
  const [isLoading, setIsLoading] = useState(session !== null)
  const sessionAtStartup = useRef(session)

  const clearSession = useCallback(() => {
    sessionStorage.removeItem(sessionKey)
    setSession(null)
  }, [])

  useEffect(() => {
    const startupSession = sessionAtStartup.current
    if (!startupSession) {
      setIsLoading(false)
      return
    }

    const validateSession = async () => {
      try {
        const user = await apiRequest<AuthenticatedUser>('/api/auth/me', {
          accessToken: startupSession.accessToken,
        })
        const refreshed = { ...startupSession, user }
        sessionStorage.setItem(sessionKey, JSON.stringify(refreshed))
        setSession(refreshed)
      } catch {
        clearSession()
      } finally {
        setIsLoading(false)
      }
    }

    void validateSession()
  }, [clearSession])

  const login = useCallback(async (username: string, password: string) => {
    const response = await apiRequest<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    })
    sessionStorage.setItem(sessionKey, JSON.stringify(response))
    setSession(response)
  }, [])

  const logout = useCallback(async () => {
    const token = session?.accessToken
    try {
      if (token) {
        await apiRequest<void>('/api/auth/logout', {
          method: 'POST',
          accessToken: token,
        })
      }
    } finally {
      clearSession()
    }
  }, [clearSession, session?.accessToken])

  const value = useMemo<AuthContextValue>(
    () => ({
      accessToken: session?.accessToken ?? null,
      user: session?.user ?? null,
      isLoading,
      login,
      logout,
      hasRole: (...roles) =>
        session?.user ? roles.includes(session.user.role) : false,
    }),
    [isLoading, login, logout, session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// The provider and its colocated hook form one public authentication boundary.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.')
  }
  return context
}
