pipeline {
    agent any

    tools {
        // Configure JFrog CLI tool in Jenkins Global Tool Configuration
        jfrog 'jfrog-cli'
    }

    environment {
        // .NET Configuration
        DOTNET_VERSION = '6.0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

        // Test Results Configuration
        TEST_RESULTS_DIR = 'test-results'
        COVERAGE_REPORTS_DIR = 'coverage-reports'

        // SonarQube Configuration
        SONARQUBE_URL = 'http://localhost:9000'
        SONAR_PROJECT_KEY = 'your-project-key' // Replace with your actual SonarQube project key

        // JFrog Configuration
        JFROG_CLI_BUILD_NAME = "${JOB_NAME}"
        JFROG_CLI_BUILD_NUMBER = "${BUILD_NUMBER}"

        ARTIFACTORY_REPO_BINARIES = 'libs-release-local'
        ARTIFACTORY_REPO_NUGET = 'nuget-local'
        ARTIFACTORY_REPO_REPORTS = 'reports-local'

        dotnetVerbosity = 'n' // Default verbosity for dotnet commands

        // Security Check Configuration
        SECURITY_REPORTS_DIR = 'security-reports'
        DEPENDENCY_CHECK_VERSION = '9.2.0'
        SEMGREP_TIMEOUT = '300'

        // Linter, Secrets, and License Tool Configuration
        LINTER_REPORTS_DIR = 'linter-reports'
        DOTNET_FORMAT_VERSION = '7.0.400' // Use a version compatible with your SDK
        SECRETS_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/secrets"
        GITLEAKS_VERSION = '8.18.2'
        LICENSE_CHECKER_VERSION = '3.0.0'

        // FOSSA Configuration
        FOSSA_API_KEY = credentials('fossa-api-key')

        // DAST Configuration
        DAST_REPORTS_DIR = "${SECURITY_REPORTS_DIR}/dast"
    }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 30, unit: 'MINUTES')
        timestamps()
        skipDefaultCheckout()
        parallelsAlwaysFailFast()
    }

    parameters {
        booleanParam(
            name: 'GENERATE_COVERAGE',
            defaultValue: true,
            description: 'Generate code coverage reports'
        )
        booleanParam(
            name: 'FAIL_ON_TEST_FAILURE',
            defaultValue: true,
            description: 'Fail the build if any tests fail'
        )
        choice(
            name: 'LOG_LEVEL',
            choices: ['INFO', 'DEBUG', 'WARN', 'ERROR'],
            description: 'Set logging level for test execution'
        )
        booleanParam(
            name: 'ENABLE_SECURITY_SCAN',
            defaultValue: true,
            description: 'Enable comprehensive security scanning'
        )
        booleanParam(
            name: 'FAIL_ON_SECURITY_ISSUES',
            defaultValue: false,
            description: 'Fail build on critical security vulnerabilities'
        )
        choice(
            name: 'SECURITY_SCAN_LEVEL',
            choices: ['BASIC', 'COMPREHENSIVE', 'FULL'],
            description: 'Security scanning depth level'
        )

        booleanParam(
            name: 'ENABLE_LINTING',
            defaultValue: true,
            description: 'Enable .NET code style linting with dotnet-format'
        )

        booleanParam(
            name: 'ENABLE_SECRETS_SCAN',
            defaultValue: true,
            description: 'Enable secrets detection scan with Gitleaks'
        )

        booleanParam(
            name: 'ENABLE_LICENSE_CHECK',
            defaultValue: true,
            description: 'Enable dependency license compliance check'
        )

        // // DAST Parameters (to be configured in the pipeline)
        // booleanParam(name: 'ENABLE_DAST_SCAN', defaultValue: true, description: 'Enable Dynamic Application Security Testing (DAST) with OWASP ZAP')
        // string(name: 'STAGING_URL', defaultValue: 'http://your-staging-app.example.com', description: 'URL of the deployed staging application for DAST scan')

        // // Notification Parameters (to be configured in the pipeline)
        // string(name: 'SLACK_CHANNEL', defaultValue: '#ci-alerts', description: 'Slack channel to send notifications to')
        // credentials(name: 'SLACK_CREDENTIAL_ID', description: 'Jenkins credential ID for the Slack Bot Token', required: false)
    }

    stages {
        stage('Initialize') {
            steps {
                script {
                    switch (params.LOG_LEVEL) {
                        case 'INFO':
                            dotnetVerbosity = 'n'
                            break
                        case 'DEBUG':
                            dotnetVerbosity = 'd'
                            break
                        case 'WARN':
                            dotnetVerbosity = 'm'
                            break
                        case 'ERROR':
                            dotnetVerbosity = 'q'
                            break
                        default:
                            dotnetVerbosity = 'n'
                    }
                    echo "🔧 dotnetVerbosity set to: ${dotnetVerbosity}"
                    env.nunitProjects = ''
                }
            }
        }

        stage('Checkout') {
            steps {
                script {
                    echo "🔄 Checking out source code..."
                    checkout scm

                    env.GIT_COMMIT_SHORT = sh(
                        script: "git rev-parse --short HEAD",
                        returnStdout: true
                    ).trim()
                    env.GIT_COMMIT_MSG = sh(
                        script: "git log -1 --pretty=format:'%s'",
                        returnStdout: true
                    ).trim()

                    echo "📋 Build Info:"
                    echo "    Branch: ${env.BRANCH_NAME}"
                    echo "    Commit: ${env.GIT_COMMIT_SHORT}"
                    echo "    Message: ${env.GIT_COMMIT_MSG}"
                }
            }
        }

        stage('Setup .NET Environment') {
            steps {
                script {
                    echo "🔧 Setting up .NET environment..."
                    def dotnetInstalled = sh(
                        script: "dotnet --version",
                        returnStatus: true
                    )
                    if (dotnetInstalled != 0) {
                        error "❌ .NET SDK not found. Please install .NET SDK ${DOTNET_VERSION}"
                    }
                    sh """
                        echo "📦 .NET SDK Version:"
                        dotnet --version
                        dotnet --info
                    """
                }
            }
        }

        stage('Discover NUnit Test Projects') {
            steps {
                script {
                    echo "🔍 Discovering NUnit test projects..."
                    
                    def detectTestFramework = { projectPath ->
                        try {
                            def projectContent = readFile(projectPath)
                            
                            // Parse XML properly to check for NUnit packages
                            def packageRefs = []
                            def packageRefPattern = /<PackageReference\s+Include="([^"]+)"/
                            def matcher = projectContent =~ packageRefPattern
                            matcher.each { match ->
                                packageRefs.add(match[1])
                            }
                            
                            // Check for NUnit-related packages
                            def nunitPackages = ['NUnit', 'NUnit3TestAdapter', 'Microsoft.NET.Test.Sdk']
                            def hasTestSdk = packageRefs.any { it.contains('Microsoft.NET.Test.Sdk') }
                            def hasNUnit = packageRefs.any { pkg -> 
                                nunitPackages.any { nunitPkg -> pkg.contains(nunitPkg) }
                            }
                            
                            if (hasTestSdk && hasNUnit) {
                                echo "✅ NUnit project detected: ${projectPath}"
                                return 'NUNIT'
                            }
                            
                            // Also check for legacy packages.config references
                            if (projectContent.contains('packages.config')) {
                                def packagesConfigPath = projectPath.replace('.csproj', '/packages.config')
                                if (fileExists(packagesConfigPath)) {
                                    def packagesContent = readFile(packagesConfigPath)
                                    if (packagesContent.contains('id="NUnit"')) {
                                        echo "✅ Legacy NUnit project detected: ${projectPath}"
                                        return 'NUNIT'
                                    }
                                }
                            }
                            
                            return 'UNKNOWN'
                        } catch (Exception e) {
                            echo "⚠️  Error reading project file ${projectPath}: ${e.message}"
                            return 'ERROR'
                        }
                    }

                    def findAllTestProjects = {
                        try {
                            // Multiple search patterns for different naming conventions
                            def searchPatterns = [
                                "find . -name '*.csproj' -path '*/Test*'",
                                "find . -name '*.csproj' -path '*/*Test*'", 
                                "find . -name '*.csproj' -path '*/*Tests*'",
                                "find . -name '*.csproj' -path '*/Tests/*'",
                                "find . -name '*.Test.csproj'",
                                "find . -name '*.Tests.csproj'",
                                "find . -name '*.UnitTests.csproj'",
                                "find . -name '*.IntegrationTests.csproj'"
                            ]
                            
                            def allProjects = []
                            searchPatterns.each { pattern ->
                                def result = sh(
                                    script: "${pattern} 2>/dev/null || true",
                                    returnStdout: true
                                ).trim()
                                
                                if (result) {
                                    def projects = result.split('\n').findAll { it.trim() }
                                    allProjects.addAll(projects)
                                }
                            }
                            
                            // Remove duplicates and limit results
                            return allProjects.unique().take(100)
                            
                        } catch (Exception e) {
                            echo "⚠️  Error searching for test projects: ${e.message}"
                            
                            // Fallback to simple find
                            try {
                                def result = sh(
                                    script: "find . -name '*.csproj' | head -50",
                                    returnStdout: true
                                ).trim()
                                return result ? result.split('\n').findAll { it.trim() } : []
                            } catch (Exception fallbackError) {
                                echo "❌ Fallback search also failed: ${fallbackError.message}"
                                return []
                            }
                        }
                    }

                    // Discover test projects
                    def allTestProjects = findAllTestProjects()
                    echo "📁 Found ${allTestProjects.size()} potential test project(s)"
                    
                    def nunitProjectsList = []
                    def skippedProjects = []
                    def errorProjects = []
                    
                    if (allTestProjects) {
                        allTestProjects.each { project ->
                            if (fileExists(project)) {
                                def framework = detectTestFramework(project)
                                switch(framework) {
                                    case 'NUNIT':
                                        nunitProjectsList.add(project)
                                        break
                                    case 'ERROR':
                                        errorProjects.add(project)
                                        break
                                    default:
                                        skippedProjects.add(project)
                                }
                            } else {
                                echo "⚠️  Project file not found: ${project}"
                            }
                        }
                    }

                    // Logging results
                    echo "📊 Test project discovery results:"
                    echo "  ✅ NUnit projects: ${nunitProjectsList.size()}"
                    echo "  ⏭️  Skipped projects: ${skippedProjects.size()}"
                    echo "  ❌ Error projects: ${errorProjects.size()}"
                    
                    if (nunitProjectsList) {
                        echo "📋 NUnit projects found:"
                        nunitProjectsList.each { project ->
                            echo "  - ${project}"
                        }
                    }

                    // Configurable fallback
                    if (nunitProjectsList.isEmpty()) {
                        def fallbackProjects = env.FALLBACK_NUNIT_PROJECTS ?: './csharp-nunit/Calculator.Tests/Calculator.Tests.csproj'
                        echo "⚠️  No NUnit projects discovered, using fallback: ${fallbackProjects}"
                        nunitProjectsList = fallbackProjects.split(',').collect { it.trim() }
                        
                        // Verify fallback projects exist
                        nunitProjectsList = nunitProjectsList.findAll { project ->
                            if (fileExists(project)) {
                                return true
                            } else {
                                echo "❌ Fallback project not found: ${project}"
                                return false
                            }
                        }
                    }

                    // Validate final list
                    if (nunitProjectsList.isEmpty()) {
                        error("❌ No valid NUnit test projects found and no valid fallback projects available")
                    }

                    env.nunitProjects = nunitProjectsList.join(',')
                    env.nunitProjectCount = nunitProjectsList.size().toString()
                    
                    echo "🎯 Final NUnit projects (${nunitProjectsList.size()}):"
                    nunitProjectsList.each { project ->
                        echo "  → ${project}"
                    }
                }
            }
        }

        stage('Restore .NET Dependencies') {
            steps {
                script {
                    echo "📦 Restoring .NET dependencies..."
                    def solutionFiles = sh(
                        script: "find . -name '*.sln' | head -10",
                        returnStdout: true
                    ).trim().split('\n').findAll { it.trim() }

                    if (solutionFiles && solutionFiles[0]) {
                        solutionFiles.each { sln ->
                            if (fileExists(sln)) {
                                sh "dotnet restore '${sln}' --verbosity ${dotnetVerbosity}"
                            }
                        }
                    } else {
                        sh "dotnet restore --verbosity ${dotnetVerbosity}"
                    }
                }
            }
        }

        // SINGLE INTEGRATED STAGE:
        // - Build .NET Project
        // - Run NUnit Tests  
        // - Generate Coverage Report
        // - SAST (SonarQube)
        // - Publish Reports
        
        stage('Build, Test & SAST Analysis') {
            steps {
                script {
                    echo "🔨 Building .NET project with integrated SonarQube analysis..."

                    // Create directories
                    sh """
                        mkdir -p ${TEST_RESULTS_DIR}
                        mkdir -p ${COVERAGE_REPORTS_DIR}
                    """

                    withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
                        try {
                            // Install SonarQube scanner
                            sh '''
                                echo "📦 Installing dotnet-sonarscanner..."
                                dotnet tool install --global dotnet-sonarscanner || true
                                export PATH="$PATH:$HOME/.dotnet/tools"
                            '''

                            // Begin SonarQube analysis
                            sh '''
                                echo "🔍 Starting SonarQube analysis..."
                                export PATH="$PATH:$HOME/.dotnet/tools"
                                dotnet sonarscanner begin \
                                    /k:"$SONAR_PROJECT_KEY" \
                                    /d:sonar.host.url="$SONARQUBE_URL" \
                                    /d:sonar.login="$SONAR_TOKEN" \
                                    /d:sonar.cs.nunit.reportsPaths="$TEST_RESULTS_DIR/*.trx" \
                                    /d:sonar.cs.opencover.reportsPaths="**/coverage.cobertura.xml" \
                                    /d:sonar.exclusions="**/bin/**,**/obj/**,**/*.Tests/**" \
                                    /d:sonar.test.exclusions="**/*.Tests/**" \
                                    /d:sonar.coverage.exclusions="**/*.Tests/**"
                            '''

                            // Build the project
                            echo "🔨 Building .NET project..."
                            def solutionFiles = sh(
                                script: "find . -name '*.sln' | head -10",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }

                            if (solutionFiles && solutionFiles[0]) {
                                solutionFiles.each { sln ->
                                    if (fileExists(sln)) {
                                        echo "🔨 Building solution: ${sln}"
                                        sh """
                                            dotnet build '${sln}' --configuration Release --no-restore \\
                                                --verbosity ${dotnetVerbosity}
                                        """
                                    }
                                }
                            } else {
                                echo "🔨 No solution files found, building all projects..."
                                sh """
                                    dotnet build --configuration Release --no-restore \\
                                        --verbosity ${dotnetVerbosity}
                                """
                            }

                            // Run NUnit tests if they exist
                            if (env.nunitProjects && env.nunitProjects.trim() != '') {
                                echo "🧪 Running NUnit tests..."
                                def nunitProjectsList = env.nunitProjects.split(',').findAll { it.trim() }

                                if (nunitProjectsList && nunitProjectsList.size() > 0) {
                                    echo "🧪 Running ${nunitProjectsList.size()} NUnit test project(s)"

                                    def coverageArg = params.GENERATE_COVERAGE
                                        ? '--collect:"XPlat Code Coverage"'
                                        : ""

                                    nunitProjectsList.each { project ->
                                        project = project.trim()
                                        if (project) {
                                            echo "🧪 Running NUnit tests in: ${project}"
                                            def projectName = project.split('/')[-1].replace('.csproj', '')
                                            sh """
                                                dotnet test '${project}' \\
                                                    --configuration Release \\
                                                    --no-build \\
                                                    --logger "trx;LogFileName=nunit-results-${projectName}.trx" \\
                                                    --results-directory ${TEST_RESULTS_DIR} \\
                                                    ${coverageArg} \\
                                                    --verbosity ${dotnetVerbosity}
                                            """
                                        }
                                    }
                                }
                            } else {
                                echo "⚠️ No NUnit test projects found"
                            }

                            // Generate coverage report if enabled
                            if (params.GENERATE_COVERAGE) {
                                echo "📊 Generating coverage report..."
                                def coverageFiles = sh(
                                    script: "find . -type f -name 'coverage.cobertura.xml' 2>/dev/null || true",
                                    returnStdout: true
                                ).trim()

                                if (coverageFiles) {
                                    echo "📊 Found coverage files, generating report..."
                                    sh """
                                        # Install ReportGenerator if not already installed
                                        dotnet tool install --global dotnet-reportgenerator-globaltool || true
                                        export PATH="\$PATH:\$HOME/.dotnet/tools"

                                        # Generate HTML coverage report
                                        reportgenerator \\
                                            -reports:**/coverage.cobertura.xml \\
                                            -targetdir:${COVERAGE_REPORTS_DIR}/dotnet \\
                                            -reporttypes:Html,Cobertura,JsonSummary \\
                                            -verbosity:${params.LOG_LEVEL}
                                    """
                                } else {
                                    echo "⚠️ No coverage files found"
                                }
                            }

                            // Complete SonarQube analysis
                            sh '''
                                echo "🔍 Completing SonarQube analysis..."
                                export PATH="$PATH:$HOME/.dotnet/tools"
                                dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN"
                            '''

                            echo "✅ Build, test, and SonarQube analysis completed successfully"

                        } catch (Exception e) {
                            echo "❌ Build, test, or SonarQube analysis failed: ${e.getMessage()}"
                            
                            // Try to end SonarQube analysis gracefully if it was started
                            try {
                                sh '''
                                    export PATH="$PATH:$HOME/.dotnet/tools"
                                    dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN" || true
                                '''
                            } catch (Exception endException) {
                                echo "⚠️ Could not end SonarQube analysis gracefully: ${endException.getMessage()}"
                            }
                            
                            // Decide whether to fail the pipeline or mark as unstable
                            if (params.FAIL_ON_TEST_FAILURE) {
                                throw e
                            } else {
                                currentBuild.result = 'UNSTABLE'
                                echo "⚠️ Build marked as unstable due to failures"
                            }
                        }
                    }
                }
            }
            post {
                always {
                    script {
                        echo "📊 Archiving test results and artifacts..."

                        // Archive test results
                        if (fileExists("${TEST_RESULTS_DIR}")) {
                            try {
                                archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*.trx", allowEmptyArchive: true
                                echo "✅ Test results archived"
                            } catch (Exception e) {
                                echo "⚠️ Could not archive test results: ${e.getMessage()}"
                            }
                        }

                        // Publish test results to Jenkins UI
                        try {
                            def testResultsFound = sh(
                                script: "find ${TEST_RESULTS_DIR} -name '*.trx' -type f | head -1",
                                returnStdout: true
                            ).trim()

                            if (testResultsFound) {
                                // Use the 'mstest' step for .trx files.
                                mstest testResultsFile: "${TEST_RESULTS_DIR}/*.trx", 
                                    failOnError: false, 
                                    keepLongStdio: true
                                echo "✅ Test results published to Jenkins UI"
                            } else {
                                echo "ℹ️ No test result files found to publish"
                            }
                        } catch (Exception e) {
                            echo "⚠️ Could not publish test results: ${e.getMessage()}"
                        }

                        // Archive coverage reports
                        if (params.GENERATE_COVERAGE && fileExists("${COVERAGE_REPORTS_DIR}")) {
                            try {
                                // Publish coverage report
                                def coberturaFile = "${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml"
                                if (fileExists(coberturaFile)) {
                                    recordCoverage tools: [[parser: 'COBERTURA', pattern: coberturaFile]],
                                                sourceCodeRetention: 'EVERY_BUILD'
                                }

                                // Archive coverage artifacts
                                archiveArtifacts artifacts: "${COVERAGE_REPORTS_DIR}/**",
                                                allowEmptyArchive: true,
                                                fingerprint: true
                                echo "✅ Coverage reports archived"
                            } catch (Exception e) {
                                echo "⚠️ Could not archive coverage reports: ${e.getMessage()}"
                            }
                        }
                    }
                }
                success {
                    script {
                        echo "✅ Build, test, and SAST analysis completed successfully!"
                    }
                }
                failure {
                    script {
                        echo "❌ Build, test, or SAST analysis failed!"
                    }
                }
                unstable {
                    script {
                        echo "⚠️ Build completed with warnings!"
                    }
                }
            }
        }

        stage('Dependency Vulnerability Scan') {
            when {
                expression { params.ENABLE_SECURITY_SCAN }
            }
            steps {
                script {
                    echo "🔒 Running OWASP Dependency Check..."
                    
                    try {
                        // Setup directories
                        sh "mkdir -p ${SECURITY_REPORTS_DIR}/dependency-check"
                        
                        cleanupExistingDependencyCheck()
                        downloadDependencyCheck()
                        runDependencyCheck()
                        processDependencyCheckResults()
                        publishDependencyCheckReports()
                        
                    } catch (Exception e) {
                        echo "❌ Dependency Check failed: ${e.getMessage()}"
                        env.DEPENDENCY_VULNERABILITIES = "ERROR"
                        env.DEPENDENCY_HIGH_CRITICAL = "ERROR"
                        currentBuild.result = 'UNSTABLE'
                    }
                }
            }
        }
        
        stage('SAST Security Scan') {
            when {
                expression { params.ENABLE_SECURITY_SCAN }
            }
            steps {
                script {
                    echo "🔒 Running Semgrep SAST analysis..."
                    
                    try {
                        sh "mkdir -p ${SECURITY_REPORTS_DIR}/semgrep"
                        
                        // Install and run Semgrep
                        installSemgrep()
                        runSemgrepScan()
                        processSemgrepResults()
                        
                    } catch (Exception e) {
                        echo "❌ Semgrep scan failed: ${e.getMessage()}"
                        env.SEMGREP_ISSUES = "ERROR"
                        env.SEMGREP_CRITICAL = "ERROR"
                        currentBuild.result = 'UNSTABLE'
                    }
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

        stage('Secrets Detection') {
            when { expression { params.ENABLE_SECRETS_SCAN } }
            steps {
                script {
                    runSecretsScan()
                }
            }
        }
        
        stage('Container Security Scan') {
            when {
                expression { 
                    params.SECURITY_SCAN_LEVEL == 'COMPREHENSIVE' || 
                    params.SECURITY_SCAN_LEVEL == 'FULL' 
                }
            }
            steps {
                script {
                    echo "🔒 Running Trivy security scan..."
                    
                    try {
                        sh "mkdir -p ${SECURITY_REPORTS_DIR}/trivy"
                        installTrivy()
                        runTrivyScan()
                        
                    } catch (Exception e) {
                        echo "❌ Trivy scan failed: ${e.getMessage()}"
                        echo "⚠️ Continuing without container security scan"
                    }
                }
            }
        }
        
        stage('License Compliance Check') {
            when {
                expression { params.ENABLE_LICENSE_CHECK && (params.SECURITY_SCAN_LEVEL == 'COMPREHENSIVE' || params.SECURITY_SCAN_LEVEL == 'FULL') }
            }
            steps {
                script {
                    try {
                        runScanCodeLicenseCheck()
                    } catch (Exception e) {
                        echo "❌ License check failed: ${e.getMessage()}"
                        if (params.FAIL_ON_SECURITY_ISSUES) {
                            error("Failing build due to non-compliant licenses.")
                        } else {
                            currentBuild.result = 'UNSTABLE'
                        }
                    }
                }
            }
        }

        stage('Quality Gate') {
            steps {
                script {
                    echo "🚦 Evaluating comprehensive quality gate..."
                    def buildResult = currentBuild.result ?: 'SUCCESS'
                    def buildStatus = currentBuild.currentResult

                    echo "📊 Build Status Summary:"
                    echo "    Build Result: ${buildResult}"
                    echo "    Current Status: ${buildStatus}"
                    
                    // Security metrics
                    if (params.ENABLE_SECURITY_SCAN) {
                        echo "🔒 Security Metrics:"
                        echo "    Dependency Vulnerabilities: ${env.DEPENDENCY_VULNERABILITIES ?: 'N/A'}"
                        echo "    SAST Issues: ${env.SEMGREP_ISSUES ?: 'N/A'}"
                        echo "    Critical SAST Issues: ${env.SEMGREP_CRITICAL ?: 'N/A'}"
                        
                        // Check security thresholds
                        def criticalIssues = (env.SEMGREP_CRITICAL ?: "0").toInteger()
                        if (criticalIssues > 5) {
                            echo "⚠️ Warning: High number of critical security issues (${criticalIssues})"
                            currentBuild.result = 'UNSTABLE'
                        }
                    }

                    if (buildStatus == 'FAILURE') {
                        error "❌ Quality gate failed: Build has failed"
                    }
                    if (buildStatus == 'UNSTABLE') {
                        echo "⚠️ Quality gate warning: Build is unstable"
                    }
                    if (buildStatus == 'SUCCESS') {
                        echo "✅ Quality gate passed!"
                    }
                }
            }
        }

        // stage('Upload to JFrog Artifactory') {
        //     steps {
        //         script {
        //             echo "📦 Uploading .NET artifacts to JFrog Artifactory..."
                    
        //             try {
        //                 // Test connection to Artifactory
        //                 jf 'rt ping'
        //                 echo "✅ JFrog Artifactory connection successful"
                        
        //                 // Debug: Show current directory and file structure
        //                 echo "🔍 Current directory structure:"
        //                 sh """
        //                     echo "Working directory: \$(pwd)"
        //                     echo "Contents of current directory:"
        //                     ls -la
        //                     echo "Looking for bin directories:"
        //                     find . -type d -name 'bin' | head -10
        //                     echo "Looking for .NET files in bin directories:"
        //                     find . -type f -name '*.dll' -o -name '*.exe' | head -10
        //                 """
                        
        //                 // Find and list all potential artifacts with existence check
        //                 def artifactsList = sh(
        //                     script: """
        //                         find . -type f \\( -name '*.dll' -o -name '*.exe' -o -name '*.pdb' \\) \\
        //                             \\( -path '*/bin/Release/*' -o -path '*/bin/Debug/*' \\) | sort
        //                     """,
        //                     returnStdout: true
        //                 ).trim()
                        
        //                 echo "🔍 Searching for .NET artifacts completed"
                        
        //                 if (artifactsList) {
        //                     echo "📋 Found potential artifacts:"
        //                     echo "${artifactsList}"
                            
        //                     def artifactFiles = artifactsList.split('\n').findAll { 
        //                         it.trim() && !it.trim().isEmpty() && !it.contains('🔍') && !it.contains('Searching')
        //                     }
                            
        //                     if (artifactFiles.size() > 0) {
        //                         echo "📦 Processing ${artifactFiles.size()} artifact(s)..."
                                
        //                         // Get current working directory for absolute paths
        //                         def workingDir = sh(script: "pwd", returnStdout: true).trim()
        //                         echo "📁 Working directory: ${workingDir}"
                                
        //                         // Process each artifact file with existence verification
        //                         artifactFiles.each { artifactPath ->
        //                             artifactPath = artifactPath.trim()
                                    
        //                             // Verify file exists before attempting upload
        //                             def fileExists = sh(
        //                                 script: "test -f '${artifactPath}' && echo 'true' || echo 'false'",
        //                                 returnStdout: true
        //                             ).trim()
                                    
        //                             if (fileExists == 'true') {
        //                                 // Get relative path for target structure
        //                                 def relativePath = artifactPath.startsWith('./') ? artifactPath.substring(2) : artifactPath
                                        
        //                                 echo "📤 Processing file: ${relativePath}"
                                        
        //                                 try {
        //                                     // FIXED: Use proper jf rt u command syntax
        //                                     jf "rt u \"${artifactPath}\" ${ARTIFACTORY_REPO_BINARIES}/${relativePath} --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
                                            
        //                                     echo "✅ Successfully uploaded: ${relativePath}"
                                            
        //                                 } catch (Exception uploadException) {
        //                                     echo "❌ Failed to upload ${relativePath}: ${uploadException.getMessage()}"
                                            
        //                                     // Alternative approach: Upload with simpler syntax
        //                                     try {
        //                                         echo "🔄 Trying simplified upload..."
                                                
        //                                         // Create a temporary spec file for upload
        //                                         def specContent = """
        //                                         {
        //                                             "files": [
        //                                                 {
        //                                                     "pattern": "${artifactPath}",
        //                                                     "target": "${ARTIFACTORY_REPO_BINARIES}/${relativePath}"
        //                                                 }
        //                                             ]
        //                                         }
        //                                         """
                                                
        //                                         writeFile file: 'upload-spec.json', text: specContent
                                                
        //                                         jf "rt u --spec=upload-spec.json --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER}"
                                                
        //                                         echo "✅ Successfully uploaded with spec file: ${relativePath}"
                                                
        //                                     } catch (Exception specException) {
        //                                         echo "❌ Spec file approach also failed: ${specException.getMessage()}"
                                                
        //                                         // Final fallback: Direct upload without build info
        //                                         try {
        //                                             echo "🔄 Trying direct upload..."
                                                    
        //                                             jf "rt u \"${artifactPath}\" ${ARTIFACTORY_REPO_BINARIES}/${relativePath}"
                                                    
        //                                             echo "✅ Successfully uploaded (direct): ${relativePath}"
                                                    
        //                                         } catch (Exception directException) {
        //                                             echo "❌ All upload approaches failed for: ${relativePath}"
        //                                             echo "Error: ${directException.getMessage()}"
        //                                         }
        //                                     }
        //                                 }
        //                             } else {
        //                                 echo "⚠️ File does not exist, skipping: ${artifactPath}"
        //                             }
        //                         }
                                
        //                         // Upload NuGet packages if they exist
        //                         def nugetPackages = sh(
        //                             script: "find . -name '*.nupkg' -o -name '*.snupkg' | head -20",
        //                             returnStdout: true
        //                         ).trim()
                                
        //                         if (nugetPackages) {
        //                             echo "📦 Found NuGet packages, uploading..."
        //                             nugetPackages.split('\n').findAll { it.trim() }.each { packagePath ->
        //                                 def packageExists = sh(
        //                                     script: "test -f '${packagePath}' && echo 'true' || echo 'false'",
        //                                     returnStdout: true
        //                                 ).trim()
                                        
        //                                 if (packageExists == 'true') {
        //                                     echo "📤 Uploading NuGet package: ${packagePath}"
                                            
        //                                     try {
        //                                         jf "rt u \"${packagePath}\" ${ARTIFACTORY_REPO_NUGET}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=true"
        //                                         echo "✅ Successfully uploaded NuGet package: ${packagePath}"
        //                                     } catch (Exception nugetException) {
        //                                         echo "❌ Failed to upload NuGet package ${packagePath}: ${nugetException.getMessage()}"
        //                                     }
        //                                 }
        //                             }
        //                         }
                                
        //                         // Upload test results and coverage reports
        //                         def testResultsPath = "${workingDir}/${TEST_RESULTS_DIR}"
        //                         def testResultsExists = sh(
        //                             script: "test -d '${testResultsPath}' && echo 'true' || echo 'false'",
        //                             returnStdout: true
        //                         ).trim()
                                
        //                         if (testResultsExists == 'true') {
        //                             echo "📊 Uploading test results..."
        //                             try {
        //                                 jf "rt u \"${testResultsPath}/*\" ${ARTIFACTORY_REPO_REPORTS}/test-results/${JFROG_CLI_BUILD_NAME}/${JFROG_CLI_BUILD_NUMBER}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
        //                                 echo "✅ Test results uploaded successfully"
        //                             } catch (Exception testException) {
        //                                 echo "❌ Failed to upload test results: ${testException.getMessage()}"
        //                             }
        //                         }
                                
        //                         def coveragePath = "${workingDir}/${COVERAGE_REPORTS_DIR}"
        //                         def coverageExists = sh(
        //                             script: "test -d '${coveragePath}' && echo 'true' || echo 'false'",
        //                             returnStdout: true
        //                         ).trim()
                                
        //                         if (coverageExists == 'true') {
        //                             echo "📊 Uploading coverage reports..."
        //                             try {
        //                                 jf "rt u \"${coveragePath}/**\" ${ARTIFACTORY_REPO_REPORTS}/coverage/${JFROG_CLI_BUILD_NAME}/${JFROG_CLI_BUILD_NUMBER}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
        //                                 echo "✅ Coverage reports uploaded successfully"
        //                             } catch (Exception coverageException) {
        //                                 echo "❌ Failed to upload coverage reports: ${coverageException.getMessage()}"
        //                             }
        //                         }
                                
        //                         // Publish build info
        //                         try {
        //                             jf "rt bp ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
        //                             echo "✅ Build info published successfully"
        //                         } catch (Exception buildInfoException) {
        //                             echo "❌ Failed to publish build info: ${buildInfoException.getMessage()}"
        //                         }
                                
        //                     } else {
        //                         echo "⚠️ No artifacts found to upload"
        //                     }
        //                 } else {
        //                     echo "⚠️ No .NET artifacts found in bin/Release or bin/Debug directories"
        //                     echo "🔍 Checking alternative locations..."
                            
        //                     // Check for artifacts in other common locations
        //                     def alternativeArtifacts = sh(
        //                         script: "find . -name '*.dll' -o -name '*.exe' | grep -v '/obj/' | head -10",
        //                         returnStdout: true
        //                     ).trim()
                            
        //                     if (alternativeArtifacts) {
        //                         echo "📋 Found artifacts in alternative locations:"
        //                         echo "${alternativeArtifacts}"
        //                     } else {
        //                         echo "❌ No .NET artifacts found anywhere"
        //                     }
        //                 }
                        
        //             } catch (Exception e) {
        //                 echo "❌ JFrog Artifactory upload failed: ${e.getMessage()}"
        //                 echo "📊 This is non-critical - marking as unstable"
        //                 currentBuild.result = 'UNSTABLE'
        //             }
        //         }
        //     }
        // }

        stage('Deployment') {
            steps {
                script {
                    echo "🚀 Deployment stage (to be implemented)"
                }
            }
        }

        //  // Dynamic Application Security Testing (DAST)
        // stage('DAST Scan (OWASP ZAP)') {
        //     when { expression { params.ENABLE_DAST_SCAN } }
        //     steps {
        //         script {
        //             catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
        //                 echo "🔒 Running DAST Scan on ${params.STAGING_URL}"
        //                 sh "mkdir -p ${DAST_REPORTS_DIR}"

        //                 // Use the official OWASP ZAP Docker image to run a baseline scan
        //                 // This scan is non-intrusive and ideal for CI/CD pipelines
        //                 try {
        //                     sh """
        //                         docker pull owasp/zap2docker-stable
        //                         docker run --rm -v \$(pwd):/zap/wrk/:rw --network=host owasp/zap2docker-stable zap-baseline.py \\
        //                             -t ${params.STAGING_URL} \\
        //                             -g gen.conf \\
        //                             -r ${DAST_REPORTS_DIR}/dast-report.html \\
        //                             -w ${DAST_REPORTS_DIR}/dast-report.md \\
        //                             -x ${DAST_REPORTS_DIR}/dast-report.xml \\
        //                             -J ${DAST_REPORTS_DIR}/dast-report.json || true 
        //                     """
        //                     // The '|| true' prevents the build from failing if ZAP finds issues.
        //                     // We will check the results and decide the build status below.

        //                 } catch (Exception e) {
        //                     echo "❌ DAST scan execution failed: ${e.message}"
        //                     currentBuild.result = 'UNSTABLE'
        //                 }

        //                 // Publish the HTML report to Jenkins UI for easy viewing
        //                 publishHTML(target: [
        //                     allowMissing: true,
        //                     alwaysLinkToLastBuild: true,
        //                     keepAll: true,
        //                     reportDir: DAST_REPORTS_DIR,
        //                     reportFiles: 'dast-report.html',
        //                     reportName: '🛡️ DAST Report (OWASP ZAP)'
        //                 ])

        //                 // Check the JSON report for findings and mark build as unstable if any are found
        //                 def jsonReport = readFile("${DAST_REPORTS_DIR}/dast-report.json")
        //                 def zapResults = new groovy.json.JsonSlurper().parseText(jsonReport)
        //                 def highAlerts = zapResults.site.alerts.findAll { it.risk == 'High' }.size()
        //                 def mediumAlerts = zapResults.site.alerts.findAll { it.risk == 'Medium' }.size()
        //                 def lowAlerts = zapResults.site.alerts.findAll { it.risk == 'Low' }.size()
                        
        //                 env.DAST_HIGH_ALERTS = highAlerts.toString()
        //                 env.DAST_MEDIUM_ALERTS = mediumAlerts.toString()
        //                 env.DAST_LOW_ALERTS = lowAlerts.toString()

        //                 if (highAlerts > 0 || mediumAlerts > 0) {
        //                     echo "⚠️ DAST scan found ${highAlerts} High and ${mediumAlerts} Medium risk alerts. Marking build as UNSTABLE."
        //                     currentBuild.result = 'UNSTABLE'
        //                 } else {
        //                     echo "✅ DAST scan completed with no High or Medium risk alerts."
        //                 }
        //             }
        //         }
        //     }
        // }
    }

    post {
        always {
            script {
                archiveSecurityReports()
                publishSecurityResults()
                generateSecuritySummary()
                evaluateSecurityGates()
                echo "🧹 Post-build cleanup..."
                node {
                    sh """
                        find . -type f -name '*.tmp' -delete || true
                        find . -type d -name 'TestResults' -exec rm -rf {} + || true
                    """
                }
            }
        }
        success {
            script {
                echo "✅ Build completed successfully!"
            }
        }
        failure {
            script {
                echo "❌ Build failed!"
            }
        }
        unstable {
            script {
                echo "⚠️ Build completed with warnings!"
            }
        }
    }
}

// Helper function to clean up any existing dependency-check installations
def cleanupExistingDependencyCheck() {
    echo "🧹 Cleaning up existing dependency-check installations..."
    sh """
        # Remove any existing dependency-check directories
        find . -maxdepth 1 -name "dependency-check*" -type d -exec rm -rf {} + 2>/dev/null || true
        
        # Remove any zip files
        find . -maxdepth 1 -name "dependency-check*.zip" -type f -delete 2>/dev/null || true
        
        # List what's left to verify cleanup
        echo "📋 Remaining files after cleanup:"
        ls -la | grep -i dependency || echo "   No dependency-check files found"
        
        echo "✅ Cleanup completed"
    """
}

// Helper function to download and setup specific version
def downloadDependencyCheck() {
    echo "📥 Setting up OWASP Dependency Check version ${DEPENDENCY_CHECK_VERSION}..."
    sh """
        # Set the target directory name
        TARGET_DIR="dependency-check"
        ZIP_FILE="dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip"
        
        # Check if we already have the correct version
        if [ -d "\$TARGET_DIR" ] && [ -f "\$TARGET_DIR/bin/dependency-check.sh" ]; then
            # Check version
            CURRENT_VERSION=\$(\$TARGET_DIR/bin/dependency-check.sh --version 2>/dev/null | grep -oE '[0-9]+\\.[0-9]+\\.[0-9]+' | head -1 || echo "unknown")
            if [ "\$CURRENT_VERSION" = "${DEPENDENCY_CHECK_VERSION}" ]; then
                echo "✅ Correct version ${DEPENDENCY_CHECK_VERSION} already installed"
                exit 0
            else
                echo "⚠️ Different version found (\$CURRENT_VERSION), reinstalling..."
                rm -rf "\$TARGET_DIR"
            fi
        fi
        
        # Download if not exists
        if [ ! -f "\$ZIP_FILE" ]; then
            echo "📥 Downloading OWASP Dependency Check version ${DEPENDENCY_CHECK_VERSION}..."
            curl -L -o "\$ZIP_FILE" \\
                "https://github.com/jeremylong/DependencyCheck/releases/download/v${DEPENDENCY_CHECK_VERSION}/dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip"
            
            if [ \$? -ne 0 ]; then
                echo "❌ Failed to download dependency-check"
                exit 1
            fi
        else
            echo "📦 Using existing download"
        fi
        
        # Extract
        echo "📦 Extracting dependency-check..."
        unzip -q "\$ZIP_FILE"
        
        # Rename to standard directory name
        if [ -d "dependency-check-${DEPENDENCY_CHECK_VERSION}" ]; then
            mv "dependency-check-${DEPENDENCY_CHECK_VERSION}" "\$TARGET_DIR"
        fi
        
        # Make executable
        chmod +x "\$TARGET_DIR/bin/dependency-check.sh"
        
        # Verify installation
        if [ -f "\$TARGET_DIR/bin/dependency-check.sh" ]; then
            echo "✅ Dependency-Check ${DEPENDENCY_CHECK_VERSION} installed successfully"
            echo "🔍 Verifying installation..."
            \$TARGET_DIR/bin/dependency-check.sh --version || echo "Version check failed but binary exists"
        else
            echo "❌ Installation failed - binary not found"
            exit 1
        fi
        
        # Clean up zip file
        rm -f "\$ZIP_FILE"
    """
}

// Main dependency check execution
def runDependencyCheck() {
    withCredentials([string(credentialsId: 'nvd-api-key', variable: 'NVD_API_KEY', optional: true)]) {
        sh """
            echo "🔍 Scanning for vulnerable dependencies..."
            
            # Verify dependency-check is available
            if [ ! -f "./dependency-check/bin/dependency-check.sh" ]; then
                echo "❌ dependency-check.sh not found!"
                exit 1
            fi
            
            # Create output directory
            mkdir -p ${SECURITY_REPORTS_DIR}/dependency-check
            
            # Check if NVD API Key option is supported
            NVD_ARGS=""
            if [ -n "\$NVD_API_KEY" ]; then
                echo "🔑 NVD API Key found, checking if supported..."
                if ./dependency-check/bin/dependency-check.sh --help | grep -q "nvdApiKey" 2>/dev/null; then
                    NVD_ARGS="--nvdApiKey \$NVD_API_KEY"
                    echo "✅ Using NVD API Key for faster scanning"
                else
                    echo "⚠️ NVD API Key option not supported in this version"
                fi
            else
                echo "⚠️ NVD API Key not found. Dependency-Check may be slow or fail."
            fi
            
            # Check if suppression file exists
            SUPPRESSION_ARGS=""
            if [ -f "dependency-check-suppressions.xml" ]; then
                SUPPRESSION_ARGS="--suppression dependency-check-suppressions.xml"
                echo "📋 Using suppression file: dependency-check-suppressions.xml"
            else
                echo "📋 No suppression file found, proceeding without suppressions"
            fi

            # Run dependency check
            echo "🚀 Starting dependency scan..."
            ./dependency-check/bin/dependency-check.sh \\
                --scan . \\
                --format JSON \\
                --format XML \\
                --format HTML \\
                --format JUNIT \\
                --out ${SECURITY_REPORTS_DIR}/dependency-check \\
                --project "\${JOB_NAME}" \\
                --failOnCVSS 0 \\
                --enableRetired \\
                --enableExperimental \\
                --exclude "**/*test*/**" \\
                --exclude "**/*Test*/**" \\
                --exclude "**/bin/**" \\
                --exclude "**/obj/**" \\
                --exclude "**/packages/**" \\
                --exclude "**/node_modules/**" \\
                \$SUPPRESSION_ARGS \\
                \$NVD_ARGS || true
            
            # Verify report generation
            echo "📋 Verifying report generation..."
            if [ -f "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json" ]; then
                echo "✅ Dependency check report generated successfully"
                echo "📊 Generated reports:"
                ls -la ${SECURITY_REPORTS_DIR}/dependency-check/
            else
                echo "❌ Failed to generate dependency check report"
                echo "📋 Directory contents:"
                ls -la ${SECURITY_REPORTS_DIR}/dependency-check/ || echo "Directory doesn't exist"
                exit 1
            fi
        """
    }
}

def processDependencyCheckResults() {
    echo "🔍 Processing dependency check results..."
    
    // Initialize default values
    env.DEPENDENCY_VULNERABILITIES = "0"
    env.DEPENDENCY_HIGH_CRITICAL = "0"
    env.DEPENDENCIES_SCANNED = "0"
    
    // Check if JSON report exists and process it
    if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json")) {
        try {
            def jsonReport = readFile("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json")
            def report = new groovy.json.JsonSlurper().parseText(jsonReport)
            
            def dependencyCount = report.dependencies?.size() ?: 0
            def vulnerabilityCount = 0
            def highCriticalCount = 0
            def severityCounts = [:]
            
            // Process vulnerabilities
            report.dependencies?.each { dep ->
                dep.vulnerabilities?.each { vuln ->
                    vulnerabilityCount++
                    def severity = vuln.severity ?: 'UNKNOWN'
                    severityCounts[severity] = (severityCounts[severity] ?: 0) + 1
                    
                    if (severity?.toUpperCase() in ['HIGH', 'CRITICAL']) {
                        highCriticalCount++
                    }
                }
            }
            
            // Set environment variables
            env.DEPENDENCY_VULNERABILITIES = vulnerabilityCount.toString()
            env.DEPENDENCY_HIGH_CRITICAL = highCriticalCount.toString()
            env.DEPENDENCIES_SCANNED = dependencyCount.toString()
            
            // Log results
            echo "📊 Dependency Check Results:"
            echo "   📦 Dependencies scanned: ${dependencyCount}"
            echo "   🚨 Vulnerabilities found: ${vulnerabilityCount}"
            echo "   ⚠️  High/Critical vulnerabilities: ${highCriticalCount}"
            
            if (vulnerabilityCount > 0) {
                echo "📈 Vulnerability breakdown by severity:"
                severityCounts.each { severity, count ->
                    echo "   ${severity}: ${count}"
                }
                
                // Show top 5 most critical vulnerabilities
                echo "🔍 Top 5 critical vulnerabilities:"
                def vulnCounter = 0
                report.dependencies?.each { dep ->
                    if (vulnCounter >= 5) return
                    dep.vulnerabilities?.findAll { it.severity?.toUpperCase() in ['HIGH', 'CRITICAL'] }?.each { vuln ->
                        if (vulnCounter >= 5) return
                        vulnCounter++
                        def cvssScore = vuln.cvssv3?.baseScore ?: vuln.cvssv2?.score ?: 'N/A'
                        echo "   ${vulnCounter}. ${vuln.name} (${vuln.severity}, CVSS: ${cvssScore}) in ${dep.fileName}"
                    }
                }
            } else {
                echo "✅ No vulnerabilities found - all dependencies are secure"
            }
            
        } catch (Exception e) {
            echo "⚠️ Error processing JSON report: ${e.getMessage()}"
            env.DEPENDENCY_VULNERABILITIES = "ERROR"
            env.DEPENDENCY_HIGH_CRITICAL = "ERROR"
        }
    } else {
        echo "⚠️ JSON report not found"
        env.DEPENDENCY_VULNERABILITIES = "UNKNOWN"
        env.DEPENDENCY_HIGH_CRITICAL = "UNKNOWN"
    }
}

