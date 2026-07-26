import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const apiBaseUrl = process.env.VMS_API_BASE_URL ?? 'http://localhost:8080'

const loginResponse = await fetch(`${apiBaseUrl}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'operator',
    password: 'Operator123!',
  }),
})

if (!loginResponse.ok) {
  throw new Error(`Operator login failed with HTTP ${loginResponse.status}.`)
}

const login = await loginResponse.json()
const connection = new HubConnectionBuilder()
  .withUrl(`${apiBaseUrl}/hubs/command-center`, {
    accessTokenFactory: () => login.accessToken,
    withCredentials: false,
  })
  .withAutomaticReconnect()
  .configureLogging(LogLevel.Error)
  .build()

let timeout
let notificationHandler
try {
  await connection.start()

  const notification = new Promise((resolve, reject) => {
    notificationHandler = (message) => {
      if (!message?.revision || message.reason !== 'camera-health-tested') {
        return
      }

      connection.off('DashboardChanged', notificationHandler)
      resolve(message)
    }
    timeout = setTimeout(
      () => reject(new Error('DashboardChanged was not received within 15 seconds.')),
      15_000,
    )
    connection.on('DashboardChanged', notificationHandler)
  })

  const probeResponse = await fetch(
    `${apiBaseUrl}/api/cameras/camera-1/test-connection`,
    {
      method: 'POST',
      headers: { Authorization: `Bearer ${login.accessToken}` },
    },
  )
  if (!probeResponse.ok) {
    throw new Error(
      `Camera probe trigger failed with HTTP ${probeResponse.status}.`,
    )
  }

  const message = await notification
  clearTimeout(timeout)

  console.log(
    `[PASS] SignalR delivered dashboard revision ${message.revision} (${message.reason}).`,
  )
} finally {
  clearTimeout(timeout)
  if (notificationHandler) {
    connection.off('DashboardChanged', notificationHandler)
  }
  await connection.stop()
}
