export const hlsBaseUrl =
  import.meta.env.VITE_HLS_BASE_URL ?? 'http://localhost:8888'

export function resolveHlsSource(path: string) {
  return path.startsWith('http') ? path : `${hlsBaseUrl}${path}`
}