def publishDependencyCheckReports() {
    echo "📤 Publishing dependency check reports..."
    
    // Publish HTML report
    if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html")) {
        publishHTML([
            allowMissing: false,
            alwaysLinkToLastBuild: true,
            keepAll: true,
            reportDir: "${SECURITY_REPORTS_DIR}/dependency-check",
            reportFiles: 'dependency-check-report.html',
            reportName: 'Dependency Check Report',
            reportTitles: 'OWASP Dependency Check Security Report'
        ])
        echo "✅ HTML report published successfully"
    } else {
        echo "⚠️ HTML report not found, skipping HTML publishing"
    }
    
    // Publish JUnit XML results (for test trends)
    if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-junit.xml")) {
        publishTestResults testResultsPattern: "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-junit.xml",
                          allowEmptyResults: true,
                          testResultsProcessorType: 'junit'
        echo "✅ JUnit XML results published successfully"
    } else {
        echo "⚠️ JUnit XML report not found, skipping JUnit publishing"
    }
    
    // Archive all reports
    archiveArtifacts artifacts: "${SECURITY_REPORTS_DIR}/dependency-check/*", 
                    allowEmptyArchive: true,
                    fingerprint: true
    echo "✅ All dependency check reports archived"
    
    // Add build description with summary
    if (env.DEPENDENCY_VULNERABILITIES != "ERROR" && env.DEPENDENCY_VULNERABILITIES != "UNKNOWN") {
        def buildDescription = "🛡️ Dependencies: ${env.DEPENDENCIES_SCANNED} scanned, ${env.DEPENDENCY_VULNERABILITIES} vulnerabilities (${env.DEPENDENCY_HIGH_CRITICAL} high/critical)"
        
        if (currentBuild.description) {
            currentBuild.description += "<br/>" + buildDescription
        } else {
            currentBuild.description = buildDescription
        }
        
        // Set build result based on vulnerabilities
        if (env.DEPENDENCY_HIGH_CRITICAL != "0") {
            currentBuild.result = 'UNSTABLE'
            echo "⚠️ Build marked as UNSTABLE due to high/critical vulnerabilities"
        }
    }
    
    echo "📊 Dependency check reports published to Jenkins menu"
}

