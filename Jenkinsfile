/**
 * Refactored Jenkins Pipeline for a .NET Project
 *
 * This pipeline automates the build, test, analysis, and deployment of a .NET application.
 * It includes stages for:
 * - Compiling the code
 * - Running NUnit tests and generating coverage reports
 * - Performing Static Application Security Testing (SAST) with SonarQube and Semgrep
 * - Linting and secrets detection
 * - Publishing artifacts to JFrog Artifactory
 */

pipeline {
    agent {label 'pipeline'}

    tools {
        // Configure JFrog CLI tool in Jenkins Global Tool Configuration
        jfrog 'jfrog-cli'
    }

    // =========================================================================
    // Environment Variables
    // =========================================================================
    environment {
        // --- .NET Configuration ---
        DOTNET_VERSION = '6.0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        DOTNET_FORMAT_VERSION = '7.0.400' // Compatible with your SDK
        DOTNET_VERBOSITY = 'n' // Default verbosity (n: normal, q: quiet, m: minimal, d: detailed)

        // --- Reporting Directories ---
        TEST_RESULTS_DIR = 'test-results'
        COVERAGE_REPORTS_DIR = 'coverage-reports'
        LINTER_REPORTS_DIR = 'linter-reports'
        SECURITY_REPORTS_DIR = 'security-reports'
        SECRETS_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/secrets"
        DAST_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/dast"

        // --- SonarQube Configuration ---
        SONARQUBE_URL = 'http://localhost:9000'
        SONAR_PROJECT_KEY = 'calculator' // Replace with your SonarQube project key

        // --- JFrog Artifactory Configuration ---
        JFROG_CLI_BUILD_NAME = "${JOB_NAME}"
        JFROG_CLI_BUILD_NUMBER = "${BUILD_NUMBER}"
        ARTIFACTORY_REPO_BINARIES = 'libs-release-local'
        ARTIFACTORY_REPO_NUGET = 'nuget-local'
        ARTIFACTORY_REPO_REPORTS = 'reports-local'

        // --- Security Tool Configuration ---
        SEMGREP_TIMEOUT = '300'
        GITLEAKS_VERSION = '8.18.2'
        TRIVY_VERSION = '0.50.0'

        // --- Tool Installation Control Environment Variables ---
        INSTALL_SEMGREP = "${env.INSTALL_SEMGREP ?: 'auto'}"              // auto, always, never, skip
        INSTALL_TRIVY = "${env.INSTALL_TRIVY ?: 'auto'}"                  // auto, always, never, skip
        INSTALL_GITLEAKS = "${env.INSTALL_GITLEAKS ?: 'auto'}"            // auto, always, never, skip
        INSTALL_DOTNET_TOOLS = "${env.INSTALL_DOTNET_TOOLS ?: 'auto'}"    // auto, always, never, skip
        INSTALL_SONARSCANNER = "${env.INSTALL_SONARSCANNER ?: 'auto'}"    // auto, always, never, skip
        INSTALL_REPORTGEN = "${env.INSTALL_REPORTGEN ?: 'auto'}"          // auto, always, never, skip
        INSTALL_DOTNET_FORMAT = "${env.INSTALL_DOTNET_FORMAT ?: 'auto'}"  // auto, always, never, skip

        // --- Tool Binary Paths (for custom installations) ---
        SEMGREP_PATH = "${env.SEMGREP_PATH ?: ''}"                        // Custom path to semgrep binary
        TRIVY_PATH = "${env.TRIVY_PATH ?: ''}"                            // Custom path to trivy binary
        GITLEAKS_PATH = "${env.GITLEAKS_PATH ?: ''}"                      // Custom path to gitleaks binary

        // --- Installation Method Control ---
        TOOL_INSTALL_METHOD = "${env.TOOL_INSTALL_METHOD ?: 'runtime'}"   // runtime, prebuilt, hybrid
        FORCE_TOOL_REINSTALL = "${env.FORCE_TOOL_REINSTALL ?: 'false'}"   // Force reinstallation even if tools exist
        SKIP_TOOL_VERIFICATION = "${env.SKIP_TOOL_VERIFICATION ?: 'false'}" // Skip tool verification after installation

        // --- Test Token ---
        // This is a fake GitHub token for testing purposes
        GITHUB_TOKEN = "ghp_1234567890abcdef1234567890abcdef1234"
    }

    // =========================================================================
    // Pipeline Options
    // =========================================================================
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 30, unit: 'MINUTES')
        timestamps()
        skipDefaultCheckout()
        parallelsAlwaysFailFast()
    }

    // =========================================================================
    // Build Parameters
    // =========================================================================
    parameters {
        // --- Test & Coverage ---
        booleanParam(name: 'GENERATE_COVERAGE', defaultValue: true, description: 'Generate code coverage reports')
        booleanParam(name: 'FAIL_ON_TEST_FAILURE', defaultValue: true, description: 'Fail the build if any tests fail')
        choice(name: 'LOG_LEVEL', choices: ['INFO', 'DEBUG', 'WARN', 'ERROR'], description: 'Set logging level for test execution')

        // --- Security Scans ---
        booleanParam(name: 'ENABLE_SECURITY_SCAN', defaultValue: true, description: 'Enable comprehensive security scanning')
        booleanParam(name: 'FAIL_ON_SECURITY_ISSUES', defaultValue: false, description: 'Fail build on critical security vulnerabilities')
        choice(name: 'SECURITY_SCAN_LEVEL', choices: ['BASIC', 'COMPREHENSIVE', 'FULL'], description: 'Security scanning depth level')
        booleanParam(name: 'ENABLE_LINTING', defaultValue: true, description: 'Enable .NET code style linting with dotnet-format')
        booleanParam(name: 'ENABLE_SECRETS_SCAN', defaultValue: true, description: 'Enable secrets detection scan with Gitleaks')
        booleanParam(name: 'ENABLE_LICENSE_CHECK', defaultValue: true, description: 'Enable dependency license compliance check')

        // --- Tool Installation Override Parameters ---
        choice(name: 'TOOL_INSTALLATION_STRATEGY', 
               choices: ['Environment Variables', 'Runtime Detection', 'Force Install All', 'Use Prebuilt'], 
               description: 'Override tool installation strategy')
        booleanParam(name: 'DEBUG_TOOL_INSTALLATION', defaultValue: false, description: 'Enable verbose logging for tool installation')

        // --- Docker Image Parameters ---
        booleanParam(
            name: 'USE_FULL_IMAGE', 
            defaultValue: false, 
            description: 'Use pre-built image with all tools (faster) vs minimal image with runtime installation (flexible)'
        )
        booleanParam(
            name: 'FORCE_TOOL_INSTALLATION', 
            defaultValue: false, 
            description: 'Force tool installation even if using full image (for testing new versions)'
        )
    }

    // =========================================================================
    // Pipeline Stages
    // =========================================================================
    stages {
        stage('Initialize') {
            steps {
                script {
                    initializeBuild()
                }
            }
        }

        stage('Checkout') {
            steps {
                script {
                    checkoutScm()
                }
            }
        }

        stage('Setup .NET Environment') {
            steps {
                script {
                    setupDotnet()
                }
            }
        }

        stage('Discover NUnit Test Projects') {
            steps {
                script {
                    discoverTestProjects()
                }
            }
        }

        stage('Restore .NET Dependencies') {
            steps {
                script {
                    restoreDependencies()
                }
            }
        }

        stage('Build, Test & SAST Analysis') {
            steps {
                script {
                    runBuildTestAndSast()
                }
            }
            post {
                always {
                    script {
                        archiveAndPublishTestResults()
                    }
                }
            }
        }

        stage('SAST Security Scan (Semgrep)') {
            when { expression { params.ENABLE_SECURITY_SCAN } }
            steps {
                script {
                    runSemgrepScan()
                }
            }
        }

        stage('Linting & Code Style') {
            when { expression { params.ENABLE_LINTING } }
            steps {
                script {
                    runLinting()
                }
            }
        }

        stage('Secrets Detection (Gitleaks)') {
            when { expression { params.ENABLE_SECRETS_SCAN } }
            steps {
                script {
                    runSecretsScan()
                }
            }
        }
        
        stage('Container Security Scan (Trivy)') {
            when { expression { params.SECURITY_SCAN_LEVEL in ['COMPREHENSIVE', 'FULL'] } }
            steps {
                script {
                    runTrivyContainerScan()
                }
            }
        }

        stage('Quality Gate') {
            steps {
                script {
                    evaluateQualityGate()
                }
            }
        }

        stage('Upload to JFrog Artifactory') {
            steps {
                script {
                    uploadArtifacts()
                }
            }
        }

        stage('Deployment') {
            // This stage is a placeholder for your deployment logic.
            steps {
                echo "🚀 Deployment stage (to be implemented)"
            }
        }

        stage('DAST Scan (OWASP ZAP)') {
            // This stage is a placeholder for your DAST logic.
            when { expression { params.ENABLE_DAST_SCAN } }
            steps {
                echo "🚀 DAST scan (to be implemented)"
            }
        }
    }

    // =========================================================================
    // Post-Build Actions
    // =========================================================================
    post {
        always {
            script {
                // Archive all generated security reports
                archiveSecurityReports()
                // Publish security results to Jenkins UI
                publishSecurityResults()
                // Generate a text summary of the security scans
                generateSecuritySummary()
                // Evaluate security gates to potentially fail the build
                evaluateSecurityGates()
                // Clean up workspace
                echo "🧹 Post-build cleanup..."
                cleanWs()
            }
        }
        success {
            script {
                echo "✅ Build completed successfully!"
                // notifySuccess() // Example of a notification function
            }
        }
        failure {
            script {
                echo "❌ Build failed!"
                // notifyFailure() // Example of a notification function
            }
        }
        unstable {
            script {
                echo "⚠️ Build completed with warnings!"
                // notifyUnstable() // Example of a notification function
            }
        }
    }
}


