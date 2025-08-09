# ============================================================================
# SMS GATEWAY DOCKER IMAGE
# ============================================================================
# Multi-stage Docker build for the SMS Gateway service that handles
# Prometheus alert notifications via SMS and email.
#
# Features:
# - Lightweight Node.js 18 Alpine base image
# - Production-only dependencies for smaller image size
# - Non-root user for security compliance
# - Health check endpoint for container orchestration
# - Optimized layer caching for faster builds
# ============================================================================

# Use official Node.js 18 Alpine image (lightweight ~40MB base)
FROM node:18-alpine

# Set working directory for application files
WORKDIR /app

# ============================================================================
# DEPENDENCY INSTALLATION (Optimized for Docker layer caching)
# ============================================================================
# Copy package files first - if source code changes but dependencies don't,
# Docker can reuse the npm install layer from cache
COPY package*.json ./

# Install only production dependencies to reduce final image size
# --only=production excludes devDependencies (nodemon, etc.)
RUN npm install --only=production

# ============================================================================
# APPLICATION CODE
# ============================================================================
# Copy application code after dependencies for optimal caching strategy
COPY app.js ./

# ============================================================================
# SECURITY HARDENING
# ============================================================================
# Create a non-root user for security best practices
# Running containers as root is a significant security risk
RUN addgroup -g 1001 -S nodejs && \
    adduser -S nodejs -u 1001

# Change ownership of the application directory to the nodejs user
RUN chown -R nodejs:nodejs /app

# Switch to non-root user for all subsequent operations
USER nodejs

# ============================================================================
# CONTAINER CONFIGURATION
# ============================================================================
# Expose port 8080 for the SMS Gateway web server
EXPOSE 8080

# Health check for container orchestration and monitoring systems
# This allows Docker/Kubernetes to determine if the container is healthy
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD node -e "require('http').get('http://localhost:8080/health', (res) => { process.exit(res.statusCode === 200 ? 0 : 1) })"

# Start the application using npm start (defined in package.json)
CMD ["npm", "start"]