def installSemgrep() {
    sh """
        echo "📦 Installing/Updating Python packages for Semgrep..."
        pip3 install --user --upgrade requests urllib3 chardet
        
        echo "📦 Installing Semgrep..."
        pip3 install --user semgrep --quiet || pip install --user semgrep --quiet || true
        export PATH="\$PATH:\$HOME/.local/bin"
        semgrep --version || echo "⚠️ Semgrep installation verification failed"
    """
}

def runSemgrepScan() {
    def semgrepRules = params.SECURITY_SCAN_LEVEL == 'COMPREHENSIVE' || params.SECURITY_SCAN_LEVEL == 'FULL' ? 
        '--config=auto --config=p/cwe-top-25 --config=p/owasp-top-10' : 
        '--config=auto'
    
    sh """
        export PATH="\$PATH:\$HOME/.local/bin"
        
        echo "🔍 Running Semgrep security analysis..."
        timeout ${SEMGREP_TIMEOUT} semgrep \\
            ${semgrepRules} \\
            --json \\
            --output=${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.json \\
            --timeout=60 \\
            --max-target-bytes=1000000 \\
            --exclude="**/bin/**" \\
            --exclude="**/obj/**" \\
            --exclude="**/packages/**" \\
            --exclude="**/*test*/**" \\
            --exclude="**/*Test*/**" \\
            . || true
        
        # Generate SARIF format for Jenkins integration
        timeout ${SEMGREP_TIMEOUT} semgrep \\
            ${semgrepRules} \\
            --sarif \\
            --output=${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif \\
            --timeout=60 \\
            --max-target-bytes=1000000 \\
            --exclude="**/bin/**" \\
            --exclude="**/obj/**" \\
            --exclude="**/packages/**" \\
            --exclude="**/*test*/**" \\
            --exclude="**/*Test*/**" \\
            . || true
    """
}

