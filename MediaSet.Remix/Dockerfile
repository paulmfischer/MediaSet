# Build stage: install all dependencies and generate the stylesheet (public/app.css)
FROM node:24.22.1-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci && \
    npm cache clean --force

COPY . .
RUN npm run build

# Trim stage: install the production dependencies once and prune what the server never loads
# (see scripts/trim-runtime-deps.mjs). This is its own stage because deleting files in a later
# layer of the runtime stage would not shrink the image.
FROM node:24.22.1-alpine AS trim
WORKDIR /app

COPY package*.json ./
# `--omit=dev` still installs playwright and playwright-core: @remix-run/test declares playwright
# as an optional peer dependency and npm installs it. Only the test runner uses it, so the trim
# script removes both.
RUN npm ci --omit=dev && \
    npm cache clean --force

COPY scripts/trim-runtime-deps.mjs ./scripts/
COPY app/ui/icons.ts ./app/ui/icons.ts
RUN MEDIASET_IMAGE_BUILD=1 node scripts/trim-runtime-deps.mjs node_modules app/ui/icons.ts

# Runtime stage: Node runs the TypeScript sources directly (no compiled output)
FROM node:24.22.1-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -S mediaset && adduser -S mediaset -G mediaset

# Own /app so the install and copies below need no recursive chown layer
RUN chown mediaset:mediaset /app
USER mediaset

COPY --chown=mediaset:mediaset package*.json ./
COPY --from=trim --chown=mediaset:mediaset /app/node_modules ./node_modules

COPY --from=build --chown=mediaset:mediaset /app/server.ts /app/tsconfig.json ./
COPY --from=build --chown=mediaset:mediaset /app/app ./app
COPY --from=build --chown=mediaset:mediaset /app/public ./public

EXPOSE 3000

ENV NODE_ENV=production

# Footer version, read at run time. Pass --build-arg APP_VERSION=x.y.z to bake it into the image;
# a container can override it with an APP_VERSION environment variable.
ARG APP_VERSION=0.0.0-local
ENV APP_VERSION=${APP_VERSION}

# Run node directly (not through npm) so SIGTERM from `docker stop` reaches the server.
CMD ["node", "--import", "remix/node-tsx", "server.ts"]
