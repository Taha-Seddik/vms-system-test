import { useEffect, useState } from 'react'
import { apiRequestBlob } from '../api/client'

export function ProtectedImage({
  path,
  accessToken,
  alt,
}: {
  path: string
  accessToken: string
  alt: string
}) {
  const [source, setSource] = useState<string | null>(null)

  useEffect(() => {
    let objectUrl: string | null = null
    let active = true
    void apiRequestBlob(path, { accessToken })
      .then((blob) => {
        if (!active) return
        objectUrl = URL.createObjectURL(blob)
        setSource(objectUrl)
      })
      .catch(() => {
        if (active) setSource(null)
      })

    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [accessToken, path])

  return source ? (
    <img src={source} alt={alt} />
  ) : (
    <span className="keyframe-placeholder">Loading preview…</span>
  )
}