// =============================================================================
// HELPER FUNCTIONS
// =============================================================================

//---------------------------------
// Pipeline Initialization & Setup
//---------------------------------

/**
 * Initializes build-wide variables and handles tool installation based on environment variables.
 */
def initializeBuild() {
    echo "🔧 Initializing build with environment-based tool installation..."
    
    // Set verbosity based on log level
    def verbosityMapping = [INFO: 'n', DEBUG: 'd', WARN: 'm', ERROR: 'q']
    env.DOTNET_VERBOSITY = verbosityMapping.get(params.LOG_LEVEL, 'n')
    echo "🔧 dotnet verbosity set to: ${env.DOTNET_VERBOSITY}"
    env.NUNIT_PROJECTS = ''

    // Report current environment configuration
    reportEnvironmentInfo()
    
    // Handle tool installation based on strategy
    handleToolInstallationStrategy()
    
    echo "✅ Build initialization completed"
}

/**
 * Handles tool installation based on the selected strategy and environment variables.
 */
def handleToolInstallationStrategy() {
    def strategy = params.TOOL_INSTALLATION_STRATEGY ?: 'Environment Variables'
    echo "🔧 Tool Installation Strategy: ${strategy}"
    
    switch(strategy) {
        case 'Environment Variables':
            installToolsBasedOnEnvironment()
            break
        case 'Runtime Detection':
            installAllSecurityTools() // Original logic
            break
        case 'Force Install All':
            forceInstallAllTools()
            break
        case 'Use Prebuilt':
            verifyPrebuiltTools()
            break
        default:
            echo "⚠️ Unknown strategy, falling back to environment variables"
            installToolsBasedOnEnvironment()
    }
}

/**
 * Installs tools based on environment variable configuration.
 */
def installToolsBasedOnEnvironment() {
    echo "🔧 Installing tools based on environment variable configuration..."
    
    def toolsToInstall = [:]
    def debugMode = params.DEBUG_TOOL_INSTALLATION
    
    // Semgrep installation logic
    if (shouldInstallTool('SEMGREP', env.INSTALL_SEMGREP, 'semgrep')) {
        toolsToInstall['Semgrep'] = { 
            if (debugMode) echo "🔧 Installing Semgrep based on INSTALL_SEMGREP=${env.INSTALL_SEMGREP}"
            installSemgrep() 
        }
    }
    
    // Trivy installation logic
    if (shouldInstallTool('TRIVY', env.INSTALL_TRIVY, 'trivy')) {
        toolsToInstall['Trivy'] = { 
            if (debugMode) echo "🔧 Installing Trivy based on INSTALL_TRIVY=${env.INSTALL_TRIVY}"
            installTrivy() 
        }
    }
    
    // Gitleaks installation logic
    if (shouldInstallTool('GITLEAKS', env.INSTALL_GITLEAKS, 'gitleaks')) {
        toolsToInstall['Gitleaks'] = { 
            if (debugMode) echo "🔧 Installing Gitleaks based on INSTALL_GITLEAKS=${env.INSTALL_GITLEAKS}"
            installGitleaks() 
        }
    }
    
    // .NET Tools installation logic
    def dotnetToolsNeeded = getDotnetToolsToInstall(debugMode)
    if (dotnetToolsNeeded) {
        toolsToInstall['DotNet Tools'] = {
            if (debugMode) echo "🔧 Installing .NET tools: ${dotnetToolsNeeded.join(', ')}"
            dotnetToolsNeeded.each { toolInfo ->
                installDotnetTool(toolInfo.name, toolInfo.version)
            }
        }
    }
    
    if (toolsToInstall.isEmpty()) {
        echo "✅ All required tools are already available or installation is disabled"
        return
    }
    
    echo "📦 Installing ${toolsToInstall.size()} tool categories based on environment configuration..."
    
    // Install tools in parallel for speed
    try {
        parallel(toolsToInstall)
        echo "✅ Environment-based tool installation completed successfully"
    } catch (Exception e) {
        echo "❌ Tool installation failed: ${e.getMessage()}"
        if (env.FORCE_TOOL_REINSTALL == 'true') {
            error("Tool installation is required but failed")
        } else {
            echo "⚠️ Continuing with available tools..."
        }
    }
    
    // Verify installations if not skipped
    if (env.SKIP_TOOL_VERIFICATION != 'true') {
        verifyToolInstallations()
    }
}

