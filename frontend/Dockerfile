# Runtime-only image: expects `npm run build` to have already been run on the host (see
# the root README Docker section) — npm install/build doesn't happen inside this build
# because this network's proxy blocks registry.npmjs.org from containers. Just serves the
# pre-built static output with nginx, which also reverse-proxies /api/* to the backend
# container so the SPA can keep using its relative, same-origin "/api" base URL (see
# src/environments/environment.ts) with no CORS configuration needed.
FROM nginx:1.27-alpine AS final
COPY dist/frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
