import { render } from '@testing-library/react'
import { HlsVideo } from './HlsVideo'

const hlsState = vi.hoisted(() => ({
  configuration: null as {
    xhrSetup?: (request: XMLHttpRequest) => void
  } | null,
}))

vi.mock('hls.js', () => {
  class MockHls {
    static isSupported() {
      return true
    }

    static Events = {
      MANIFEST_PARSED: 'manifest-parsed',
      ERROR: 'error',
    }

    static ErrorTypes = {
      NETWORK_ERROR: 'network-error',
      MEDIA_ERROR: 'media-error',
    }

    constructor(configuration: typeof hlsState.configuration) {
      hlsState.configuration = configuration
    }

    loadSource() {}
    attachMedia() {}
    on() {}
    startLoad() {}
    recoverMediaError() {}
    destroy() {}
  }

  return { default: MockHls }
})

it('adds the current JWT to every HLS request', () => {
  render(
    <HlsVideo
      source="http://localhost:8888/camera-1/index.m3u8"
      title="Entrance"
      zoom={1}
      accessToken="signed-test-token"
    />,
  )

  const setRequestHeader = vi.fn()
  hlsState.configuration?.xhrSetup?.({
    setRequestHeader,
  } as unknown as XMLHttpRequest)

  expect(setRequestHeader).toHaveBeenCalledWith(
    'Authorization',
    'Bearer signed-test-token',
  )
})