/**
 * Determines if a tool should be installed based on environment configuration.
 */
def shouldInstallTool(String toolEnvPrefix, String installFlag, String toolName) {
    def customPath = env."${toolEnvPrefix}_PATH"
    
    // If custom path is provided, check if it exists
    if (customPath && fileExists(customPath)) {
        echo "✅ Using custom ${toolName} at: ${customPath}"
        return false
    }
    
    switch(installFlag?.toLowerCase()) {
        case 'always':
            return true
        case 'never':
        case 'skip':
            echo "⏭️ Skipping ${toolName} installation (${installFlag})"
            return false
        case 'auto':
        default:
            def available = isToolAvailable(toolName) || isToolAvailableCustom(toolName)
            if (available && env.FORCE_TOOL_REINSTALL != 'true') {
                echo "✅ ${toolName} already available, skipping installation"
                return false
            }
            return true
    }
}

/**
 * Gets the list of .NET tools that need to be installed based on environment variables.
 */
def getDotnetToolsToInstall(boolean debugMode) {
    def toolsNeeded = []
    
    // Map of tool names to their environment variables and versions
    def dotnetTools = [
        [name: 'dotnet-sonarscanner', envVar: 'INSTALL_SONARSCANNER', version: ''],
        [name: 'dotnet-reportgenerator-globaltool', envVar: 'INSTALL_REPORTGEN', version: ''],
        [name: 'dotnet-format', envVar: 'INSTALL_DOTNET_FORMAT', version: env.DOTNET_FORMAT_VERSION]
    ]
    
    dotnetTools.each { tool ->
        def installFlag = env."${tool.envVar}"
        if (shouldInstallDotnetTool(tool.name, installFlag, debugMode)) {
            toolsNeeded.add([name: tool.name, version: tool.version])
        }
    }
    
    return toolsNeeded
}

/**
 * Determines if a .NET tool should be installed.
 */
def shouldInstallDotnetTool(String toolName, String installFlag, boolean debugMode) {
    switch(installFlag?.toLowerCase()) {
        case 'always':
            if (debugMode) echo "🔧 Force installing ${toolName} (always)"
            return true
        case 'never':
        case 'skip':
            if (debugMode) echo "⏭️ Skipping ${toolName} (${installFlag})"
            return false
        case 'auto':
        default:
            def available = isDotnetToolAvailable(toolName)
            if (available && env.FORCE_TOOL_REINSTALL != 'true') {
                if (debugMode) echo "✅ ${toolName} already available"
                return false
            }
            if (debugMode) echo "🔧 Need to install ${toolName}"
            return true
    }
}

/**
 * Forces installation of all tools regardless of current state.
 */
def forceInstallAllTools() {
    echo "🔧 Force installing all tools..."
    def originalForceFlag = env.FORCE_TOOL_REINSTALL
    env.FORCE_TOOL_REINSTALL = 'true'
    
    try {
        installAllSecurityTools()
    } finally {
        env.FORCE_TOOL_REINSTALL = originalForceFlag
    }
}

/**
 * Verifies that prebuilt tools are available and working.
 */
def verifyPrebuiltTools() {
    echo "🔍 Verifying prebuilt tools are available..."
    def missingTools = []
    
    def toolsToCheck = [
        [name: 'semgrep', check: { isToolAvailable('semgrep') }],
        [name: 'trivy', check: { isTrivyAvailable() }],
        [name: 'gitleaks', check: { isGitleaksAvailable() }],
        [name: 'dotnet-sonarscanner', check: { isDotnetToolAvailable('dotnet-sonarscanner') }]
    ]
    
    toolsToCheck.each { tool ->
        if (!tool.check()) {
            missingTools.add(tool.name)
        }
    }
    
    if (missingTools) {
        error("❌ Prebuilt strategy failed. Missing tools: ${missingTools.join(', ')}")
    } else {
        echo "✅ All required prebuilt tools are available"
    }
}

/**
 * Verifies tool installations after completion.
 */
def verifyToolInstallations() {
    echo "🔍 Verifying tool installations..."
    def verificationResults = []
    
    // Check each tool that should be installed
    if (env.INSTALL_SEMGREP != 'never' && env.INSTALL_SEMGREP != 'skip') {
        verificationResults.add([tool: 'Semgrep', available: isToolAvailable('semgrep')])
    }
    
    if (env.INSTALL_TRIVY != 'never' && env.INSTALL_TRIVY != 'skip') {
        verificationResults.add([tool: 'Trivy', available: isTrivyAvailable()])
    }
    
    if (env.INSTALL_GITLEAKS != 'never' && env.INSTALL_GITLEAKS != 'skip') {
        verificationResults.add([tool: 'Gitleaks', available: isGitleaksAvailable()])
    }
    
    def failedVerifications = verificationResults.findAll { !it.available }
    
    if (failedVerifications) {
        def failedTools = failedVerifications.collect { it.tool }.join(', ')
        echo "❌ Tool verification failed for: ${failedTools}"
        if (env.FORCE_TOOL_REINSTALL == 'true') {
            error("Required tools failed verification")
        } else {
            echo "⚠️ Continuing despite verification failures..."
        }
    } else {
        echo "✅ All installed tools verified successfully"
    }
}

// =============================================================================
// SMART INSTALLATION FUNCTIONS THAT DETECT EXISTING TOOLS
// =============================================================================

