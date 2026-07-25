#!/bin/sh
set -eu

: "${CAMERA_PATH:?CAMERA_PATH is required}"
: "${CAMERA_LABEL:?CAMERA_LABEL is required}"

CAMERA_RESOLUTION="${CAMERA_RESOLUTION:-640x360}"
CAMERA_FPS="${CAMERA_FPS:-10}"
CAMERA_HUE="${CAMERA_HUE:-0}"
PUBLISH_URL="${PUBLISH_URL:-rtsp://mediamtx:8554/${CAMERA_PATH}}"
GOP_SIZE=$((CAMERA_FPS * 2))

stop_publisher() {
  if [ -n "${publisher_pid:-}" ]; then
    kill -TERM "$publisher_pid" 2>/dev/null || true
    wait "$publisher_pid" 2>/dev/null || true
  fi
  exit 0
}

trap stop_publisher INT TERM

while true; do
  echo "Publishing ${CAMERA_LABEL} to ${PUBLISH_URL}"

  ffmpeg \
    -hide_banner \
    -loglevel warning \
    -re \
    -f lavfi \
    -i "testsrc2=size=${CAMERA_RESOLUTION}:rate=${CAMERA_FPS}" \
    -vf "hue=h=${CAMERA_HUE},drawtext=fontfile=/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf:text='${CAMERA_LABEL}':fontcolor=white:fontsize=28:box=1:boxcolor=black@0.65:x=24:y=24" \
    -c:v libx264 \
    -preset ultrafast \
    -tune zerolatency \
    -pix_fmt yuv420p \
    -g "$GOP_SIZE" \
    -keyint_min "$GOP_SIZE" \
    -sc_threshold 0 \
    -b:v 500k \
    -maxrate 500k \
    -bufsize 1000k \
    -an \
    -rtsp_transport tcp \
    -f rtsp \
    "$PUBLISH_URL" &

  publisher_pid=$!
  set +e
  wait "$publisher_pid"
  exit_code=$?
  set -e
  publisher_pid=""

  echo "Publisher exited with code ${exit_code}; retrying in 2 seconds."
  sleep 2
done
