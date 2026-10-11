# PWA Angular servida por nginx. La compilación es independiente de la arquitectura.
FROM --platform=$BUILDPLATFORM node:24-alpine AS build
WORKDIR /src
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci
COPY apps/web/ ./
RUN npx ng build --configuration production

FROM nginx:1.29-alpine AS final
COPY infra/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist/web/browser /usr/share/nginx/html
ENV API_URL=http://api:8080
EXPOSE 8080