def installAllSecurityTools() {
    echo "🔧 Checking and installing security tools as needed..."
    
    def toolsToInstall = [:]
    
    // Check which tools need installation
    if (!isToolAvailable('semgrep')) {
        toolsToInstall['Semgrep'] = { installSemgrep() }
    }
    
    if (!isTrivyAvailable()) {
        toolsToInstall['Trivy'] = { installTrivy() }
    }
    
    if (!isGitleaksAvailable()) {
        toolsToInstall['Gitleaks'] = { installGitleaks() }
    }
    
    // Check .NET tools
    def dotnetToolsNeeded = []
    if (!isDotnetToolAvailable('dotnet-sonarscanner')) {
        dotnetToolsNeeded.add('dotnet-sonarscanner')
    }
    if (!isDotnetToolAvailable('dotnet-reportgenerator-globaltool')) {
        dotnetToolsNeeded.add('dotnet-reportgenerator-globaltool')
    }
    if (!isDotnetToolAvailable('dotnet-format')) {
        dotnetToolsNeeded.add('dotnet-format')
    }
    
    if (dotnetToolsNeeded) {
        toolsToInstall['DotNet Tools'] = {
            dotnetToolsNeeded.each { tool ->
                def version = (tool == 'dotnet-format') ? env.DOTNET_FORMAT_VERSION : ''
                installDotnetTool(tool, version)
            }
        }
    }
    
    if (toolsToInstall.isEmpty()) {
        echo "✅ All required tools are already available"
        return
    }
    
    echo "📦 Installing ${toolsToInstall.size()} tool categories..."
    
    // Install tools in parallel for speed
    parallel(toolsToInstall)
    
    echo "✅ Tool installation completed"
}

// =============================================================================
// TOOL DETECTION HELPER FUNCTIONS
// =============================================================================

def isToolAvailable(String toolName) {
    // Check custom path first
    def customPath = env."${toolName.toUpperCase()}_PATH"
    if (customPath && fileExists(customPath)) {
        return true
    }
    
    def result = sh(
        script: "which ${toolName} > /dev/null 2>&1 && echo 'found' || echo 'not_found'",
        returnStdout: true
    ).trim()
    return result == 'found'
}

def isToolAvailableCustom(String toolName) {
    // Additional custom logic for tool detection
    switch(toolName) {
        case 'trivy':
            return isTrivyAvailable()
        case 'gitleaks':
            return isGitleaksAvailable()
        default:
            return false
    }
}

def isTrivyAvailable() {
    // Check custom path first
    if (env.TRIVY_PATH && fileExists(env.TRIVY_PATH)) {
        return true
    }
    
    // Check both system-wide and workspace installation
    def systemInstall = isToolAvailable('trivy')
    def workspaceInstall = fileExists("${env.WORKSPACE}/trivy-bin/trivy")
    return systemInstall || workspaceInstall
}

def isGitleaksAvailable() {
    // Check custom path first
    if (env.GITLEAKS_PATH && fileExists(env.GITLEAKS_PATH)) {
        return true
    }
    
    // Check both system-wide and workspace installation
    def systemInstall = isToolAvailable('gitleaks')
    def workspaceInstall = fileExists("${env.WORKSPACE}/gitleaks")
    return systemInstall || workspaceInstall
}

def isDotnetToolAvailable(String toolName) {
    def result = sh(
        script: "dotnet tool list -g | grep -q '${toolName}' && echo 'found' || echo 'not_found'",
        returnStdout: true
    ).trim()
    return result == 'found'
}

// =============================================================================
// ENVIRONMENT DETECTION AND REPORTING
// =============================================================================

def reportEnvironmentInfo() {
    echo "🔍 Environment Information:"
    echo "   Docker Image Strategy: ${params.USE_FULL_IMAGE ? 'Full Image' : 'Minimal + Runtime Installation'}"
    echo "   Force Installation: ${params.FORCE_TOOL_INSTALLATION}"
    echo "   Tool Install Method: ${env.TOOL_INSTALL_METHOD}"
    echo "   Force Tool Reinstall: ${env.FORCE_TOOL_REINSTALL}"
    
    // Report tool installation flags
    echo "   Tool Installation Flags:"
    echo "     - Semgrep: ${env.INSTALL_SEMGREP}"
    echo "     - Trivy: ${env.INSTALL_TRIVY}"
    echo "     - Gitleaks: ${env.INSTALL_GITLEAKS}"
    echo "     - SonarScanner: ${env.INSTALL_SONARSCANNER}"
    echo "     - Report Generator: ${env.INSTALL_REPORTGEN}"
    echo "     - Dotnet Format: ${env.INSTALL_DOTNET_FORMAT}"
    
    // Report custom paths if provided
    def customPaths = []
    if (env.SEMGREP_PATH) customPaths.add("Semgrep: ${env.SEMGREP_PATH}")
    if (env.TRIVY_PATH) customPaths.add("Trivy: ${env.TRIVY_PATH}")
    if (env.GITLEAKS_PATH) customPaths.add("Gitleaks: ${env.GITLEAKS_PATH}")
    
    if (customPaths) {
        echo "   Custom Tool Paths:"
        customPaths.each { echo "     - ${it}" }
    }
    
    // Report available tools
    def availableTools = []
    if (isToolAvailable('semgrep')) availableTools.add('Semgrep')
    if (isTrivyAvailable()) availableTools.add('Trivy')
    if (isGitleaksAvailable()) availableTools.add('Gitleaks')
    if (isDotnetToolAvailable('dotnet-sonarscanner')) availableTools.add('SonarScanner')
    
    echo "   Currently Available Tools: ${availableTools.join(', ')}"
}

/**
 * Checks out the source code from SCM and gathers git information.
 */
def checkoutScm() {
    echo "🔄 Checking out source code..."
    checkout scm

    env.GIT_COMMIT_SHORT = sh(script: "git rev-parse --short HEAD", returnStdout: true).trim()
    env.GIT_COMMIT_MSG = sh(script: "git log -1 --pretty=format:'%s'", returnStdout: true).trim()

    echo "📋 Build Info:"
    echo "   Branch: ${env.BRANCH_NAME}"
    echo "   Commit: ${env.GIT_COMMIT_SHORT}"
    echo "   Message: ${env.GIT_COMMIT_MSG}"
}

/**
 * Verifies the .NET SDK installation.
 */
def setupDotnet() {
    echo "🔧 Setting up .NET environment..."
    if (sh(script: "dotnet --version", returnStatus: true) != 0) {
        error "❌ .NET SDK not found. Please install .NET SDK ${DOTNET_VERSION}"
    }
    sh """
        echo "📦 .NET SDK Version:"
        dotnet --version
        dotnet --info
    """
}

/**
 * Restores .NET dependencies for the solution.
 */
def restoreDependencies() {
    echo "📦 Restoring .NET dependencies..."
    def solutionFiles = findFiles(glob: '**/*.sln')
    if (solutionFiles) {
        solutionFiles.each { sln ->
            sh "dotnet restore '${sln.path}' --verbosity ${env.DOTNET_VERBOSITY}"
        }
    } else {
        echo "⚠️ No solution file found. Running restore on the current directory."
        sh "dotnet restore --verbosity ${env.DOTNET_VERBOSITY}"
    }
}

//---------------------------------
// Build, Test, and SAST (SonarQube)
//---------------------------------

/**
 * Main function to orchestrate the build, test, and SonarQube analysis stage.
 */
