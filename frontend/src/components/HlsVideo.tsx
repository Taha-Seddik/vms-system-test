import Hls from 'hls.js'
import {
  forwardRef,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react'

interface HlsVideoProps {
  source: string
  title: string
  zoom: number
}

export const HlsVideo = forwardRef<HTMLVideoElement, HlsVideoProps>(
  function HlsVideo({ source, title, zoom }, forwardedRef) {
    const videoRef = useRef<HTMLVideoElement>(null)
    const [error, setError] = useState<string | null>(null)

    useImperativeHandle(forwardedRef, () => videoRef.current!, [])

    useEffect(() => {
      const video = videoRef.current
      if (!video) {
        return
      }

      setError(null)
      video.crossOrigin = 'anonymous'
      let hls: Hls | null = null

      if (Hls.isSupported()) {
        hls = new Hls({
          lowLatencyMode: true,
          backBufferLength: 20,
          liveSyncDurationCount: 2,
        })
        hls.loadSource(source)
        hls.attachMedia(video)
        hls.on(Hls.Events.MANIFEST_PARSED, () => {
          void video.play().catch(() => undefined)
        })
        hls.on(Hls.Events.ERROR, (_, data) => {
          if (!data.fatal) {
            return
          }

          if (data.type === Hls.ErrorTypes.NETWORK_ERROR) {
            setError('Stream is reconnecting…')
            hls?.startLoad()
          } else if (data.type === Hls.ErrorTypes.MEDIA_ERROR) {
            setError('Video decoder is recovering…')
            hls?.recoverMediaError()
          } else {
            setError('Live stream is unavailable.')
            hls?.destroy()
            hls = null
          }
        })
      } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = source
        const play = () => void video.play().catch(() => undefined)
        video.addEventListener('loadedmetadata', play)
        return () => {
          video.removeEventListener('loadedmetadata', play)
          video.removeAttribute('src')
          video.load()
        }
      } else {
        setError('This browser does not support HLS playback.')
      }

      return () => {
        hls?.destroy()
        video.removeAttribute('src')
      }
    }, [source])

    return (
      <>
        <video
          ref={videoRef}
          className="live-video"
          aria-label={`${title} live video`}
          muted
          playsInline
          autoPlay
          style={{ transform: `scale(${zoom})` }}
        />
        {error && <span className="live-video-error">{error}</span>}
      </>
    )
  },
)
