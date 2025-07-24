# Minimal Docker image that supports your current pipeline code
FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy

# Set environment variables
ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true \
    PATH="/root/.dotnet/tools:${PATH}"

# Install only essential system dependencies that your pipeline needs
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    git \
    unzip \
    tar \
    python3 \
    python3-pip \
    openjdk-11-jdk \
    && rm -rf /var/lib/apt/lists/*

# Install JFrog CLI (it installs directly to /usr/local/bin as 'jf')
RUN curl -fL https://install-cli.jfrog.io | sh

# Create Jenkins user
RUN groupadd -g 1000 jenkins && \
    useradd -u 1000 -g jenkins -m -s /bin/bash jenkins

# Set working directory
WORKDIR /workspace

# Verify basic installations
RUN echo "=== Base Image Verification ===" && \
    dotnet --version && \
    python3 --version && \
    pip3 --version && \
    jf --version && \
    wget --version | head -1 && \
    echo "✅ Base image ready for runtime installations"

USER jenkins