def runBuildTestAndSast() {
    echo "🔨 Starting Build, Test, and SAST Analysis..."
    sh "mkdir -p ${TEST_RESULTS_DIR} ${COVERAGE_REPORTS_DIR}"

    withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
        withSonarQubeEnv('sonar-server') {
            try {
                ensureDotnetTool('dotnet-sonarscanner', env.INSTALL_SONARSCANNER)
                
                startSonarScanner()
                buildSolution()
                    sh '''
                        echo "--- Verifying build artifacts ---"
                        ls -Rla
                        echo "---------------------------------"
                    '''
                runNunitTests()
                generateCoverageReports()

            } catch (e) {
                echo "❌ Build, Test, or SonarQube analysis failed: ${e.getMessage()}"
                if (params.FAIL_ON_TEST_FAILURE) {
                    throw e
                } else {
                    currentBuild.result = 'UNSTABLE'
                    echo "⚠️ Build marked as unstable due to failures."
                }
            } finally {
                endSonarScanner()
            }
        }
    }
}

/**
 * Ensures a .NET tool is available, installing if needed based on environment configuration.
 */
def ensureDotnetTool(String toolName, String installFlag) {
    if (shouldInstallDotnetTool(toolName, installFlag, params.DEBUG_TOOL_INSTALLATION)) {
        def version = (toolName == 'dotnet-format') ? env.DOTNET_FORMAT_VERSION : ''
        installDotnetTool(toolName, version)
    }
}

/**
 * Installs a global .NET tool.
 */
def installDotnetTool(String toolName, String version = '') {
    echo "📦 Installing .NET tool: ${toolName}..."
    def versionArg = version ? "--version ${version}" : ""
    sh """
        dotnet tool install --global ${toolName} ${versionArg} || true
        export PATH="\$PATH:\$HOME/.dotnet/tools"
    """
}

/**
 * Starts the SonarQube scanner.
 */
def startSonarScanner() {
    echo "🔍 Starting SonarQube analysis..."
    sh '''
        export PATH="$PATH:$HOME/.dotnet/tools"
        dotnet sonarscanner begin \\
            /k:"$SONAR_PROJECT_KEY" \\
            /d:sonar.host.url="$SONARQUBE_URL" \\
            /d:sonar.cs.nunit.reportsPaths="$TEST_RESULTS_DIR/*.trx" \\
            /d:sonar.cs.opencover.reportsPaths="**/coverage.cobertura.xml" \\
            /d:sonar.exclusions="**/bin/**,**/obj/**,**/*.Tests/**,**/security-reports/**,**/coverage-reports/**" \\
            /d:sonar.test.exclusions="**/*.Tests/**" \\
            /d:sonar.coverage.exclusions="**/*.Tests/**"
    '''
}

/**
 * Builds the .NET solution.
 */
def buildSolution() {
    echo "🔨 Building .NET solution..."
    def solutionFiles = findFiles(glob: '**/*.sln')
    if (solutionFiles) {
        solutionFiles.each { sln ->
            echo "🔨 Building solution: ${sln.path}"
            sh "dotnet build '${sln.path}' --configuration Release --no-restore --verbosity ${env.DOTNET_VERBOSITY}"
        }
    } else {
        echo "🔨 No solution files found, building all projects..."
        sh "dotnet build --configuration Release --no-restore --verbosity ${env.DOTNET_VERBOSITY}"
    }
}

/**
 * Runs NUnit tests for the discovered projects.
 */
def runNunitTests() {
    if (!env.NUNIT_PROJECTS) {
        echo "⚠️ No NUnit test projects found to run."
        return
    }

    echo "🧪 Running NUnit tests..."
    def nunitProjectsList = env.NUNIT_PROJECTS.split(',').findAll { it.trim() }
    def coverageArg = params.GENERATE_COVERAGE ? '--collect:"XPlat Code Coverage"' : ''

    nunitProjectsList.each { project ->
        project = project.trim()
        if (project) {
            echo "🧪 Running tests in: ${project}"
            def projectName = project.split('/')[-1].replace('.csproj', '')
            sh """
                dotnet test '${project}' \\
                    --configuration Release \\
                    --no-build \\
                    --logger "trx;LogFileName=nunit-results-${projectName}.trx" \\
                    --results-directory ${TEST_RESULTS_DIR} \\
                    ${coverageArg} \\
                    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \\
                    --verbosity ${env.DOTNET_VERBOSITY}
            """
        }
    }
}

/**
 * Generates HTML coverage reports from Cobertura XML files.
 */
def generateCoverageReports() {
    if (!params.GENERATE_COVERAGE) return

    echo "📊 Generating coverage reports..."
    def coverageFiles = findFiles(glob: '**/coverage.cobertura.xml')
    if (coverageFiles) {
        ensureDotnetTool('dotnet-reportgenerator-globaltool', env.INSTALL_REPORTGEN)
        sh """
            export PATH="\$PATH:\$HOME/.dotnet/tools"
            reportgenerator \\
                -reports:**/coverage.cobertura.xml \\
                -targetdir:${COVERAGE_REPORTS_DIR}/dotnet \\
                -reporttypes:Html,Cobertura,JsonSummary \\
                -verbosity:${params.LOG_LEVEL}
        """
    } else {
        echo "⚠️ No coverage files found."
    }
}

/**
 * Ends the SonarQube scanner analysis.
 */
def endSonarScanner() {
    try {
        echo "🔍 Completing SonarQube analysis..."
        sh '''
            export PATH="$PATH:$HOME/.dotnet/tools"
            dotnet sonarscanner end
        '''
    } catch (Exception e) {
        echo "⚠️ Could not end SonarQube analysis gracefully: ${e.getMessage()}"
    }
}

//---------------------------------
// Security Scan Helpers
//---------------------------------

/**
 * Runs Semgrep SAST scan with environment-based tool handling.
 */
def runSemgrepScan() {
    echo "🔒 Running Semgrep SAST analysis..."
    try {
        sh "mkdir -p ${SECURITY_REPORTS_DIR}/semgrep"
        ensureSecurityTool('semgrep', env.INSTALL_SEMGREP) { installSemgrep() }
        
        def semgrepRules = (params.SECURITY_SCAN_LEVEL in ['COMPREHENSIVE', 'FULL']) ?
            '--config=auto --config=p/cwe-top-25 --config=p/owasp-top-10' :
            '--config=auto'

        def semgrepCmd = getSemgrepCommand()
        sh """
            timeout ${SEMGREP_TIMEOUT} ${semgrepCmd} \\
                ${semgrepRules} \\
                --sarif \\
                --output=${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif \\
                . || true
        """
    } catch (Exception e) {
        echo "❌ Semgrep scan failed: ${e.getMessage()}"
        currentBuild.result = 'UNSTABLE'
    }
}

/**
 * Runs Trivy for container security scanning with environment-based tool handling.
 */