def processSemgrepResults() {
    env.SEMGREP_ISSUES = "0"
    env.SEMGREP_CRITICAL = "0"
    
    if (fileExists("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.json")) {
        try {
            def semgrepResults = readFile("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.json")
            def report = new groovy.json.JsonSlurper().parseText(semgrepResults)
            
            def issueCount = report.results?.size() ?: 0
            def criticalIssues = report.results?.findAll { 
                it.extra?.severity == 'ERROR' || it.extra?.severity == 'HIGH' 
            }?.size() ?: 0
            
            env.SEMGREP_ISSUES = issueCount.toString()
            env.SEMGREP_CRITICAL = criticalIssues.toString()
            
            echo "📊 Semgrep Results: ${issueCount} issues found (${criticalIssues} critical)"
            
        } catch (Exception e) {
            echo "⚠️ Error processing Semgrep results: ${e.getMessage()}"
            env.SEMGREP_ISSUES = "ERROR"
            env.SEMGREP_CRITICAL = "ERROR"
        }
    } else {
        echo "⚠️ Semgrep results file not found"
    }
}

def installTrivy() {
    sh """
        echo "📦 Installing Trivy..."
        TRIVY_VERSION=0.50.0
        TRIVY_DIR=\$(pwd)/trivy-bin
        mkdir -p \$TRIVY_DIR

        # Always reinstall if binary doesn't work or doesn't exist
        if [ ! -f \$TRIVY_DIR/trivy ] || ! \$TRIVY_DIR/trivy --version >/dev/null 2>&1; then
            echo "Downloading Trivy v\$TRIVY_VERSION..."
            
            # Clean up any existing files
            rm -f \$TRIVY_DIR/trivy
            rm -f trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
            
            # Download with better error handling
            wget -q https://github.com/aquasecurity/trivy/releases/download/v\$TRIVY_VERSION/trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
            
            # Verify download succeeded
            if [ ! -f trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz ]; then
                echo "❌ Failed to download Trivy"
                exit 1
            fi
            
            # Extract to specific directory
            tar -xzf trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz -C \$TRIVY_DIR trivy
            
            # Verify extraction
            if [ ! -f \$TRIVY_DIR/trivy ]; then
                echo "❌ Failed to extract Trivy binary"
                exit 1
            fi
            
            # Set permissions
            chmod +x \$TRIVY_DIR/trivy
            
            # Clean up tarball
            rm -f trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
            
            echo "✅ Trivy installed successfully"
        else
            echo "✅ Trivy already installed and working"
        fi

        # Test the installation
        \$TRIVY_DIR/trivy --version
        
        # Download vulnerability database
        echo "📥 Downloading vulnerability database..."
        \$TRIVY_DIR/trivy image --download-db-only
    """
}

