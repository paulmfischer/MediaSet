# Build stage: only needed to generate the Tailwind stylesheet (Remix 3 has no bundler step).
FROM node:24.22.1-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci && npm cache clean --force
COPY . .
RUN npm run css

# Runtime stage: Remix 3 runs the TypeScript sources directly, so app/ ships alongside prod node_modules.
FROM node:24.22.1-alpine AS runtime
WORKDIR /app
RUN addgroup -S mediaset && adduser -S mediaset -G mediaset
COPY package*.json ./
RUN npm ci --omit=dev && npm cache clean --force
COPY --from=build /app/app ./app
COPY --from=build /app/public ./public
COPY server.ts tsconfig.json ./
RUN chown -R mediaset:mediaset /app
USER mediaset
EXPOSE 3000
ENV NODE_ENV=production PORT=3000
CMD ["npm", "run", "start"]