def runTrivyContainerScan() {
    echo "🔒 Running Trivy container security scan..."
    try {
        sh "mkdir -p ${SECURITY_REPORTS_DIR}/trivy"
        ensureSecurityTool('trivy', env.INSTALL_TRIVY) { installTrivy() }
        
        def trivyCmd = getTrivyCommand()
        sh """
            ${trivyCmd} fs \\
                --format sarif \\
                --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif \\
                --skip-dirs bin,obj,packages \\
                --timeout 10m \\
                . || true
        """
    } catch (Exception e) {
        echo "❌ Trivy scan failed: ${e.getMessage()}"
        currentBuild.result = 'UNSTABLE'
    }
}

/**
 * Runs Gitleaks for secrets detection with environment-based tool handling.
 */
def runSecretsScan() {
    echo "🤫 Running Secrets Detection (Gitleaks)..."
    try {
        sh "mkdir -p ${SECRETS_REPORTS_DIR}"
        ensureSecurityTool('gitleaks', env.INSTALL_GITLEAKS) { installGitleaks() }
        
        def gitleaksCmd = getGitleaksCommand()
        sh """
            ${gitleaksCmd} detect --source="." --report-path="${SECRETS_REPORTS_DIR}/gitleaks-report.sarif" --report-format="sarif" --exit-code 0
        """
        processGitleaksResults()
    } catch (Exception e) {
        currentBuild.result = 'UNSTABLE'
        echo "❌ Gitleaks scan failed to execute: ${e.getMessage()}"
    }
}

/**
 * Ensures a security tool is available, installing if needed based on environment configuration.
 */
def ensureSecurityTool(String toolName, String installFlag, Closure installClosure) {
    if (shouldInstallTool(toolName.toUpperCase(), installFlag, toolName)) {
        installClosure()
    }
}

/**
 * Gets the appropriate command for Semgrep based on custom path or system installation.
 */
def getSemgrepCommand() {
    if (env.SEMGREP_PATH && fileExists(env.SEMGREP_PATH)) {
        return env.SEMGREP_PATH
    }
    return 'export PATH="$PATH:$HOME/.local/bin"; semgrep'
}

/**
 * Gets the appropriate command for Trivy based on custom path or installation location.
 */
def getTrivyCommand() {
    if (env.TRIVY_PATH && fileExists(env.TRIVY_PATH)) {
        return env.TRIVY_PATH
    }
    if (fileExists("${env.WORKSPACE}/trivy-bin/trivy")) {
        return "${env.WORKSPACE}/trivy-bin/trivy"
    }
    return 'trivy'
}

/**
 * Gets the appropriate command for Gitleaks based on custom path or installation location.
 */
def getGitleaksCommand() {
    if (env.GITLEAKS_PATH && fileExists(env.GITLEAKS_PATH)) {
        return env.GITLEAKS_PATH
    }
    if (fileExists("${env.WORKSPACE}/gitleaks")) {
        return "${env.WORKSPACE}/gitleaks"
    }
    return 'gitleaks'
}

/**
 * Processes Gitleaks results and updates the build status.
 */
def processGitleaksResults() {
    def reportFile = "${SECRETS_REPORTS_DIR}/gitleaks-report.sarif"
    if (!fileExists(reportFile)) {
        echo "⚠️ Gitleaks report was not generated."
        return
    }

    recordIssues(
        tool: sarif(pattern: reportFile, id: 'gitleaks', name: 'Secrets'),
        enabledForFailure: true,
        qualityGates: [[threshold: 1, type: 'TOTAL', unstable: true]]
    )

    def gitleaksReport = readJSON(file: reportFile)
    def resultsCount = gitleaksReport.runs[0].results.size()

    echo "📊 Gitleaks found ${resultsCount} potential secret(s)."
    if (resultsCount > 0) {
        if (params.FAIL_ON_SECURITY_ISSUES) {
            error("❌ Build failed: Secrets detected in the codebase by Gitleaks.")
        } else {
            currentBuild.result = 'UNSTABLE'
        }
    } else {
        echo "✅ No secrets found."
    }
}

//---------------------------------
// Linting Helpers
//---------------------------------

/**
 * Runs the .NET linter (dotnet-format) and calls the publisher.
 */
def runLinting() {
    echo "💅 Running .NET Linter (dotnet-format)..."
    def lintingStatus = 'SUCCESS'
    try {
        sh "mkdir -p ${LINTER_REPORTS_DIR}"
        def solutionFile = findFiles(glob: '**/*.sln')
        if (solutionFile.size() == 0) {
            error "❌ Could not find a solution file (.sln) to lint."
        }

        ensureDotnetTool('dotnet-format', env.INSTALL_DOTNET_FORMAT)

        def formatResult = sh(
            script: """
                export PATH="\$PATH:\$HOME/.dotnet/tools"
                dotnet format '${solutionFile[0].path}' --verify-no-changes --report ${LINTER_REPORTS_DIR}/dotnet-format.json --verbosity diagnostic
            """,
            returnStatus: true
        )

        if (formatResult == 0) {
            echo "✅ Code style is consistent."
        } else {
            lintingStatus = 'UNSTABLE'
            echo "ℹ️ Formatting issues found. The build will be marked as unstable."
        }
    } catch (Exception e) {
        lintingStatus = 'UNSTABLE'
        echo "❌ Linting check encountered an error: ${e.getMessage()}"
    } finally {
        publishLintResults()
        if (lintingStatus == 'UNSTABLE') {
            currentBuild.result = 'UNSTABLE'
        }
    }
}

/**
 * Publishes linting results to Jenkins UI by converting the report to SARIF.
 */
def publishLintResults() {
    echo "📊 Publishing linting results..."
    def reportJsonFile = "${LINTER_REPORTS_DIR}/dotnet-format.json"
    def reportSarifFile = "${LINTER_REPORTS_DIR}/linting-report.sarif"

    if (!fileExists(reportJsonFile)) {
        echo "⚠️ Linting report file not found. Skipping publishing."
        return
    }

    try {
        archiveArtifacts artifacts: reportJsonFile, allowEmptyArchive: true

        def jsonContent = readJSON file: reportJsonFile
        def sarifReport = convertDotnetFormatToSarif(jsonContent)
        writeJSON file: reportSarifFile, json: sarifReport, pretty: 4

        recordIssues(
            tool: sarif(pattern: reportSarifFile, id: 'dotnet-format', name: 'Linting'),
            qualityGates: [
                [threshold: 1, type: 'TOTAL', unstable: true]
            ]
        )
        echo "✅ Linting results published to Jenkins UI."

    } catch (Exception e) {
        echo "⚠️ Could not publish linting results: ${e.getMessage()}"
    }
}

/**
 * Converts the JSON output from `dotnet-format` to the standard SARIF format.
 * This version correctly handles the actual report structure.
 * @param jsonReport The parsed JSON object from the dotnet-format report (which is a List).
 * @return A Map representing the SARIF report.
 */
