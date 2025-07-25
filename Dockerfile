# Jenkins Agent for .NET Pipeline (Runtime Tool Installation)
FROM jenkins/agent:latest

# Switch to root for installations
USER root

# Set environment variables
ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

# Install system dependencies that your pipeline needs
RUN apt-get update && apt-get install -y \
    wget \
    curl \
    git \
    unzip \
    tar \
    python3 \
    python3-pip \
    python3-venv \
    ca-certificates \
    gnupg \
    lsb-release \
    && rm -rf /var/lib/apt/lists/*

# Install .NET SDK 8.0
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-8.0 && \
    rm packages-microsoft-prod.deb && \
    rm -rf /var/lib/apt/lists/*

# Install JFrog CLI (your pipeline expects this to be available)
RUN curl -fL https://install-cli.jfrog.io | sh

# Switch back to jenkins user
USER jenkins

# Update PATH to include .NET tools location
ENV PATH="/home/jenkins/.dotnet/tools:/root/.dotnet/tools:/usr/local/bin:${PATH}"

# Verify basic installations
RUN echo "=== Base Installation Verification ===" && \
    java -version && \
    dotnet --version && \
    python3 --version && \
    pip3 --version && \
    jf --version && \
    echo "✅ Base tools ready - pipeline will install additional tools at runtime"

# Keep the default CMD from jenkins/agent (handles Jenkins remoting)