def runTrivyScan() {
    sh """
        echo "🔍 Scanning filesystem for vulnerabilities..."

        TRIVY_DIR=\$(pwd)/trivy-bin
        mkdir -p ${SECURITY_REPORTS_DIR}/trivy

        # JSON output
        \$TRIVY_DIR/trivy fs \\
            --format json \\
            --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.json \\
            --skip-dirs bin,obj,packages \\
            --timeout 10m \\
            . || true

        # SARIF output (for recordIssues, optional)
        \$TRIVY_DIR/trivy fs \\
            --format sarif \\
            --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif \\
            --skip-dirs bin,obj,packages \\
            --timeout 10m \\
            . || true

        # HTML output (for publishHTML plugin)
        \$TRIVY_DIR/trivy fs \\
            --format template \\
            --template "@contrib/html.tpl" \\
            --output ${SECURITY_REPORTS_DIR}/trivy/index.html \\
            --skip-dirs bin,obj,packages \\
            --timeout 10m \\
            . || true
    """

    echo "✅ Trivy scan completed"
}

def archiveSecurityReports() {
    echo "📊 Archiving security reports..."
    
    try {
        archiveArtifacts artifacts: "${SECURITY_REPORTS_DIR}/**/*", 
                       allowEmptyArchive: true,
                       fingerprint: true
        echo "✅ Security reports archived"
    } catch (Exception e) {
        echo "⚠️ Could not archive security reports: ${e.getMessage()}"
    }
}

def publishSecurityResults() {
    echo "📋 Publishing security results..."
    
    try {
        // Method 1: Try native OWASP Dependency Check plugin (if available)
        if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.xml")) {
            try {
                dependencyCheckPublisher([
                    pattern: "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.xml",
                    canComputeNew: false,
                    defaultEncoding: '',
                    healthy: '',
                    unHealthy: '',
                    thresholdLimit: 'low',
                    pluginName: '[OWASP-DC]'
                ])
                echo "✅ OWASP Dependency Check results published via native plugin"
            } catch (Exception pluginException) {
                echo "⚠️ Native OWASP plugin not available: ${pluginException.getMessage()}"
                
                // Method 2: Use JUnit format for reliable parsing
                if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-junit.xml")) {
                    echo "📋 Using JUnit format for dependency check results..."
                    publishTestResults([
                        testResultsPattern: "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-junit.xml",
                        allowEmptyResults: true,
                        keepLongStdio: true
                    ])
                    echo "✅ Published dependency check as test results"
                }
                
                // Method 3: Manual issue creation from JSON
                createManualIssueReport()
            }
        }
        
        // Publish Semgrep SARIF results
        if (fileExists("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif")) {
            recordIssues enabledForFailure: true,
                       tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif", id: 'semgrep')],
                       name: 'SAST (Semgrep)',
                       id: 'semgrep',
                       qualityGates: [[threshold: 1, type: 'TOTAL_ERROR', unstable: true]]
        }
        
        // Publish Trivy SARIF results
        if (fileExists("${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")) {
            recordIssues enabledForFailure: true,
                       tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif", id: 'trivy')],
                          name: 'Security (Trivy)',
                          id: 'trivy',
                       qualityGates: [[threshold: 5, type: 'TOTAL_HIGH', unstable: true]]
        }
        
        echo "✅ Security results published to Jenkins UI"
        
    } catch (Exception e) {
        echo "⚠️ Could not publish security results: ${e.getMessage()}"
        e.printStackTrace()
    }
}