def convertDotnetFormatToSarif(List jsonReport) {
    def results = []

    jsonReport.each { doc ->
        def relativePath = doc.FilePath.replace("${env.WORKSPACE}/", "")

        if (doc.FileChanges) {
            doc.FileChanges.each { change ->
                results.add([
                    ruleId: change.DiagnosticId ?: "WHITESPACE",
                    level: "note", // Treat pure formatting as a "note" level issue.
                    message: [ text: change.FormatDescription ],
                    locations: [[
                        physicalLocation: [
                            artifactLocation: [ uri: relativePath ],
                            region: [ startLine: change.LineNumber, startColumn: change.CharNumber ]
                        ]
                    ]]
                ])
            }
        }
    }

    // Construct the final SARIF structure
    def sarif = [
        '$schema': "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json",
        version: "2.1.0",
        runs: [[
            tool: [
                driver: [
                    name: "dotnet-format",
                    rules: []
                ]
            ],
            results: results
        ]]
    ]
    return sarif
}

//---------------------------------
// Test Project Discovery Helpers
//---------------------------------

/**
 * Discovers NUnit test projects in the workspace.
 */
def discoverTestProjects() {
    echo "🔍 Discovering NUnit test projects..."
    def nunitProjectsList = findNunitProjects()

    if (nunitProjectsList.isEmpty()) {
        echo "⚠️ No NUnit projects discovered automatically. Using fallback."
        def fallbackProjects = env.FALLBACK_NUNIT_PROJECTS ?: './csharp-nunit/Calculator.Tests/Calculator.Tests.csproj'
        nunitProjectsList = fallbackProjects.split(',').findAll { proj ->
            if (fileExists(proj.trim())) {
                return true
            } else {
                echo "❌ Fallback project not found: ${proj.trim()}"
                return false
            }
        }
    }

    if (nunitProjectsList.isEmpty()) {
        error("❌ No valid NUnit test projects found, and no valid fallback projects available.")
    }

    env.NUNIT_PROJECTS = nunitProjectsList.join(',')
    echo "🎯 Final NUnit projects (${nunitProjectsList.size()}):"
    nunitProjectsList.each { project -> echo "   → ${project}" }
}

/**
 * Finds files matching a pattern and returns a list of NUnit projects.
 */
def findNunitProjects() {
    def csprojFiles = findFiles(glob: '**/*Tests/*.csproj') + findFiles(glob: '**/*.Test.csproj')
    def nunitProjects = []

    csprojFiles.each { projectFile ->
        def projectContent = readFile(projectFile.path)
        if (projectContent.contains('NUnit') && projectContent.contains('Microsoft.NET.Test.Sdk')) {
            echo "✅ NUnit project detected: ${projectFile.path}"
            nunitProjects.add(projectFile.path)
        }
    }
    return nunitProjects
}


//---------------------------------
// Reporting & Archiving Helpers
//---------------------------------

/**
 * Archives and publishes test results.
 */
def archiveAndPublishTestResults() {
    echo "📊 Archiving and publishing test results..."

    // Archive raw TRX files
    if (fileExists(TEST_RESULTS_DIR)) {
        archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*.trx", allowEmptyArchive: true
    }

    // Publish test results to Jenkins UI
    try {
        if (findFiles(glob: "${TEST_RESULTS_DIR}/*.trx")) {
            mstest testResultsFile: "${TEST_RESULTS_DIR}/*.trx", failOnError: false, keepLongStdio: true
            echo "✅ Test results published to Jenkins UI"
        } else {
            echo "ℹ️ No test result files found to publish."
        }
    } catch (Exception e) {
        echo "⚠️ Could not publish test results: ${e.getMessage()}"
    }

    // Publish coverage reports if they exist
    if (params.GENERATE_COVERAGE && fileExists("${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml")) {
        try {
            recordCoverage tools: [[parser: 'COBERTURA', pattern: "${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml"]], sourceCodeRetention: 'EVERY_BUILD'
            archiveArtifacts artifacts: "${COVERAGE_REPORTS_DIR}/**", allowEmptyArchive: true, fingerprint: true
            echo "✅ Coverage reports published and archived"
        } catch (Exception e) {
            echo "⚠️ Could not publish or archive coverage reports: ${e.getMessage()}"
        }
    }
}

/**
 * Archives all security reports.
 */
def archiveSecurityReports() {
    echo "📊 Archiving security reports..."
    try {
        archiveArtifacts artifacts: "${SECURITY_REPORTS_DIR}/**/*", allowEmptyArchive: true, fingerprint: true
        echo "✅ Security reports archived."
    } catch (e) {
        echo "⚠️ Could not archive security reports: ${e.getMessage()}"
    }
}

/**
 * Publishes security scan results to the Jenkins UI.
 */
def publishSecurityResults() {
    echo "📋 Publishing security results..."
    // Publish Semgrep SARIF results
    if (fileExists("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif")) {
        recordIssues tool: sarif(pattern: "${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif", id: 'semgrep', name: 'SAST (Semgrep)'),
                     qualityGates: [[threshold: 1, type: 'TOTAL_ERROR', unstable: true]]
    }
    // Publish Trivy SARIF results
    if (fileExists("${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")) {
        recordIssues tool: sarif(pattern: "${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif", id: 'trivy', name: 'Security (Trivy)'),
                     qualityGates: [[threshold: 5, type: 'TOTAL_HIGH', unstable: true]]
    }
}


//---------------------------------
// JFrog Artifactory Helpers
//---------------------------------

/**
 * The main controller function for uploads.
 * It gathers all artifact rules, builds a single File Spec, and runs one upload command.
 */
def uploadArtifacts() {
    echo "📦 Preparing to upload artifacts using best practices..."
    try {
        // 1. Verify connection to Artifactory
        jf 'rt ping'
        echo "✅ JFrog Artifactory connection successful."

        // 2. Gather all upload rules from separate helper functions
        def allSpecEntries = []
        allSpecEntries.addAll(getBinarySpecEntries())
        allSpecEntries.addAll(getNugetSpecEntries())
        allSpecEntries.addAll(getReportSpecEntries())

        // 3. Proceed only if there are items to upload
        if (allSpecEntries.size() > 0) {
            def spec = [files: allSpecEntries]
            writeFile file: 'upload-spec.json', text: groovy.json.JsonOutput.toJson(spec)
            echo "📝 Generated a single, unified upload spec:"
            sh 'cat upload-spec.json'

            // 4. Execute a single upload command using the spec
            jf "rt u --spec=upload-spec.json --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER}"
            
            // 5. Publish all collected build information in one go
            jf "rt bp ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
            
            echo "✅ Successfully uploaded all artifacts and published build info."
        } else {
            echo "⚠️ No artifacts found to upload. Skipping."
        }

    } catch (e) {
        echo "❌ JFrog Artifactory stage failed: ${e.getMessage()}"
        currentBuild.result = 'UNSTABLE'
    }
}

