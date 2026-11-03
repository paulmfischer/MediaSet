# Build stage: install all dependencies and generate the stylesheet (public/app.css)
FROM node:24.22.1-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci && \
    npm cache clean --force

COPY . .
RUN npm run build

# Runtime stage: Node runs the TypeScript sources directly (no compiled output)
FROM node:24.22.1-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -S mediaset && adduser -S mediaset -G mediaset

COPY package*.json ./
RUN npm ci --omit=dev && \
    npm cache clean --force

COPY --from=build /app/server.ts /app/tsconfig.json ./
COPY --from=build /app/app ./app
COPY --from=build /app/public ./public

RUN chown -R mediaset:mediaset /app

USER mediaset

EXPOSE 3000

ENV NODE_ENV=production

# Footer version, read at run time. Pass --build-arg APP_VERSION=x.y.z to bake it into the image;
# a container can override it with an APP_VERSION environment variable.
ARG APP_VERSION=0.0.0-local
ENV APP_VERSION=${APP_VERSION}

# Run node directly (not through npm) so SIGTERM from `docker stop` reaches the server.
CMD ["node", "--import", "remix/node-tsx", "server.ts"]
