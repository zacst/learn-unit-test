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
    python3-full \
    pipx \
    ca-certificates \
    gnupg \
    lsb-release \
    && rm -rf /var/lib/apt/lists/*

# Install Semgrep system-wide to avoid pip user installation issues
RUN pip3 install --break-system-packages semgrep

# Install .NET SDK 8.0 (to support .NET 8.0 projects)
RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-9.0 && \
    rm packages-microsoft-prod.deb && \
    rm -rf /var/lib/apt/lists/*

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
    semgrep --version && \
    echo "✅ Base tools ready - Jenkins will provide JFrog CLI via tool configuration"

# Keep the default CMD from jenkins/agent (handles Jenkins remoting)