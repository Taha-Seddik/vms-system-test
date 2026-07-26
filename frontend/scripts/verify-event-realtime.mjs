import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const apiBaseUrl = process.env.VMS_API_BASE_URL ?? 'http://localhost:8080'
const eventId = process.env.VMS_EVENT_ID

if (!eventId) {
  throw new Error('VMS_EVENT_ID is required.')
}

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
let handler
try {
  await connection.start()
  const notification = new Promise((resolve, reject) => {
    handler = (message) => {
      if (!message?.revision || message.reason !== 'event-closed') return
      resolve(message)
    }
    connection.on('DashboardChanged', handler)
    timeout = setTimeout(
      () => reject(new Error('Event close update was not received within 15 seconds.')),
      15_000,
    )
  })

  const closeResponse = await fetch(`${apiBaseUrl}/api/events/${eventId}/close`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${login.accessToken}` },
  })
  if (!closeResponse.ok) {
    throw new Error(`Event close failed with HTTP ${closeResponse.status}.`)
  }

  const message = await notification
  clearTimeout(timeout)
  console.log(
    `[PASS] SignalR delivered event revision ${message.revision} (${message.reason}).`,
  )
} finally {
  clearTimeout(timeout)
  if (handler) connection.off('DashboardChanged', handler)
  await connection.stop()
  await fetch(`${apiBaseUrl}/api/auth/logout`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${login.accessToken}` },
  })
}