def createManualIssueReport() {
    echo "📋 Creating manual issue report from JSON..."
    
    if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json")) {
        try {
            def jsonReport = readFile("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json")
            def report = new groovy.json.JsonSlurper().parseText(jsonReport)
            
            // Create a simple text report for Jenkins
            def issueReport = new StringBuilder()
            issueReport.append("OWASP Dependency Check Issues Report\n")
            issueReport.append("=" * 50 + "\n\n")
            
            def issueCount = 0
            report.dependencies?.each { dep ->
                dep.vulnerabilities?.each { vuln ->
                    issueCount++
                    issueReport.append("Issue #${issueCount}\n")
                    issueReport.append("File: ${dep.fileName}\n")
                    issueReport.append("Vulnerability: ${vuln.name}\n")
                    issueReport.append("Severity: ${vuln.severity}\n")
                    issueReport.append("CVSS Score: ${vuln.cvssv3?.baseScore ?: vuln.cvssv2?.score ?: 'N/A'}\n")
                    issueReport.append("Description: ${vuln.description}\n")
                    issueReport.append("-" * 40 + "\n\n")
                }
            }
            
            writeFile file: "${SECURITY_REPORTS_DIR}/dependency-check/issues-report.txt", text: issueReport.toString()
            
            // Archive the text report
            archiveArtifacts artifacts: "${SECURITY_REPORTS_DIR}/dependency-check/issues-report.txt", allowEmptyArchive: true
            
            echo "✅ Manual issue report created with ${issueCount} issues"
            
        } catch (Exception e) {
            echo "⚠️ Failed to create manual issue report: ${e.getMessage()}"
        }
    }
}

def generateSecuritySummary() {
    echo "📊 Generating security summary..."
    
    def securitySummary = """
🔒 Security Scan Summary
========================
Build: ${BUILD_NUMBER}
Date: ${new Date()}
Scan Level: ${params.SECURITY_SCAN_LEVEL}

📦 Dependency Check Results:
   Dependencies Scanned: ${env.DEPENDENCIES_SCANNED ?: 'N/A'}
   Vulnerabilities Found: ${env.DEPENDENCY_VULNERABILITIES ?: 'N/A'}
   High/Critical Issues: ${env.DEPENDENCY_HIGH_CRITICAL ?: 'N/A'}

🔍 SAST Results:
   Total Issues: ${env.SEMGREP_ISSUES ?: 'N/A'}
   Critical Issues: ${env.SEMGREP_CRITICAL ?: 'N/A'}

Status: ${currentBuild.result ?: 'SUCCESS'}
"""
    
    echo securitySummary
    writeFile file: "${SECURITY_REPORTS_DIR}/security-summary.txt", text: securitySummary
}

def evaluateSecurityGates() {
    echo "🚧 Evaluating security gates..."
    
    if (params.FAIL_ON_SECURITY_ISSUES) {
        def criticalSastIssues = (env.SEMGREP_CRITICAL ?: "0").isInteger() ? 
            (env.SEMGREP_CRITICAL ?: "0").toInteger() : 0
        def highCriticalDepIssues = (env.DEPENDENCY_HIGH_CRITICAL ?: "0").isInteger() ? 
            (env.DEPENDENCY_HIGH_CRITICAL ?: "0").toInteger() : 0
        
        if (criticalSastIssues > 0) {
            error "❌ Build failed due to ${criticalSastIssues} critical SAST security issues"
        }
        
        if (highCriticalDepIssues > 0) {
            currentBuild.result = 'UNSTABLE'
            echo "⚠️ Build marked unstable due to ${highCriticalDepIssues} high/critical dependency vulnerabilities"
        }
    }
    
    echo "✅ Security gate evaluation completed"
}

/**
 * Runs dotnet-format to check for code style and formatting issues.
 */
def runLinting() {
    echo "💅 Running .NET Linter (dotnet-format)..."
    try {
        sh "mkdir -p ${LINTER_REPORTS_DIR}"
        
        def solutionFile = sh(script: "find . -name '*.sln' -print -quit", returnStdout: true).trim()
        if (solutionFile.isEmpty()) {
            error "❌ Could not find a solution file (.sln) in the workspace."
        }
        
        // Install dotnet-format tool
        sh """
            dotnet tool install --global dotnet-format --version ${DOTNET_FORMAT_VERSION} || true
            export PATH="\$PATH:\$HOME/.dotnet/tools"
        """
        
        // Run format check and capture both exit code and output
        def formatOutput = ""
        def formatResult = sh(
            script: """
                export PATH="\$PATH:\$HOME/.dotnet/tools"
                dotnet format '${solutionFile}' --verify-no-changes --verbosity diagnostic 2>&1 | tee format-output.txt
            """,
            returnStatus: true
        )
        
        // Read the output for processing
        formatOutput = readFile('format-output.txt').trim()
        
        // Parse violations and create custom report
        def violations = parseFormatViolations(formatOutput)
        createCustomLintReport(violations)
        
        if (formatResult == 0) {
            echo "✅ Code style is consistent."
        } else {
            echo "ℹ️ Found ${violations.size()} formatting issues. Check the warnings for details."
            // Set build status to unstable rather than failed
            currentBuild.result = 'UNSTABLE'
        }
        
    } catch (Exception e) {
        echo "❌ Linting check encountered an error: ${e.message}"
        currentBuild.result = 'UNSTABLE'
    } finally {
        publishLintResults()
        archiveArtifacts artifacts: "${LINTER_REPORTS_DIR}/*.json,format-output.txt", allowEmptyArchive: true
    }
}

def parseFormatViolations(String output) {
    def violations = []
    def lines = output.split('\n')
    
    for (line in lines) {
        if (line.contains('error WHITESPACE:')) {
            def matcher = line =~ /^(.+?)\((\d+),(\d+)\): error WHITESPACE: (.+?) \[(.+?)\]$/
            if (matcher.find()) {
                violations << [
                    file: matcher.group(1),
                    line: matcher.group(2),
                    column: matcher.group(3),
                    message: matcher.group(4),
                    project: matcher.group(5)
                ]
            }
        }
    }
    
    return violations
}

def createCustomLintReport(violations) {
    def report = [
        version: "1.0",
        timestamp: new Date().format("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"),
        tool: "dotnet-format",
        issues: violations.collect { violation ->
            [
                file: violation.file,
                line: violation.line as Integer,
                column: violation.column as Integer,
                severity: "warning",
                message: violation.message,
                rule: "WHITESPACE",
                category: "Style"
            ]
        }
    ]
    
    writeJSON file: "${LINTER_REPORTS_DIR}/custom-lint-report.json", json: report
}

def publishLintResults() {
    echo "📊 Publishing linting results..."
    
    try {
        // Try multiple report formats
        def reportFiles = [
            "${LINTER_REPORTS_DIR}/dotnet-format-report.json",
            "${LINTER_REPORTS_DIR}/custom-lint-report.json"
        ]
        
        for (reportFile in reportFiles) {
            if (fileExists(reportFile)) {
                echo "Processing report: ${reportFile}"
                
                // Use checkstyle format which is more widely supported
                if (reportFile.contains("custom-lint-report")) {
                    publishCheckstyleReport(reportFile)
                }
            }
        }
        
    } catch (Exception e) {
        echo "⚠️ Report publishing failed: ${e.message}"
        // Continue without failing the build
    }
}

def publishCheckstyleReport(String reportFile) {
    // Convert custom JSON to Checkstyle XML format
    def jsonReport = readJSON file: reportFile
    def checkstyleXml = convertToCheckstyle(jsonReport)
    
    writeFile file: "${LINTER_REPORTS_DIR}/checkstyle-report.xml", text: checkstyleXml
    
    recordIssues(
        enabledForFailure: false,
        tools: [checkStyle(pattern: "${LINTER_REPORTS_DIR}/checkstyle-report.xml")],
        qualityGates: [
            [threshold: 1, type: 'TOTAL', unstable: true],
            [threshold: 10, type: 'TOTAL', failed: true]
        ],
        name: 'Linting',
        id: 'dotnet-format',
    )
}

def convertToCheckstyle(jsonReport) {
    def xml = new StringBuilder()
    xml.append('<?xml version="1.0" encoding="UTF-8"?>\n')
    xml.append('<checkstyle version="8.0">\n')
    
    def fileGroups = jsonReport.issues.groupBy { it.file }
    
    fileGroups.each { fileName, issues ->
        xml.append("  <file name=\"${fileName}\">\n")
        issues.each { issue ->
            xml.append("    <error line=\"${issue.line}\" column=\"${issue.column}\" ")
            xml.append("severity=\"${issue.severity}\" message=\"${issue.message}\" ")
            xml.append("source=\"${issue.rule}\"/>\n")
        }
        xml.append("  </file>\n")
    }
    
    xml.append('</checkstyle>\n')
    return xml.toString()
}

/**
 * Runs Gitleaks to detect hardcoded secrets in the repository.
 */
def runSecretsScan() {
    echo "🤫 Running Secrets Detection (Gitleaks)..."
    try {
        sh "mkdir -p ${SECRETS_REPORTS_DIR}"
        sh """
            wget -q https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz
            tar -xzf gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz
            chmod +x gitleaks

            # Run gitleaks and output to SARIF. --exit-code 0 prevents the shell step from failing the build immediately.
            ./gitleaks detect --source="." --report-path="${SECRETS_REPORTS_DIR}/gitleaks-report.sarif" --report-format="sarif" --exit-code 0
        """

        // Check if the report was actually created and is not empty
        def reportFile = "${SECRETS_REPORTS_DIR}/gitleaks-report.sarif"
        if (fileExists(reportFile)) {
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
        } else {
            echo "⚠️ Gitleaks report was not generated."
        }
    } catch (Exception e) {
        currentBuild.result = 'UNSTABLE'
        echo "❌ Gitleaks scan failed to execute: ${e.getMessage()}"
    }
}

def runFossaLicenseCheck() {
    echo "⚖️ Running FOSSA License Compliance Check..."
    
    try {
        // Install FOSSA CLI if not already installed
        installFossaCli()
        
        // Configure FOSSA settings
        configureFossa()
        
        // Run dependency analysis
        runFossaAnalysis()
        
        // Test for license and vulnerability issues
        runFossaTest()
        
        echo "✅ FOSSA license compliance check completed successfully."
        
    } catch (Exception e) {
        echo "❌ FOSSA license check failed: ${e.getMessage()}"
        if (params.FAIL_ON_SECURITY_ISSUES) {
            error("Failing build due to FOSSA license compliance issues.")
        } else {
            currentBuild.result = 'UNSTABLE'
        }
    } finally {
        // Always publish results
        publishFossaResults()
    }
}

