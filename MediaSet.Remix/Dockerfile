# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install all dependencies (including dev dependencies needed for build)
RUN npm ci && \
    npm cache clean --force

# Copy source code
COPY . .

# Build argument for versioning - must match what's passed from CI/CD
ARG VITE_APP_VERSION=0.0.0-local
ENV VITE_APP_VERSION=${VITE_APP_VERSION}

# Build the application
RUN npm run build

# Runtime stage
FROM node:20-alpine AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup -S mediaset && adduser -S mediaset -G mediaset

# Copy package files and install only production dependencies
COPY package*.json ./
RUN npm ci --only=production && \
    npm cache clean --force

# Copy built application from build stage
COPY --from=build /app/build ./build
COPY --from=build /app/public ./public

# Set ownership
RUN chown -R mediaset:mediaset /app

# Switch to non-root user
USER mediaset

# Expose port
EXPOSE 3000

# Set environment variables
ENV NODE_ENV=production

CMD ["npm", "run", "start"]