FROM alpine:3.22

RUN apk add --no-cache ffmpeg font-dejavu

COPY entrypoint.sh /usr/local/bin/camera-generator
RUN chmod +x /usr/local/bin/camera-generator

ENTRYPOINT ["/usr/local/bin/camera-generator"]