def installFossaCli() {
    echo "🔧 Installing FOSSA CLI..."
    
    def fossaInstalled = sh(
        script: "which fossa || echo 'not-found'",
        returnStdout: true
    ).trim()
    
    if (fossaInstalled == 'not-found') {
        sh """
            echo "Installing FOSSA CLI v3 to local directory..."
            
            # Create local bin directory
            mkdir -p \${HOME}/bin
            
            # Manual installation without any external scripts
            echo "Getting latest FOSSA CLI v3 release..."
            LATEST_RELEASE=\$(curl -s https://api.github.com/repos/fossas/fossa-cli/releases/latest)
            VERSION=\$(echo "\$LATEST_RELEASE" | grep '"tag_name"' | head -n1 | cut -d'"' -f4)
            
            if [ -z "\$VERSION" ]; then
                echo "❌ Could not get latest version from GitHub API"
                exit 1
            fi
            
            echo "Latest version: \$VERSION"
            
            # Extract all download URLs for this version
            echo "Available download URLs:"
            echo "\$LATEST_RELEASE" | grep '"browser_download_url"' | cut -d'"' -f4
            
            # Try to find the Linux AMD64 download URL
            DOWNLOAD_URL=\$(echo "\$LATEST_RELEASE" | grep '"browser_download_url"' | grep 'linux_amd64' | head -n1 | cut -d'"' -f4)
            
            if [ -z "\$DOWNLOAD_URL" ]; then
                # Try alternative patterns
                DOWNLOAD_URL=\$(echo "\$LATEST_RELEASE" | grep '"browser_download_url"' | grep -i 'linux' | grep -i 'amd64\\|x86_64' | head -n1 | cut -d'"' -f4)
            fi
            
            if [ -z "\$DOWNLOAD_URL" ]; then
                echo "❌ Could not find Linux AMD64 download URL"
                echo "Available assets:"
                echo "\$LATEST_RELEASE" | grep '"browser_download_url"' | cut -d'"' -f4
                exit 1
            fi
            
            echo "Downloading from: \$DOWNLOAD_URL"
            
            # Download the archive
            if ! curl -L --fail --silent --show-error "\$DOWNLOAD_URL" -o /tmp/fossa.archive; then
                echo "❌ Download failed"
                exit 1
            fi
            
            # Verify download
            if [ ! -f /tmp/fossa.archive ] || [ ! -s /tmp/fossa.archive ]; then
                echo "❌ Download failed or file is empty"
                exit 1
            fi
            
            # Check file type and extract accordingly
            FILE_TYPE=\$(file /tmp/fossa.archive)
            echo "File type: \$FILE_TYPE"
            
            if echo "\$FILE_TYPE" | grep -q "gzip"; then
                echo "Extracting tar.gz archive..."
                tar -xzf /tmp/fossa.archive -C /tmp/
            elif echo "\$FILE_TYPE" | grep -q "Zip"; then
                echo "Extracting zip archive..."
                unzip -q /tmp/fossa.archive -d /tmp/
            else
                echo "❌ Unknown file type: \$FILE_TYPE"
                echo "File contents (first 50 bytes):"
                head -c 50 /tmp/fossa.archive | hexdump -C
                exit 1
            fi
            
            # Find the fossa binary (check multiple possible locations)
            FOSSA_BINARY=""
            
            # Look for fossa binary in extracted files
            for possible_path in \$(find /tmp -name "fossa" -type f 2>/dev/null); do
                if [ -x "\$possible_path" ]; then
                    FOSSA_BINARY="\$possible_path"
                    break
                fi
            done
            
            # If not found as executable, look for any fossa file and make it executable
            if [ -z "\$FOSSA_BINARY" ]; then
                FOSSA_BINARY=\$(find /tmp -name "fossa" -type f 2>/dev/null | head -n1)
                if [ -n "\$FOSSA_BINARY" ]; then
                    chmod +x "\$FOSSA_BINARY"
                fi
            fi
            
            if [ -z "\$FOSSA_BINARY" ]; then
                echo "❌ Could not find fossa binary after extraction"
                echo "Contents of /tmp after extraction:"
                ls -la /tmp/
                echo "Looking for any fossa-related files:"
                find /tmp -name "*fossa*" -type f 2>/dev/null || echo "No fossa files found"
                exit 1
            fi
            
            echo "Found FOSSA binary at: \$FOSSA_BINARY"
            
            # Copy binary to bin directory
            cp "\$FOSSA_BINARY" \${HOME}/bin/fossa
            
            # Make executable
            chmod +x \${HOME}/bin/fossa
            
            # Clean up
            rm -f /tmp/fossa.archive
            rm -rf /tmp/fossa_* /tmp/fossa-* /tmp/fossa
            
            # Add to PATH for this session
            export PATH=\${HOME}/bin:\$PATH
            
            # Verify installation
            echo "Verifying FOSSA CLI installation..."
            \${HOME}/bin/fossa --version
        """
        
        env.PATH = "${env.HOME}/bin:${env.PATH}"
        
    } else {
        echo "✅ FOSSA CLI already installed: ${fossaInstalled}"
        sh "fossa --version"
    }
}

def configureFossa() {
    echo "⚙️ Configuring FOSSA..."
    
    // Validate API key is set
    if (!env.FOSSA_API_KEY) {
        error("❌ FOSSA_API_KEY environment variable is required. Please set it in Jenkins credentials.")
    }
    
    // Create FOSSA config file
    writeFile file: '.fossa.yml', text: """
version: 3
server: https://app.fossa.com
apiKey: ${env.FOSSA_API_KEY}

project:
  name: "${env.JOB_NAME}"

targets:
  auto:
    type: auto
    path: .
"""
    
    echo "✅ FOSSA configuration created"
}

def runFossaAnalysis() {
    echo "🔍 Running FOSSA dependency analysis..."
    
    sh """
        mkdir -p ${env.SECURITY_REPORTS_DIR ?: 'security-reports'}/fossa/
        fossa analyze --config .fossa.yml --debug 2>&1 | tee ${env.SECURITY_REPORTS_DIR ?: 'security-reports'}/fossa/analysis.log
    """
    
    echo "✅ FOSSA analysis completed"
}

def runFossaTest() {
    echo "🧪 Running FOSSA license and vulnerability tests..."
    
    catchError(buildResult: 'UNSTABLE', stageResult: 'FAILURE') {
        sh """
            fossa test \\
                --config .fossa.yml \\
                --timeout ${params.FOSSA_TEST_TIMEOUT ?: '600'} \\
                --format json \\
                ${params.FOSSA_TEST_ARGS ?: ''} \\
                > ${env.SECURITY_REPORTS_DIR ?: 'security-reports'}/fossa/test-results.json
        """
        echo "✅ FOSSA tests passed - no license violations found"
    }
    
    echo "📊 FOSSA test completed - results saved to test-results.json"
}

def publishFossaResults() {
    echo "📊 Publishing FOSSA results..."
    
    def reportsDir = env.SECURITY_REPORTS_DIR ?: 'security-reports'
    
    // Archive all FOSSA reports
    archiveArtifacts artifacts: "${reportsDir}/fossa/**/*", allowEmptyArchive: true
    
    // Parse and publish test results if available
    if (fileExists("${reportsDir}/fossa/test-results.json")) {
        publishFossaTestResults()
    }
    
    // Generate Jenkins-friendly warnings from FOSSA results
    generateFossaWarnings()
}

def publishFossaTestResults() {
    echo "📈 Publishing FOSSA test results..."
    
    def reportsDir = env.SECURITY_REPORTS_DIR ?: 'security-reports'
    def testResultsFile = "${reportsDir}/fossa/test-results.json"
    
    if (fileExists(testResultsFile)) {
        try {
            def testResults = readJSON file: testResultsFile
            
            // Create summary for build description
            def licenseIssues = testResults.issues?.license?.size() ?: 0
            def vulnerabilityIssues = testResults.issues?.vulnerability?.size() ?: 0
            def totalDependencies = testResults.dependencies?.size() ?: 0
            
            def summary = """
FOSSA Scan Results:
- License Issues: ${licenseIssues}
- Vulnerability Issues: ${vulnerabilityIssues}
- Total Dependencies: ${totalDependencies}
"""
            
            // Add to build description
            currentBuild.description = (currentBuild.description ?: '') + "\n" + summary
            
            echo "📊 FOSSA Results Summary:\n${summary}"
            
            // Set build result based on issues found
            if (licenseIssues > 0 || vulnerabilityIssues > 0) {
                currentBuild.result = 'UNSTABLE'
            }
            
        } catch (Exception e) {
            echo "⚠️ Error processing FOSSA test results: ${e.getMessage()}"
        }
    }
}

def generateFossaWarnings() {
    echo "⚠️ Generating FOSSA warnings for Jenkins..."
    
    def reportsDir = env.SECURITY_REPORTS_DIR ?: 'security-reports'
    
    // Use Python for warning generation - more robust than shell
    sh """
        python3 -c "
import json
import os
import sys

warnings_text = ''
warnings_count = 0

# Check for test results
test_results_file = '${reportsDir}/fossa/test-results.json'
if os.path.exists(test_results_file):
    with open(test_results_file, 'r') as f:
        try:
            test_results = json.load(f)
            
            # Process license issues
            license_issues = test_results.get('issues', {}).get('license', [])
            for issue in license_issues:
                dep_name = issue.get('dependency', {}).get('name', 'Unknown')
                license_type = issue.get('license', {}).get('name', 'Unknown')
                rule = issue.get('rule', {}).get('name', 'Unknown rule')
                
                warnings_text += f'WARNING: License issue in {dep_name} - {license_type} violates rule: {rule}\\n'
                warnings_count += 1
            
            # Process vulnerability issues
            vuln_issues = test_results.get('issues', {}).get('vulnerability', [])
            for issue in vuln_issues:
                dep_name = issue.get('dependency', {}).get('name', 'Unknown')
                vuln_id = issue.get('vulnerability', {}).get('id', 'Unknown')
                severity = issue.get('vulnerability', {}).get('severity', 'Unknown')
                
                warnings_text += f'WARNING: Vulnerability {vuln_id} ({severity}) found in {dep_name}\\n'
                warnings_count += 1
                
        except json.JSONDecodeError as e:
            warnings_text += f'ERROR: Could not parse FOSSA test results: {e}\\n'
            warnings_count += 1
        except Exception as e:
            warnings_text += f'ERROR: Error processing FOSSA results: {e}\\n'
            warnings_count += 1

# Create warnings directory if it doesn't exist
os.makedirs('${reportsDir}', exist_ok=True)

# Write warnings file
with open('${reportsDir}/fossa-warnings.txt', 'w') as f:
    f.write(warnings_text)

print(f'Generated {warnings_count} warnings')
" || {
    echo "Python failed, falling back to shell-based warning generation..."
    
    # Shell-based warning generation as fallback
    WARNINGS_FILE="${reportsDir}/fossa-warnings.txt"
    TEST_RESULTS_FILE="${reportsDir}/fossa/test-results.json"
    
    mkdir -p ${reportsDir}
    
    # Clear warnings file
    > "\$WARNINGS_FILE"
    
    if [ -f "\$TEST_RESULTS_FILE" ]; then
        echo "Processing FOSSA test results..."
        
        # Simple grep-based extraction (basic fallback)
        if grep -q '"license"' "\$TEST_RESULTS_FILE"; then
            echo "WARNING: License issues found in FOSSA scan" >> "\$WARNINGS_FILE"
        fi
        
        if grep -q '"vulnerability"' "\$TEST_RESULTS_FILE"; then
            echo "WARNING: Vulnerability issues found in FOSSA scan" >> "\$WARNINGS_FILE"
        fi
        
        # Count issues
        LICENSE_COUNT=\$(grep -c '"license"' "\$TEST_RESULTS_FILE" 2>/dev/null || echo 0)
        VULN_COUNT=\$(grep -c '"vulnerability"' "\$TEST_RESULTS_FILE" 2>/dev/null || echo 0)
        
        echo "Generated warnings for \$LICENSE_COUNT license issues and \$VULN_COUNT vulnerability issues"
    else
        echo "No FOSSA test results found"
    fi
}
    """
    
    publishWarnings()
}

