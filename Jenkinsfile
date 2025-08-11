/**
 * Refactored Jenkins Pipeline for a .NET Project
 *
 * This pipeline automates the build, test, analysis, and deployment of a .NET application.
 * It includes stages for:
 * - Compiling the code
 * - Running NUnit tests and generating coverage reports (conditionally)
 * - Performing Static Application Security Testing (SAST) with SonarQube and Semgrep
 * - Linting and secrets detection
 * - Publishing artifacts to JFrog Artifactory
 */

pipeline {
    agent {
        label 'ci-agent'
    }

    tools {
        // Configure JFrog CLI tool in Jenkins Global Tool Configuration
        jfrog 'jfrog-cli'
        // // Configure SonarQube Scanner in Jenkins Global Tool Configuration
        // sonarQubeScanner 'FOS-SonarScanner'
    }

    // =========================================================================
    // Environment Variables
    // =========================================================================
    environment {
        // --- .NET Configuration ---
        DOTNET_VERSION = '9.0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        DOTNET_FORMAT_VERSION = '7.0.400' // Compatible with your SDK
        DOTNET_VERBOSITY = 'n' // Default verbosity (n: normal, q: quiet, m: minimal, d: detailed)

        // --- Project Paths (discovered at runtime) ---
        SOLUTION_FILE_PATH = ''
        MAIN_PROJECT_PATH = ''
        NUNIT_PROJECTS = ''

        // --- Reporting Directories ---
        TEST_RESULTS_DIR = 'test-results'
        COVERAGE_REPORTS_DIR = 'coverage-reports'
        LINTER_REPORTS_DIR = 'linter-reports'
        SECURITY_REPORTS_DIR = 'security-reports'
        SECRETS_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/secrets"
        DAST_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/dast"

        // // --- Manual SonarQube Configuration --- (if needed)
        // SONARQUBE_URL = "http://${env.host_ip}:9000"

        // -- SonarQube Project Key ---
        SONAR_PROJECT_KEY = 'CI' // Replace with your SonarQube project key

        // -- Jenkins-configured SonarQube Server ---
        SONARQUBE_ENV = 'FOS-SonarQube'

        // --- JFrog Artifactory Configuration ---
        JFROG_CLI_BUILD_NAME = "${JOB_NAME}"
        JFROG_CLI_BUILD_NUMBER = "${BUILD_NUMBER}"
        ARTIFACTORY_REPO_BINARIES = 'libs-release-local'
        ARTIFACTORY_REPO_NUGET = 'nuget-local'
        ARTIFACTORY_REPO_REPORTS = 'reports-local'

        // --- Security Tool Configuration ---
        DEPENDENCY_CHECK_VERSION = '9.2.0'
        SEMGREP_TIMEOUT = '300'
        GITLEAKS_VERSION = '8.18.2'

        // --- Additional Artifact Configuration ---
        FAIL_ON_NO_ARTIFACTS = false
        DEBUG_MODE = false

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
        // -- Build Number ---
        string(name: 'MAJOR_VERSION', defaultValue: '1', description: 'Major version')
        string(name: 'MINOR_VERSION', defaultValue: '1', description: 'Minor version')

        // --- Test & Coverage ---
        booleanParam(name: 'RUN_UNIT_TESTS', defaultValue: true, description: 'Run NUnit tests and generate coverage reports if test projects are found.')
        booleanParam(name: 'GENERATE_COVERAGE', defaultValue: true, description: 'Generate code coverage reports')
        booleanParam(name: 'FAIL_ON_TEST_FAILURE', defaultValue: true, description: 'Fail the build if any tests fail')
        choice(name: 'LOG_LEVEL', choices: ['INFO', 'DEBUG', 'WARN', 'ERROR'], description: 'Set logging level for test execution')

        // --- Security Scans ---
        booleanParam(name: 'ENABLE_SECURITY_SCAN', defaultValue: true, description: 'Enable comprehensive security scanning')
        booleanParam(name: 'FAIL_ON_SECURITY_ISSUES', defaultValue: false, description: 'Fail build on critical security vulnerabilities')
        choice(name: 'SECURITY_SCAN_LEVEL', choices: ['BASIC', 'FULL'], description: 'Security scanning depth level')
        booleanParam(name: 'ENABLE_LINTING', defaultValue: true, description: 'Enable .NET code style linting with dotnet-format')
        booleanParam(name: 'ENABLE_SECRETS_SCAN', defaultValue: true, description: 'Enable secrets detection scan with Gitleaks')

        // -- Artifact Upload ---
        booleanParam(name: 'UPLOAD_SOURCE_CODE', defaultValue: false, description: 'Upload source code into JFrog artifactory')
        booleanParam(name: 'CREATE_DEPLOYMENT_PACKAGE', defaultValue: true, description: 'Create and upload the final app.zip deployment package')

        // --- DAST Parameters (Placeholder) ---
        // booleanParam(name: 'ENABLE_DAST_SCAN', defaultValue: false, description: 'Enable Dynamic Application Security Testing (DAST)')
        // string(name: 'STAGING_URL', defaultValue: 'http://your-staging-app.example.com', description: 'URL of the staging application for DAST')

        // --- Notification Parameters (Placeholder) ---
        // string(name: 'SLACK_CHANNEL', defaultValue: '#ci-alerts', description: 'Slack channel for notifications')
        // credentials(name: 'SLACK_CREDENTIAL_ID', description: 'Jenkins credential ID for the Slack Bot Token', required: false)
    }

    // =========================================================================
    // Pipeline Stages
    // =========================================================================
    stages {
        stage('Prepare Version') {
            steps {
                script {
                    def major = params.MAJOR_VERSION
                    def minor = params.MINOR_VERSION
                    def repository = "zacst/pipeline" // IMPORTANT: Change this

                    echo "Searching Docker Hub for latest patch of ${repository}:${major}.${minor}.x"

                    // 1. Use curl to get the raw JSON text. No parsing is done in the shell.
                    def responseText = sh(
                        script: "curl -s 'https://hub.docker.com/v2/repositories/${repository}/tags/?page_size=250'",
                        returnStdout: true
                    ).trim()

                    // 2. Use Jenkins' built-in readJSON to parse the text into a Groovy object
                    def responseData = readJSON(text: responseText)
                    
                    int highestPatch = -1
                    def pattern = ~/(\d+)\.(\d+)\.(\d+)/

                    // 3. Loop through the results in Groovy - much cleaner than a bash loop
                    responseData.results.each { tag ->
                        def tagName = tag.name
                        def matcher = (tagName =~ pattern)
                        
                        if (matcher.matches()) {
                            def tagMajor = matcher[0][1]
                            def tagMinor = matcher[0][2]
                            def tagPatch = matcher[0][3].toInteger()

                            if (tagMajor == major && tagMinor == minor) {
                                if (tagPatch > highestPatch) {
                                    highestPatch = tagPatch
                                }
                            }
                        }
                    }

                    // The rest of the logic is the same
                    def patch = (highestPatch >= 0) ? highestPatch + 1 : 0
                    
                    if (highestPatch >= 0) {
                        echo "Found highest existing patch: ${highestPatch}. New patch will be: ${patch}"
                    } else {
                        echo "No existing tags found for ${major}.${minor}.*. Starting new series with patch: 0"
                    }
                    
                    env.FULL_VERSION = "${major}.${minor}.${patch}"
                    env.IMAGE_TAG = env.FULL_VERSION
                    currentBuild.displayName = env.FULL_VERSION
                    
                    echo "Version set to: ${env.FULL_VERSION}"
                }
            }
        }

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

        stage('Discover Projects & Solution') {
            steps {
                script {
                    discoverProjectsAndSolution()
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
            when { expression { params.SECURITY_SCAN_LEVEL in ['FULL'] } }
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

        stage('Package for Deployment') {
            when { expression { params.CREATE_DEPLOYMENT_PACKAGE } }
            steps {
                script {
                    packageForDeployment()
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
 * Initializes build-wide variables.
 */
def initializeBuild() {
    echo "🔧 Initializing build..."
    // Set .NET verbosity based on the LOG_LEVEL parameter
    def verbosityMapping = [INFO: 'n', DEBUG: 'd', WARN: 'm', ERROR: 'q']
    env.DOTNET_VERBOSITY = verbosityMapping.get(params.LOG_LEVEL, 'n')
    echo "🔧 dotnet verbosity set to: ${env.DOTNET_VERBOSITY}"
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
    if (env.SOLUTION_FILE_PATH) {
        sh "dotnet restore '${env.SOLUTION_FILE_PATH}' --verbosity ${env.DOTNET_VERBOSITY}"
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

    withSonarQubeEnv("${SONARQUBE_ENV}") {
        try {
            // Uncomment if you want to install SonarScanner globally
            installDotnetTool('dotnet-sonarscanner')
            
            startDotnetSonarScanner()
            buildSolution()

            // *** Conditionally run tests and coverage ***
            if (params.RUN_UNIT_TESTS && env.NUNIT_PROJECTS) {
                runUnitTests()
                if (params.GENERATE_COVERAGE) {
                    generateCoverageReports()
                }
            } else {
                echo "ℹ️ Skipping unit tests and coverage generation as per configuration or no test projects found."
            }

        } catch (e) {
            echo "❌ Build, Test, or SonarQube analysis failed: ${e.getMessage()}"
            if (params.FAIL_ON_TEST_FAILURE) {
                throw e
            } else {
                currentBuild.result = 'UNSTABLE'
                echo "⚠️ Build marked as unstable due to failures."
            }
        } finally {
            // If dotnet-sonarscanner was used
            endDotnetSonarScanner()
        }
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
def startDotnetSonarScanner() {
    echo "🔍 Starting SonarQube analysis..."
    
    // Base command for SonarQube scanner
    def sonarBeginCmd = """
        export PATH="\$PATH:\$HOME/.dotnet/tools"
        dotnet sonarscanner begin \\
            /key:"\$SONAR_PROJECT_KEY" \\
            /d:sonar.host.url="\$SONAR_HOST_URL" \\
            /d:sonar.login="\$SONAR_AUTH_TOKEN" \\
            /d:sonar.exclusions="**/bin/**,**/obj/**,**/*.Tests/**,**/security-reports/**,**/coverage-reports/**" \\
            /d:sonar.test.exclusions="**/*.Tests/**" \\
            /d:sonar.coverage.exclusions="**/*.Tests/**"
    """

    // Conditionally add test and coverage report paths if tests are enabled
    if (params.RUN_UNIT_TESTS && env.NUNIT_PROJECTS) {
        sonarBeginCmd += """ \\
            /d:sonar.cs.nunit.reportsPaths="\$TEST_RESULTS_DIR/*.trx" \\
            /d:sonar.cs.opencover.reportsPaths="**/coverage.cobertura.xml"
        """
        echo "   -> Including test and coverage reports in SonarQube analysis."
    }

    sh sonarBeginCmd
}


/**
 * Builds the .NET solution.
 */
def buildSolution() {
    echo "🔨 Building .NET solution..."
    if (env.SOLUTION_FILE_PATH) {
        echo "🔨 Building solution: ${env.SOLUTION_FILE_PATH}"
        sh "dotnet build '${env.SOLUTION_FILE_PATH}' --configuration Release --no-restore --verbosity ${env.DOTNET_VERBOSITY}"
    } else {
        echo "🔨 No solution file found, building all projects in the repository..."
        sh "dotnet build --configuration Release --no-restore --verbosity ${env.DOTNET_VERBOSITY}"
    }
}

/**
 * Finds all files that are C# test projects.
 */
def findUnitProjects() {
    // A more reliable pattern to find all test projects
    def csprojFiles = findFiles(glob: '**/*.csproj')

    def testProjects = csprojFiles.findAll { projectFile ->
        def projectContent = readFile(projectFile.path)
        // The presence of 'Microsoft.NET.Test.Sdk' is the correct way to identify a test project.
        return projectContent.contains('Microsoft.NET.Test.Sdk')
    }

    if (!testProjects.isEmpty()) {
        echo "✅ Found ${testProjects.size()} test project(s)."
        return testProjects.collect { it.path }
    } else {
        return []
    }
}

/**
 * Executes 'dotnet test' on all discovered unit test projects.
 */
def runUnitTests() {
    echo "🧪 Running unit tests..."
    // Ensure the tools for coverage are installed
    installDotnetTool('coverlet.console')

    unitProjectPaths.each { projectPath ->
        try {
            echo "   -> Testing project: ${projectPath}"
            // Sanitize project name to use in file paths
            def projectName = projectPath.split('/')[-1].replace('.csproj', '')
            
            def testCommand = """
                dotnet test '${projectPath}' \\
                    --configuration Release \\
                    --no-build \\
                    --logger "trx;LogFileName=${TEST_RESULTS_DIR}/${projectName}-test-results.trx" \\
                    /p:CollectCoverage=true \\
                    /p:CoverletOutputFormat=cobertura \\
                    /p:CoverletOutput='${TEST_RESULTS_DIR}/${projectName}.coverage.cobertura.xml'
            """
            sh testCommand
        } catch (e) {
            echo "❌ Test execution failed for project: ${projectPath}"
            // Depending on params, either fail fast or continue
            if (params.FAIL_ON_TEST_FAILURE) {
                throw e
            }
        }
    }
}

/**
 * Generates HTML coverage reports from Cobertura XML files.
 */
def generateCoverageReports() {
    echo "📊 Generating coverage reports..."
    def coverageFiles = findFiles(glob: '**/coverage.cobertura.xml')
    if (coverageFiles) {
        installDotnetTool('dotnet-reportgenerator-globaltool')
        sh """
            export PATH="\$PATH:\$HOME/.dotnet/tools"
            reportgenerator \\
                -reports:**/coverage.cobertura.xml \\
                -targetdir:${COVERAGE_REPORTS_DIR}/dotnet \\
                -reporttypes:Html,Cobertura,JsonSummary \\
                -verbosity:${params.LOG_LEVEL}
        """
    } else {
        echo "⚠️ No coverage files found to generate reports from."
    }
}

/**
 * Ends the SonarQube scanner analysis.
 */
def endDotnetSonarScanner() {
    try {
        echo "🔍 Completing SonarQube analysis..."
        sh '''
            export PATH="$PATH:$HOME/.dotnet/tools"
            dotnet sonarscanner end /d:sonar.login=$SONAR_AUTH_TOKEN
        '''
    } catch (Exception e) {
        echo "⚠️ Could not end SonarQube analysis gracefully: ${e.getMessage()}"
    }
}

/**
 * Creates the final deployment package by publishing and zipping the application.
 */
def packageForDeployment() {
    echo "📦 Creating deployment package..."

    if (!env.MAIN_PROJECT_PATH) {
        error "❌ Main application project path not found. Cannot create deployment package."
    }

    def projectPath = env.MAIN_PROJECT_PATH
    echo "🎯 Publishing project for deployment: ${projectPath}"

    // Publish the final, runnable artifacts to the 'publish' directory
    sh "dotnet publish '${projectPath}' --configuration Release --output ./publish"

    // Use the built-in Jenkins zip step
    zip(zipFile: 'app.zip', dir: 'publish')

    echo "✅ Created deployment package: app.zip"
}


//---------------------------------
// Security Scan Helpers
//---------------------------------

/**
 * Runs Semgrep SAST scan.
 */
def runSemgrepScan() {
    echo "🔒 Running Semgrep SAST analysis..."
    try {
        sh "mkdir -p ${SECURITY_REPORTS_DIR}/semgrep"
        installSemgrep()
        def semgrepRules = (params.SECURITY_SCAN_LEVEL in ['FULL']) ?
            '--config=auto --config=p/cwe-top-25 --config=p/owasp-top-10' :
            '--config=auto'

        sh """
            export PATH="\$PATH:\$HOME/.local/bin"
            timeout ${SEMGREP_TIMEOUT} semgrep \\
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
 * Runs Trivy for container security scanning.
 */
def runTrivyContainerScan() {
    echo "🔒 Running Trivy container security scan..."
    try {
        sh "mkdir -p ${SECURITY_REPORTS_DIR}/trivy"
        installTrivy()
        sh """
            TRIVY_DIR=\$(pwd)/trivy-bin
            \$TRIVY_DIR/trivy fs \\
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
 * Runs Gitleaks for secrets detection.
 */
def runSecretsScan() {
    echo "🤫 Running Secrets Detection (Gitleaks)..."
    try {
        sh "mkdir -p ${SECRETS_REPORTS_DIR}"
        sh """
            wget -q https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz
            tar -xzf gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz
            chmod +x gitleaks
            ./gitleaks detect --source="." --report-path="${SECRETS_REPORTS_DIR}/gitleaks-report.sarif" --report-format="sarif" --exit-code 0
        """
        processGitleaksResults()
    } catch (Exception e) {
        currentBuild.result = 'UNSTABLE'
        echo "❌ Gitleaks scan failed to execute: ${e.getMessage()}"
    }
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
        if (!env.SOLUTION_FILE_PATH) {
            echo "⚠️ Solution file not found. Skipping linter."
            return
        }

        installDotnetTool('dotnet-format', env.DOTNET_FORMAT_VERSION)

        def projectPath = env.SOLUTION_FILE_PATH
        def formatResult = sh(
            script: """
                export PATH="\$PATH:\$HOME/.dotnet/tools"
                dotnet format '${projectPath}' --verify-no-changes --report ${LINTER_REPORTS_DIR}/dotnet-format.json --verbosity diagnostic
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
// Project Discovery Helpers
//---------------------------------

/**
 * Main discovery function to find solution, main project, and test projects.
 */
def discoverProjectsAndSolution() {
    discoverSolutionFile()
    discoverMainProject()
    discoverTestProjects()
}

/**
 * Discovers the main .sln file in the workspace.
 */
def discoverSolutionFile() {
    echo "🔍 Discovering .NET solution file (.sln)..."
    def solutionFiles = findFiles(glob: '**/*.sln')
    echo "[DEBUG] Found ${solutionFiles.length} solution file(s). Paths: ${solutionFiles.collect { it.path }}"
    if (solutionFiles.length > 0) {
        if (solutionFiles.length > 1) {
            echo "⚠️ Multiple solution files found. Using the first one: ${solutionFiles[0].path}"
        }
        env.SOLUTION_FILE_PATH = solutionFiles[0].path
        echo "🎯 Solution file set to: ${env.SOLUTION_FILE_PATH}"
    } else {
        echo "⚠️ No .sln file found. Build steps will run on the repository root."
        env.SOLUTION_FILE_PATH = ''
    }
}

/**
 * Discovers the main, publishable application project.
 */
def discoverMainProject() {
    echo "🔍 Discovering main application project for packaging..."
    def allProjects = findFiles(glob: '**/*.csproj')
    def testProjects = findFiles(glob: '**/*Tests/*.csproj') + findFiles(glob: '**/*.Test.csproj')
    def testProjectPaths = testProjects.collect { it.path }

    echo "[DEBUG] Found ${allProjects.size()} total .csproj files."
    echo "[DEBUG] Found ${testProjects.size()} test projects matching patterns."
    
    // Filter out test projects
    def mainProjects = allProjects.findAll { proj -> !testProjectPaths.contains(proj.path) }

    echo "[DEBUG] After filtering, found ${mainProjects.size()} main projects."

    if (!mainProjects.isEmpty()) {
        // Heuristic: Prefer projects that look like executables or web apps
        def publishableProject = mainProjects.find { p ->
            def content = readFile(p.path)
            // Check for OutputType Exe or presence of web-related SDKs
            return content.contains('<OutputType>Exe</OutputType>') || content.contains('Microsoft.NET.Sdk.Web')
        } ?: mainProjects[0] // Fallback to the first non-test project

        env.MAIN_PROJECT_PATH = publishableProject.path
        echo "🎯 Main project for packaging set to: ${env.MAIN_PROJECT_PATH}"
    } else {
        echo "⚠️ No main application project found. The 'Package for Deployment' stage may fail."
        env.MAIN_PROJECT_PATH = ''
    }
}

/**
 * Discovers a single NUnit test project and sets its path as an environment variable.
 */
def discoverTestProjects() {
    echo "🔍 Discovering NUnit test projects..."
    def nunitProjectsList = findNunitProjects()

    if (!nunitProjectsList.isEmpty()) {
        env.NUNIT_PROJECTS = nunitProjectsList[0] // Using the first one found
        echo "🎯 NUnit project for testing set to: ${env.NUNIT_PROJECTS}"
    } else {
        echo "ℹ️ No NUnit test projects found. Unit testing and coverage will be skipped."
        env.NUNIT_PROJECTS = ''
    }
}

/**
 * Finds files matching a pattern and returns a list of NUnit projects.
 */
def findNunitProjects() {
    def csprojFiles = findFiles(glob: '**/*Tests/*.csproj') + findFiles(glob: '**/*.Test.csproj')

    def nunitProjects = csprojFiles.findAll { projectFile ->
        def projectContent = readFile(projectFile.path)
        return projectContent.contains('NUnit') && projectContent.contains('Microsoft.NET.Test.Sdk')
    }

    if (!nunitProjects.isEmpty()) {
        echo "✅ Found ${nunitProjects.size()} NUnit project(s)."
        return nunitProjects.collect { it.path }
    } else {
        return []
    }
}


//---------------------------------
// Reporting & Archiving Helpers
//---------------------------------

/**
 * Archives and publishes test results.
 */
def archiveAndPublishTestResults() {
    // Only run if tests were supposed to run
    if (!params.RUN_UNIT_TESTS) return

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
// JFrog Artifactory Helpers - Improved Version
//---------------------------------

/**
 * The main controller function for uploads with enhanced error handling and configuration.
 */
def uploadArtifacts() {
    echo "📦 Preparing to upload artifacts using comprehensive best practices..."
    
    if (!validateEnvironment()) {
        return
    }
    
    try {
        timeout(time: 30, unit: 'SECONDS') {
            jf 'rt ping'
        }
        echo "✅ JFrog Artifactory connection successful."

        def allSpecEntries = []
        allSpecEntries.addAll(getDeploymentPackageSpecEntries())
        allSpecEntries.addAll(getBinarySpecEntries())
        allSpecEntries.addAll(getNugetSpecEntries())
        allSpecEntries.addAll(getReportSpecEntries())
        allSpecEntries.addAll(getDocumentationSpecEntries())
        allSpecEntries.addAll(getSecurityReportSpecEntries())
        allSpecEntries.addAll(getSourceCodeSpecEntries())
        allSpecEntries.addAll(getConfigurationSpecEntries())
        allSpecEntries.addAll(getBuildArtifactSpecEntries())
        allSpecEntries.addAll(getContainerImageSpecEntries())
        allSpecEntries.addAll(getDependencySpecEntries())

        if (!allSpecEntries.isEmpty()) {
            def spec = [files: allSpecEntries]
            writeFile file: 'upload-spec.json', text: groovy.json.JsonOutput.toJson(spec)
            echo "📝 Generated unified upload spec with ${allSpecEntries.size()} entries"
            
            if (env.getProperty('DEBUG_MODE') == 'true') {
                sh 'cat upload-spec.json'
            }

            def uploadCmd = "rt u --spec=upload-spec.json " +
                            "--build-name=${JFROG_CLI_BUILD_NAME} " +
                            "--build-number=${JFROG_CLI_BUILD_NUMBER} " +
                            "--detailed-summary " +
                            "--fail-no-op " +
                            "--threads=3"
            
            jf uploadCmd
            addBuildProperties()
            jf "rt bp ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
            
            echo "✅ Successfully uploaded all artifacts and published build info."
            
        } else {
            echo "⚠️ No artifacts found to upload."
            if (env.getProperty('FAIL_ON_NO_ARTIFACTS') == 'true') {
                error("No artifacts found but artifacts were expected")
            }
        }

    } catch (Exception e) {
        echo "❌ JFrog Artifactory upload failed: ${e.getMessage()}"
        currentBuild.result = 'FAILURE'
        throw e
    } finally {
        if (fileExists('upload-spec.json')) {
            sh 'rm -f upload-spec.json'
        }
    }
}


/**
 * Validates required environment variables and configuration.
 */
def validateEnvironment() {
    def requiredVars = [
        'JFROG_CLI_BUILD_NAME',
        'JFROG_CLI_BUILD_NUMBER', 
        'ARTIFACTORY_REPO_BINARIES',
        'ARTIFACTORY_REPO_NUGET',
        'ARTIFACTORY_REPO_REPORTS'
    ]
    
    def missing = []
    for (String varName : requiredVars) {
        def value = env.getProperty(varName)
        if (!value || value.trim().isEmpty()) {
            missing.add(varName)
        }
    }
    
    if (missing.size() > 0) {
        echo "❌ Missing required environment variables: ${missing.join(', ')}"
        currentBuild.result = 'FAILURE'
        return false
    }
    return true
}

/**
 * Adds build properties for better traceability and metadata.
 */
def addBuildProperties() {
    def gitCommit = env.getProperty('GIT_COMMIT') ?: 'unknown'
    def gitBranch = env.getProperty('GIT_BRANCH') ?: 'unknown'
    def jobName = env.getProperty('JOB_NAME') ?: 'unknown'
    def buildNumber = env.getProperty('BUILD_NUMBER') ?: 'unknown'
    
    // Enhanced environment variables
    env.BUILD_TIMESTAMP = new Date().format('yyyy-MM-dd HH:mm:ss')
    env.BUILD_TIMESTAMP_EPOCH = System.currentTimeMillis().toString()
    env.GIT_COMMIT_CUSTOM = gitCommit
    env.GIT_BRANCH_CUSTOM = gitBranch
    env.JENKINS_JOB_CUSTOM = jobName
    env.JENKINS_BUILD_CUSTOM = buildNumber
    env.DOTNET_VERSION_USED = sh(script: 'dotnet --version', returnStdout: true).trim()
    env.BUILD_USER = env.getProperty('BUILD_USER_ID') ?: 'system'
    env.BUILD_CAUSE = env.getProperty('BUILD_CAUSE') ?: 'unknown'
    
    // Add security scan results metadata
    if (params.ENABLE_SECURITY_SCAN) {
        env.SECURITY_SCAN_ENABLED = 'true'
        env.SECURITY_SCAN_LEVEL = params.SECURITY_SCAN_LEVEL
    }
    
    // Add test results metadata
    def testResultFiles = findFiles(glob: "${env.TEST_RESULTS_DIR}/*.trx")
    env.TEST_RESULTS_COUNT = testResultFiles.size().toString()
    
    jf "rt bce ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
}

/**
 * Enhanced binary artifacts with more comprehensive patterns
 */
def List getBinarySpecEntries() {
    def entries = []
    
    // More comprehensive binary patterns
    def binaryPatterns = [
        '**/bin/Release/**/*.{exe,dll,pdb,xml}',
        '**/bin/Debug/**/*.{exe,dll,pdb,xml}',
        '**/publish/**/*.{exe,dll,pdb,xml,json,config}',
        '**/out/**/*.{exe,dll,pdb}',
        '**/dist/**/*.{exe,dll,pdb}'
    ]
    
    binaryPatterns.each { pattern ->
        def binaryFiles = findFiles(glob: pattern)
        if (binaryFiles.size() > 0) {
            echo "   - Found ${binaryFiles.size()} binaries matching: ${pattern}"
            
            def config = pattern.contains('Release') ? 'release' : 
                         pattern.contains('Debug') ? 'debug' : 
                         pattern.contains('publish') ? 'published' : 'other'
            
            entries.add([
                "pattern": pattern,
                "target": "${ARTIFACTORY_REPO_BINARIES}/${JOB_NAME}/${BUILD_NUMBER}/${config}/",
                "recursive": "true",
                "flat": "false",
                "exclusions": ["**/*.tmp", "**/*.log", "**/obj/**", "**/*.cache"]
            ])
        }
    }
    
    return entries
}

/**
 * Enhanced NuGet packages with symbols and source packages
 */
def List getNugetSpecEntries() {
    def entries = []
    
    // Regular NuGet packages
    def nugetFiles = findFiles(glob: '**/bin/**/*.nupkg')
    if (nugetFiles.size() > 0) {
        echo "   - Found ${nugetFiles.size()} NuGet packages to upload."
        
        entries.add([
            "pattern": "**/bin/**/*.nupkg",
            "target": "${ARTIFACTORY_REPO_NUGET}/packages/",
            "flat": "false",
            "exclusions": ["**/*.symbols.nupkg", "**/*.snupkg"]
        ])
    }
    
    // Symbol packages (.symbols.nupkg)
    def symbolFiles = findFiles(glob: '**/bin/**/*.symbols.nupkg')
    if (symbolFiles.size() > 0) {
        echo "   - Found ${symbolFiles.size()} symbol packages to upload."
        entries.add([
            "pattern": "**/bin/**/*.symbols.nupkg",
            "target": "${ARTIFACTORY_REPO_NUGET}/symbols/",
            "flat": "false"
        ])
    }
    
    // Portable symbol packages (.snupkg)
    def portableSymbolFiles = findFiles(glob: '**/bin/**/*.snupkg')
    if (portableSymbolFiles.size() > 0) {
        echo "   - Found ${portableSymbolFiles.size()} portable symbol packages to upload."
        entries.add([
            "pattern": "**/bin/**/*.snupkg",
            "target": "${ARTIFACTORY_REPO_NUGET}/portable-symbols/",
            "flat": "false"
        ])
    }
    
    return entries
}

/**
 * Enhanced reports including all test and analysis reports
 */
def getReportSpecEntries() {
    def entries = []
    def timestamp = new Date().format('yyyy-MM-dd_HH-mm-ss')
    
    // Test results
    def testResultsDir = env.getProperty('TEST_RESULTS_DIR')
    if (testResultsDir && fileExists(testResultsDir)) {
        echo "   - Found test results to upload."
        entries.add([
            "pattern": "${testResultsDir}/**/*.{trx,xml,json,html}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/test-results/${BUILD_NUMBER}_${timestamp}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    // Coverage reports
    def coverageReportsDir = env.getProperty('COVERAGE_REPORTS_DIR')
    if (coverageReportsDir && fileExists(coverageReportsDir)) {
        echo "   - Found coverage reports to upload."
        entries.add([
            "pattern": "${coverageReportsDir}/**/*",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/coverage/${BUILD_NUMBER}_${timestamp}/",
            "recursive": "true",
            "flat": "false",
            "exclusions": ["**/*.tmp", "**/.git/**"]
        ])
    }
    
    // Linting reports
    def linterReportsDir = env.getProperty('LINTER_REPORTS_DIR')
    if (linterReportsDir && fileExists(linterReportsDir)) {
        echo "   - Found linting reports to upload."
        entries.add([
            "pattern": "${linterReportsDir}/**/*.{json,xml,sarif}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/linting/${BUILD_NUMBER}_${timestamp}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    // SonarQube reports (if available)
    def sonarReports = findFiles(glob: '.sonar/**/*')
    if (sonarReports.size() > 0) {
        echo "   - Found SonarQube analysis files to upload."
        entries.add([
            "pattern": ".sonar/**/*",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/sonar/${BUILD_NUMBER}_${timestamp}/",
            "recursive": "true",
            "flat": "false",
            "exclusions": ["**/.git/**", "**/cache/**"]
        ])
    }
    
    return entries
}

/**
 * Finds documentation artifacts.
 */
def List getDocumentationSpecEntries() {
    def entries = []
    
    // API documentation
    def docsDir = 'docs/api'
    if (fileExists(docsDir)) {
        echo "   - Found API documentation to upload."
        entries.add([
            "pattern": "${docsDir}/**/*",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/documentation/${BUILD_NUMBER}/api/",
            "recursive": "true"
        ])
    }
    
    // README and changelog
    def readmeFiles = findFiles(glob: '{README,CHANGELOG,RELEASE_NOTES}.{md,txt}')
    if (readmeFiles.size() > 0) {
        echo "   - Found ${readmeFiles.size()} documentation files to upload."
        entries.add([
            "pattern": "{README,CHANGELOG,RELEASE_NOTES}.{md,txt}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/documentation/${BUILD_NUMBER}/",
            "flat": "true"
        ])
    }
    
    return entries
}

/**
 * Security reports upload
 */
def List getSecurityReportSpecEntries() {
    def entries = []
    def timestamp = new Date().format('yyyy-MM-dd_HH-mm-ss')
    
    def securityReportsDir = env.getProperty('SECURITY_REPORTS_DIR')
    if (securityReportsDir && fileExists(securityReportsDir)) {
        echo "   - Found security reports to upload."
        entries.add([
            "pattern": "${securityReportsDir}/**/*.{sarif,json,xml,html,txt}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/security/${BUILD_NUMBER}_${timestamp}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    return entries
}

/**
 * Source code and project files
 */
def List getSourceCodeSpecEntries() {
    def entries = []
    
    // Only upload source if specifically requested
    if (params.UPLOAD_SOURCE_CODE == true) {
        echo "   - Preparing source code for upload."
        entries.add([
            "pattern": "**/*.{cs,csproj,sln,config,json,xml}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/source/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false",
            "exclusions": [
                "**/bin/**", "**/obj/**", "**/.git/**", 
                "**/packages/**", "**/node_modules/**",
                "**/*.tmp", "**/*.log", "**/.vs/**"
            ]
        ])
    }
    
    return entries
}

/**
 * Configuration files and deployment scripts
 */
def List getConfigurationSpecEntries() {
    def entries = []
    
    // Configuration files
    def configFiles = findFiles(glob: '**/*.{config,settings,properties,yaml,yml,json}')
    def deploymentConfigs = configFiles.findAll { file ->
        file.path.contains('config') || 
        file.path.contains('deploy') || 
        file.path.contains('environment') ||
        file.name.toLowerCase().contains('appsettings')
    }
    
    if (deploymentConfigs.size() > 0) {
        echo "   - Found ${deploymentConfigs.size()} configuration files to upload."
        entries.add([
            "pattern": "**/{appsettings,web,app}.{config,json,xml}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/configs/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false",
            "exclusions": ["**/bin/**", "**/obj/**"]
        ])
    }
    
    // Deployment scripts
    def scriptFiles = findFiles(glob: '**/*.{ps1,sh,bat,cmd}')
    if (scriptFiles.size() > 0) {
        echo "   - Found ${scriptFiles.size()} script files to upload."
        entries.add([
            "pattern": "**/*.{ps1,sh,bat,cmd}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/scripts/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false",
            "exclusions": ["**/node_modules/**", "**/.git/**"]
        ])
    }
    
    return entries
}

/**
 * Build artifacts and metadata
 */
def List getBuildArtifactSpecEntries() {
    def entries = []
    
    // Build logs and metadata
    if (fileExists('build.log')) {
        entries.add([
            "pattern": "build.log",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/build-logs/${BUILD_NUMBER}/",
            "flat": "true"
        ])
    }
    
    // MSBuild logs
    def msbuildLogs = findFiles(glob: '**/msbuild*.log')
    if (msbuildLogs.size() > 0) {
        echo "   - Found ${msbuildLogs.size()} MSBuild log files to upload."
        entries.add([
            "pattern": "**/msbuild*.log",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/msbuild-logs/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    // Dependency lock files
    def lockFiles = findFiles(glob: '**/packages.lock.json') + 
                    findFiles(glob: '**/project.lock.json') +
                    findFiles(glob: '**/project.assets.json')
    if (lockFiles.size() > 0) {
        echo "   - Found ${lockFiles.size()} dependency lock files to upload."
        entries.add([
            "pattern": "**/{packages.lock.json,project.lock.json,project.assets.json}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/dependencies/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    return entries
}

/**
 * Container images and Docker files
 */
def List getContainerImageSpecEntries() {
    def entries = []
    
    // Dockerfile and related files
    def dockerFiles = findFiles(glob: '**/Dockerfile*') + 
                      findFiles(glob: '**/.dockerignore') +
                      findFiles(glob: '**/docker-compose*.{yml,yaml}')
    if (dockerFiles.size() > 0) {
        echo "   - Found ${dockerFiles.size()} Docker-related files to upload."
        entries.add([
            "pattern": "**/Dockerfile*",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/docker/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
        entries.add([
            "pattern": "**/.dockerignore",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/docker/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
        entries.add([
            "pattern": "**/docker-compose*.{yml,yaml}",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/docker/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    return entries
}

/**
 * Dependency reports and license information
 */
def List getDependencySpecEntries() {
    def entries = []
    
    // NuGet packages.config files
    def packagesConfigs = findFiles(glob: '**/packages.config')
    if (packagesConfigs.size() > 0) {
        echo "   - Found ${packagesConfigs.size()} packages.config files to upload."
        entries.add([
            "pattern": "**/packages.config",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/package-configs/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    // License files
    def licenseFiles = findFiles(glob: '**/LICENSE*') + 
                       findFiles(glob: '**/NOTICE*') +
                       findFiles(glob: '**/THIRD-PARTY*')
    if (licenseFiles.size() > 0) {
        echo "   - Found ${licenseFiles.size()} license files to upload."
        entries.add([
            "pattern": "**/{LICENSE,NOTICE,THIRD-PARTY}*",
            "target": "${ARTIFACTORY_REPO_REPORTS}/${JOB_NAME}/licenses/${BUILD_NUMBER}/",
            "recursive": "true",
            "flat": "false"
        ])
    }
    
    return entries
}

/**
 * Finds the final deployment package (app.zip) for upload.
 */
def List getDeploymentPackageSpecEntries() {
    def entries = []

    if (fileExists('app.zip')) {
        echo "   - Found deployment package 'app.zip' to upload."
        entries.add([
            "pattern": "app.zip",
            "target": "${ARTIFACTORY_REPO_BINARIES}/${JOB_NAME}/${BUILD_NUMBER}/app.zip",
            "recursive": "false",
            "flat": "true" // Keep the filename as is in the target directory
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
    def summary = """
    🔒 Security Scan Summary
    ========================
    - Build: ${BUILD_NUMBER}
    - Scan Level: ${params.SECURITY_SCAN_LEVEL}
    - Status: ${currentBuild.currentResult}
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
    if (currentBuild.currentResult == 'UNSTABLE') {
        echo "⚠️ Build is unstable due to security issues."
    }
}

//---------------------------------
// Installation Helpers for Tools
//---------------------------------

def installSemgrep() {
    sh """
        echo "📦 Installing Semgrep..."
        pip3 install --user semgrep --quiet || pip install --user semgrep --quiet || true
        export PATH="\$PATH:\$HOME/.local/bin"
        semgrep --version || echo "⚠️ Semgrep installation verification failed"
    """
}

def installTrivy() {
    sh """
        echo "📦 Installing Trivy..."
        TRIVY_VERSION=0.50.0
        TRIVY_DIR=\$(pwd)/trivy-bin
        mkdir -p \$TRIVY_DIR
        if [ ! -f \$TRIVY_DIR/trivy ]; then
            wget -q https://github.com/aquasecurity/trivy/releases/download/v\${TRIVY_VERSION}/trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
            tar -xzf trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz -C \$TRIVY_DIR trivy
            chmod +x \$TRIVY_DIR/trivy
            rm trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
        fi
        \$TRIVY_DIR/trivy --version
        echo "📥 Downloading Trivy vulnerability database..."
        \$TRIVY_DIR/trivy image --download-db-only
    """
}