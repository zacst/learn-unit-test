# Universal Jenkins Agent for .NET, Node.js/TypeScript, and Docker Pipelines
FROM jenkins/agent:latest

# Switch to the root user to perform installations
USER root

# Set environment variables for non-interactive package installation
ENV DEBIAN_FRONTEND=noninteractive

#=============================================================================
# Step 1: Install Common System Dependencies & Security Tools
#=============================================================================
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Standard utilities
    wget \
    curl \
    git \
    unzip \
    tar \
    # Python for Semgrep
    python3 \
    python3-pip \
    # Required for adding repositories
    ca-certificates \
    gnupg \
    && rm -rf /var/lib/apt/lists/*

# Install Semgrep system-wide using pip
# The --break-system-packages flag is needed on newer Debian/Ubuntu versions.
RUN pip3 install --break-system-packages semgrep

#=============================================================================
# Step 2: Install Docker CLI
# This provides the 'docker' command inside the container.
#=============================================================================
# Add Docker's official GPG key and set up the repository
RUN install -m 0755 -d /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    chmod a+r /etc/apt/keyrings/docker.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install the Docker CLI package (NOT the full engine)
RUN apt-get update && apt-get install -y docker-ce-cli && \
    rm -rf /var/lib/apt/lists/*

#=============================================================================
# Step 3: Install the .NET SDK
#=============================================================================
# Set .NET specific environment variables
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

# Add the Microsoft package signing key and repository, then install .NET SDK 9.0
RUN wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-9.0 && \
    rm -rf /var/lib/apt/lists/*

#=============================================================================
# Step 4: Install Node.js & npm
#=============================================================================
# Use the official NodeSource repository to get the latest LTS version (v20.x)
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    rm -rf /var/lib/apt/lists/*

#=============================================================================
# Step 5: Final Configuration & Verification
#=============================================================================
# Switch back to the non-root jenkins user for security
USER jenkins

# Update PATH to include the location for dotnet global tools
ENV PATH="/home/jenkins/.dotnet/tools:${PATH}"

# Verify that all essential tools are installed and accessible
RUN echo "=== Universal Agent Tool Verification ===" && \
    echo "--- Java ---" && java -version && \
    echo "--- Git ---" && git --version && \
    echo "--- Python & Semgrep ---" && python3 --version && semgrep --version && \
    echo "--- Docker CLI ---" && docker --version && \
    echo "--- .NET SDK ---" && dotnet --version && \
    echo "--- Node.js & npm ---" && node -v && npm -v && \
    echo "✅ Agent is ready for .NET, TypeScript, and Docker pipelines."

# The default CMD from the base image will be used to start the agent connection