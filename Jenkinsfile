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
    agent any

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
        DEPENDENCY_CHECK_VERSION = '9.2.0'
        SEMGREP_TIMEOUT = '300'
        GITLEAKS_VERSION = '8.18.2'
        LICENSE_CHECKER_VERSION = '3.0.0'

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
 * Initializes build-wide variables.
 */
def initializeBuild() {
    echo "🔧 Initializing build..."
    // Set .NET verbosity based on the LOG_LEVEL parameter
    def verbosityMapping = [INFO: 'n', DEBUG: 'd', WARN: 'm', ERROR: 'q']
    env.DOTNET_VERBOSITY = verbosityMapping.get(params.LOG_LEVEL, 'n')
    echo "🔧 dotnet verbosity set to: ${env.DOTNET_VERBOSITY}"
    env.NUNIT_PROJECTS = ''
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
                installDotnetTool('dotnet-sonarscanner')
                
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
 * Runs Semgrep SAST scan.
 */
def runSemgrepScan() {
    echo "🔒 Running Semgrep SAST analysis..."
    try {
        sh "mkdir -p ${SECURITY_REPORTS_DIR}/semgrep"
        installSemgrep()
        def semgrepRules = (params.SECURITY_SCAN_LEVEL in ['COMPREHENSIVE', 'FULL']) ?
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
        def solutionFile = findFiles(glob: '**/*.sln')
        if (solutionFile.size() == 0) {
            error "❌ Could not find a solution file (.sln) to lint."
        }

        installDotnetTool('dotnet-format', env.DOTNET_FORMAT_VERSION)

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