/**
 * Finds .NET binaries and returns their File Spec rules.
 */
def List getBinarySpecEntries() {
    def entries = []
    // Looks in both Release and Debug folders to find the artifacts
    def binaryFiles = findFiles(glob: '**/bin/{Release,Debug}/**/*.*')
    if (binaryFiles.size() > 0) {
        echo "  - Found ${binaryFiles.size()} .NET binaries to upload."
        entries.add([
            "pattern": "*/bin/{Release,Debug}/",
            "target": "${ARTIFACTORY_REPO_BINARIES}/${JOB_NAME}/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    return entries
}

/**
 * Finds NuGet packages and returns their File Spec rules.
 */
def List getNugetSpecEntries() {
    def entries = []
    def nugetFiles = findFiles(glob: '**/bin/{Release,Debug}/*.nupkg')
    if (nugetFiles.size() > 0) {
        echo "  - Found ${nugetFiles.size()} NuGet packages to upload."
        entries.add([
            "pattern": "*/bin/{Release,Debug}/*.nupkg",
            "target": "${ARTIFACTORY_REPO_NUGET}/",
            "flat": "true"
        ])
    }
    return entries
}

/**
 * Finds build reports and returns their File Spec rules.
 */
def List getReportSpecEntries() {
    def entries = []
    if (fileExists(TEST_RESULTS_DIR)) {
        echo "  - Found test results to upload."
        entries.add([
            "pattern": "${TEST_RESULTS_DIR}/(*.trx)",
            "target": "${ARTIFACTORY_REPO_REPORTS}/test-results/${BUILD_NUMBER}/"
        ])
    }
    if (fileExists(COVERAGE_REPORTS_DIR)) {
        echo "  - Found coverage reports to upload."
        entries.add([
            "pattern": "${COVERAGE_REPORTS_DIR}/",
            "target": "${ARTIFACTORY_REPO_REPORTS}/coverage/${BUILD_NUMBER}/",
            "recursive": "true"
        ])
    }
    return entries
}


//---------------------------------
// Quality Gate and Summary Helpers
//---------------------------------

/**
 * Evaluates the overall quality gate based on build status and scan results.
 */
def evaluateQualityGate() {
    echo "🚦 Evaluating comprehensive quality gate..."
    def buildStatus = currentBuild.currentResult
    echo "   Current Build Status: ${buildStatus}"

    if (buildStatus == 'FAILURE') {
        error "❌ Quality gate failed: Build has failed."
    }
    if (buildStatus == 'UNSTABLE') {
        echo "⚠️ Quality gate warning: Build is unstable."
    }
    if (buildStatus == 'SUCCESS') {
        echo "✅ Quality gate passed!"
    }
}

/**
 * Generates a summary of all security scans.
 */
def generateSecuritySummary() {
    echo "📊 Generating security summary..."
    // This function can be expanded to parse report files for a more detailed summary.
    def summary = """
    🔒 Security Scan Summary
    ========================
    - Build: ${BUILD_NUMBER}
    - Scan Level: ${params.SECURITY_SCAN_LEVEL}
    - Status: ${currentBuild.currentResult}
    - Tool Installation Method: ${env.TOOL_INSTALL_METHOD}
    - Environment Configuration:
      * Semgrep: ${env.INSTALL_SEMGREP}
      * Trivy: ${env.INSTALL_TRIVY}
      * Gitleaks: ${env.INSTALL_GITLEAKS}
    """
    echo summary
    writeFile file: "${SECURITY_REPORTS_DIR}/security-summary.txt", text: summary
}

/**
 * Evaluates security gates and fails the build if configured.
 */
def evaluateSecurityGates() {
    if (!params.FAIL_ON_SECURITY_ISSUES) return

    echo "🚧 Evaluating security gates..."
    // This logic can be enhanced by parsing SARIF files for exact critical issue counts.
    // For now, it relies on the status set by other stages.
    if (currentBuild.currentResult == 'UNSTABLE') {
        // You could add more specific checks here
        echo "⚠️ Build is unstable due to security issues."
    }
}

//---------------------------------
// Installation Helpers for Tools
//---------------------------------

def installSemgrep() {
    echo "📦 Installing Semgrep..."
    if (env.FORCE_TOOL_REINSTALL == 'true') {
        sh 'pip3 uninstall -y semgrep || true'
    }
    sh """
        pip3 install --user semgrep --quiet || pip install --user semgrep --quiet || true
        export PATH="\$PATH:\$HOME/.local/bin"
        semgrep --version || echo "⚠️ Semgrep installation verification failed"
    """
}

def installTrivy() {
    echo "📦 Installing Trivy..."
    def trivyVersion = env.TRIVY_VERSION
    def trivyDir = "${env.WORKSPACE}/trivy-bin"
    
    if (env.FORCE_TOOL_REINSTALL == 'true') {
        sh "rm -rf ${trivyDir}"
    }
    
    sh """
        TRIVY_DIR=${trivyDir}
        mkdir -p \$TRIVY_DIR
        if [ ! -f \$TRIVY_DIR/trivy ] || [ "${env.FORCE_TOOL_REINSTALL}" = "true" ]; then
            echo "📥 Downloading Trivy v${trivyVersion}..."
            wget -q https://github.com/aquasecurity/trivy/releases/download/v${trivyVersion}/trivy_${trivyVersion}_Linux-64bit.tar.gz
            tar -xzf trivy_${trivyVersion}_Linux-64bit.tar.gz -C \$TRIVY_DIR trivy
            chmod +x \$TRIVY_DIR/trivy
            rm trivy_${trivyVersion}_Linux-64bit.tar.gz
        fi
        \$TRIVY_DIR/trivy --version
        echo "📥 Downloading Trivy vulnerability database..."
        \$TRIVY_DIR/trivy image --download-db-only
    """
}

def installGitleaks() {
    echo "📦 Installing Gitleaks..."
    def gitleaksVersion = env.GITLEAKS_VERSION
    
    if (env.FORCE_TOOL_REINSTALL == 'true') {
        sh 'rm -f gitleaks'
    }
    
    sh """
        if [ ! -f gitleaks ] || [ "${env.FORCE_TOOL_REINSTALL}" = "true" ]; then
            echo "📥 Downloading Gitleaks v${gitleaksVersion}..."
            wget -q https://github.com/gitleaks/gitleaks/releases/download/v${gitleaksVersion}/gitleaks_${gitleaksVersion}_linux_x64.tar.gz
            tar -xzf gitleaks_${gitleaksVersion}_linux_x64.tar.gz
            chmod +x gitleaks
            rm gitleaks_${gitleaksVersion}_linux_x64.tar.gz
        fi
        ./gitleaks version
    """
}