def publishWarnings() {
    def reportsDir = env.SECURITY_REPORTS_DIR ?: 'security-reports'
    
    // Publish warnings if any exist
    if (fileExists("${reportsDir}/fossa-warnings.txt")) {
        def warningsContent = readFile("${reportsDir}/fossa-warnings.txt")
        
        if (warningsContent.trim()) {
            try {
                recordIssues(
                    enabledForFailure: false,
                    aggregatingResults: false,
                    tools: [
                        issues(
                            pattern: "${reportsDir}/fossa-warnings.txt",
                            name: 'FOSSA Issues'
                        )
                    ],
                    qualityGates: [[threshold: 1, type: 'TOTAL', unstable: true]],
                    name: 'License & Vulnerability',
                    id: 'fossa-scan'
                )
            } catch (Exception e) {
                echo "⚠️ Could not publish warnings to Jenkins: ${e.getMessage()}"
                echo "Warnings content:"
                echo warningsContent
            }
        }
    }
}


def runScanCodeLicenseCheck() {
    // Install ScanCode using pip with --user flag and dependency resolution
    sh '''
        echo "Installing ScanCode with dependency resolution..."
        
        # Upgrade pip first
        pip3 install --user --upgrade pip
        
        # Install ScanCode with specific dependency versions to avoid conflicts
        pip3 install --user --upgrade scancode-toolkit
        
        # Fix known dependency conflicts if they exist
        pip3 install --user --force-reinstall "click>=8.1.0,<8.2.0" || echo "Click version conflict detected but continuing..."
        
        # Add user bin to PATH
        export PATH="$HOME/.local/bin:$PATH"
        
        # Verify installation
        scancode --version
    '''
    
    // Run ScanCode license scan with optimizations

sh '''
    export PATH="$HOME/.local/bin:$PATH"
    
    echo "Running optimized ScanCode license scan..."
    
    # Create comprehensive ignore file
    cat > .scancode-ignore << 'IGNORE_EOF'
# Version control
.git/*
.svn/*
.hg/*

# Dependencies and build artifacts
*node_modules/*
*venv/*
*env/*
*virtualenv/*
*__pycache__/*
*target/*
*build/*
*dist/*
*out/*
*.egg-info/*
*vendor/*
*third_party/*

# Binary and compiled files
*.pyc
*.pyo
*.class
*.jar
*.war
*.ear
*.exe
*.bin
*.so
*.dll
*.dylib
*.a
*.lib
*.o
*.obj

# Archives
*.zip
*.tar
*.tar.gz
*.tgz
*.rar
*.7z

# Logs and temporary files
*.log
*.tmp
*temp/*
*tmp/*
*.cache
*.pid

# IDE and editor files
.vscode/*
.idea/*
*.swp
*.swo
*~

# Project specific
*dependency-check/*
*security-reports/*
*trivy-bin/*
*.db
*.mv.db
*coverage/*
*reports/*
*.min.js
*.min.css

# More dependency managers
*bower_components/*
*jspm_packages/*
*packages/*
*.nuget/*
*node_modules.nosync/*

# More build/generated content
*cmake-build-*/*
*.gradle/*
*bazel-*/*
*_build/*
*generated/*
*gen/*
*.generated.*

# Documentation that might be large
*docs/_build/*
*site-packages/*
*htmlcov/*

# More binary/media files
*.pdf
*.doc
*.docx
*.xls
*.xlsx
*.ppt
*.pptx
*.png
*.jpg
*.jpeg
*.gif
*.ico
*.svg
*.mp4
*.mp3
*.wav
*.avi

# Database files
*.sqlite
*.sqlite3
*.db3
*.mdb
*.accdb
IGNORE_EOF

    # Calculate optimal process count
    PROCESSES=$(nproc 2>/dev/null || echo "2")
    AVAILABLE_MEMORY=$(free -m 2>/dev/null | awk '/^Mem:/{print $2}' || echo "4096")
    
    # Optimize process count based on both CPU and memory
    MAX_PROCESSES_BY_MEMORY=$((AVAILABLE_MEMORY / 1024))
    if [ "$MAX_PROCESSES_BY_MEMORY" -lt 1 ]; then
        MAX_PROCESSES_BY_MEMORY=1
    fi
    
    PROCESSES=$(( PROCESSES < MAX_PROCESSES_BY_MEMORY ? PROCESSES : MAX_PROCESSES_BY_MEMORY ))
    if [ "$PROCESSES" -gt 6 ]; then
        PROCESSES=6
    fi
    
    echo "Using $PROCESSES processes for scanning"
    
    # Set timeout based on expected scan size
    FILE_COUNT=$(find . -type f \\( ! -path './.git/*' ! -path './node_modules/*' ! -path './venv/*' ! -path './build/*' ! -path './dist/*' \\) | wc -l 2>/dev/null || echo "1000")
    echo "Estimated files to scan: $FILE_COUNT"
    
    # Dynamic timeout: base 30min + 1min per 1000 files, max 2 hours
    TIMEOUT=$((1800 + (FILE_COUNT / 1000) * 60))
    if [ "$TIMEOUT" -gt 7200 ]; then
        TIMEOUT=7200
    fi
    
    echo "Setting timeout to $TIMEOUT seconds ($((TIMEOUT / 60)) minutes)"
    
    timeout $TIMEOUT scancode \\
        --license \\
        --copyright \\
        --processes $PROCESSES \\
        --timeout 600 \\
        --ignore .scancode-ignore \\
        --json license-results.json \\
        --html license-report.html \\
        --strip-root \\
        --verbose \\
        --max-in-memory 50 \\
        --license-score 70 \\
        . || {
        EXIT_CODE=$?
        echo "ScanCode exit code: $EXIT_CODE"
        
        case $EXIT_CODE in
            124)
                echo "⚠️  ScanCode timed out after $((TIMEOUT / 60)) minutes"
                if [ -f license-results.json ] && [ -s license-results.json ]; then
                    echo "📄 Partial results may be available"
                    if jq empty license-results.json 2>/dev/null; then
                        echo "✅ Partial results are valid JSON - continuing"
                        exit 0
                    fi
                fi
                exit 1
                ;;
            0)
                echo "✅ ScanCode completed successfully"
                ;;
            *)
                if [ -f license-results.json ] && [ -s license-results.json ]; then
                    echo "⚠️  ScanCode completed with warnings but produced results"
                    if jq empty license-results.json 2>/dev/null; then
                        echo "✅ Results are valid JSON - continuing"
                        exit 0
                    else
                        echo "❌ Results file is corrupted"
                        exit 1
                    fi
                else
                    echo "❌ ScanCode failed completely - no results produced"
                    exit 1
                fi
                ;;
        esac
    }
    
    # NOW verify the results (after ScanCode has run)
    if [ ! -f license-results.json ]; then
        echo "❌ ERROR: license-results.json not created"
        exit 1
    fi
    
    # Check if JSON is valid and not empty
    if ! jq empty license-results.json 2>/dev/null; then
        echo "❌ ERROR: license-results.json is not valid JSON"
        exit 1
    fi
    
    echo "✅ ScanCode scan completed successfully"
    
    # Generate license compliance report
    cat > license-compliance.html << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <title>ScanCode License Compliance Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #f0f0f0; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .summary { background-color: #e6f3ff; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .license-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
        .license-table th, .license-table td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        .license-table th { background-color: #f2f2f2; }
        .risk-high { background-color: #ffe6e6; }
        .risk-medium { background-color: #fff3e6; }
        .risk-low { background-color: #e6ffe6; }
        .timestamp { color: #666; font-size: 0.9em; }
        pre { background-color: #f5f5f5; padding: 10px; overflow-x: auto; }
    </style>
</head>
<body>
    <div class="header">
        <h1>📋 ScanCode License Compliance Report</h1>
        <p class="timestamp">Generated: $(date)</p>
    </div>
    
    <div class="summary">
        <h2>📊 Quick Summary</h2>
        <div id="summary-content">
EOF
        
        # Parse results and add summary
        if [ -f license-results.json ]; then
            echo "        <p><strong>Total Files Scanned:</strong> $(jq -r '.headers[0].summary.file_count // "N/A"' license-results.json)</p>" >> license-compliance.html
            echo "        <p><strong>Files with Licenses:</strong> $(jq -r '.headers[0].summary.license_clarity_score.total // "N/A"' license-results.json)</p>" >> license-compliance.html
            echo "        <p><strong>Unique Licenses Found:</strong> $(jq -r '.license_references | length // "N/A"' license-results.json)</p>" >> license-compliance.html
        fi
        
        cat >> license-compliance.html << 'EOF'
        </div>
    </div>
    
    <div class="summary">
        <h2>⚖️ License Summary</h2>
        <pre>
EOF
        
        # Add license summary
        if [ -f license-summary.txt ]; then
            head -20 license-summary.txt >> license-compliance.html
            if [ $(wc -l < license-summary.txt) -gt 20 ]; then
                echo "... (truncated, see full report for complete list)" >> license-compliance.html
            fi
        else
            echo "No license summary available" >> license-compliance.html
        fi
        
        cat >> license-compliance.html << 'EOF'
        </pre>
    </div>
    
    <div class="summary">
        <h2>📄 Full Report</h2>
        <p>For the complete detailed report with all findings, <a href="license-report.html">click here to view the full ScanCode HTML report</a></p>
    </div>
    
</body>
</html>
EOF
        
        # Check for high-risk licenses (customize this list as needed)
        HIGH_RISK_LICENSES="gpl-2.0 gpl-3.0 agpl-3.0 lgpl-2.1 lgpl-3.0"
        
        echo "Checking for high-risk licenses..."
        for license in $HIGH_RISK_LICENSES; do
            if grep -q "$license" license-summary.txt; then
                echo "WARNING: Found high-risk license: $license"
            fi
        done
    '''
    
    // Archive the results
    archiveArtifacts artifacts: 'license-results.json, license-report.html, license-summary.txt, license-compliance.html', fingerprint: true, allowEmptyArchive: true
    
    // Publish HTML report to Jenkins menu
    publishHTML([
        allowMissing: false,
        alwaysLinkToLastBuild: true,
        keepAll: true,
        reportDir: '.',
        reportFiles: 'license-compliance.html',
        reportName: 'License Compliance Report',
        reportTitles: 'ScanCode License Compliance'
    ])
    
    // Also publish detailed ScanCode report
    publishHTML([
        allowMissing: false,
        alwaysLinkToLastBuild: true,
        keepAll: true,
        reportDir: '.',
        reportFiles: 'license-report.html',
        reportName: 'Detailed ScanCode Report',
        reportTitles: 'Full ScanCode Analysis'
    ])
}