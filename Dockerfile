# RimWatch Docker Build Environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Install additional tools for RimWorld modding
RUN apt-get update && apt-get install -y \
    unzip \
    wget \
    git \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy project files
COPY Source/ ./Source/
COPY Tests/ ./Tests/
COPY About/ ./About/
COPY docs/ ./docs/

# Copy project configuration files
COPY *.md ./
COPY .gitignore ./

# Set build configuration
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# Create output directories
RUN mkdir -p /app/Build/Assemblies
RUN mkdir -p /app/Build/About

# Build stage prepares the environment
# Actual build happens in docker-compose command with mounted RimWorldLibs
WORKDIR /app/Source/RimWatch
RUN dotnet restore
# Note: dotnet build is done in docker-compose command, not here
# This allows RimWorldLibs to be mounted via volumes

# Test stage
FROM build AS test
WORKDIR /app

# Set up test environment
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# Create test results directory
RUN mkdir -p /app/TestResults

# Note: Tests are run via docker-compose command, not during build

# Production stage - minimal image with just the compiled mod
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime

WORKDIR /mod

# Copy built assemblies and About files
COPY --from=build /app/Build/ ./

# Create version info
RUN echo "Built: $(date)" > ./Build-Info.txt
RUN echo "Docker: $(dotnet --version)" >> ./Build-Info.txt

# Volume for mod output
VOLUME ["/mod/output"]

# Default command copies mod to output volume
CMD ["sh", "-c", "cp -r /mod/* /mod/output/ && echo 'RimWatch mod copied to output volume'"]

