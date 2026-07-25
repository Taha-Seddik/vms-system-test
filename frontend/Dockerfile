FROM node:24.12.0-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG VITE_API_BASE_URL=http://localhost:8080
ARG VITE_HLS_BASE_URL=http://localhost:8888
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_HLS_BASE_URL=$VITE_HLS_BASE_URL

RUN npm run build

FROM nginx:1.29.5-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80

HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=6 \
  CMD wget -q -O /dev/null http://127.0.0.1/health || exit 1

