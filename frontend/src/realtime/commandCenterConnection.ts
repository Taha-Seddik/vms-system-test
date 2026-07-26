import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { apiBaseUrl } from '../api/client'
import type { RealtimeStatus } from '../types/dashboard'

interface CommandCenterConnectionOptions {
  accessToken: string
  onChanged: () => void
  onStatusChanged: (status: RealtimeStatus) => void
}

export function connectCommandCenter({
  accessToken,
  onChanged,
  onStatusChanged,
}: CommandCenterConnectionOptions) {
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/command-center`, {
      accessTokenFactory: () => accessToken,
      withCredentials: false,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('DashboardChanged', onChanged)
  connection.onreconnecting(() => onStatusChanged('Reconnecting'))
  connection.onreconnected(() => {
    onStatusChanged('Live')
    onChanged()
  })
  connection.onclose(() => onStatusChanged('Polling'))

  void connection
    .start()
    .then(() => onStatusChanged('Live'))
    .catch(() => onStatusChanged('Polling'))

  return () => {
    connection.off('DashboardChanged', onChanged)
    if (connection.state !== HubConnectionState.Disconnected) {
      void connection.stop()
    }
  }
}
