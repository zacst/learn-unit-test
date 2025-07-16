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

        stage('Security Analysis') {
            when {
                expression { params.ENABLE_SECURITY_SCAN }
            }
            parallel {
                stage('Dependency Vulnerability Scan') {
                    steps {
                        script {
                            echo "🔒 Running OWASP Dependency Check..."
                            
                            try {
                                // Setup directories
                                sh "mkdir -p ${SECURITY_REPORTS_DIR}/dependency-check"
                                
                                // Download and setup OWASP Dependency Check
                                downloadDependencyCheck()
                                
                                // Run the scan
                                runDependencyCheck()
                                
                                // Process results
                                processDependencyCheckResults()
                                
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
                        expression { params.SECURITY_SCAN_LEVEL == 'FULL' }
                    }
                    steps {
                        script {
                            echo "🔒 Running license compliance check..."
                            
                            try {
                                sh "mkdir -p ${SECURITY_REPORTS_DIR}/license-check"
                                runLicenseCheck()
                                
                            } catch (Exception e) {
                                echo "❌ License check failed: ${e.getMessage()}"
                            }
                        }
                    }
                }
            }
            
            post {
                always {
                    script {
                        archiveSecurityReports()
                        publishSecurityResults()
                        generateSecuritySummary()
                        evaluateSecurityGates()
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

        stage('Upload to JFrog Artifactory') {
            steps {
                script {
                    echo "📦 Uploading .NET artifacts to JFrog Artifactory..."
                    
                    try {
                        // Test connection to Artifactory
                        jf 'rt ping'
                        echo "✅ JFrog Artifactory connection successful"
                        
                        // Debug: Show current directory and file structure
                        echo "🔍 Current directory structure:"
                        sh """
                            echo "Working directory: \$(pwd)"
                            echo "Contents of current directory:"
                            ls -la
                            echo "Looking for bin directories:"
                            find . -type d -name 'bin' | head -10
                            echo "Looking for .NET files in bin directories:"
                            find . -type f -name '*.dll' -o -name '*.exe' | head -10
                        """
                        
                        // Find and list all potential artifacts with existence check
                        def artifactsList = sh(
                            script: """
                                find . -type f \\( -name '*.dll' -o -name '*.exe' -o -name '*.pdb' \\) \\
                                    \\( -path '*/bin/Release/*' -o -path '*/bin/Debug/*' \\) | sort
                            """,
                            returnStdout: true
                        ).trim()
                        
                        echo "🔍 Searching for .NET artifacts completed"
                        
                        if (artifactsList) {
                            echo "📋 Found potential artifacts:"
                            echo "${artifactsList}"
                            
                            def artifactFiles = artifactsList.split('\n').findAll { 
                                it.trim() && !it.trim().isEmpty() && !it.contains('🔍') && !it.contains('Searching')
                            }
                            
                            if (artifactFiles.size() > 0) {
                                echo "📦 Processing ${artifactFiles.size()} artifact(s)..."
                                
                                // Get current working directory for absolute paths
                                def workingDir = sh(script: "pwd", returnStdout: true).trim()
                                echo "📁 Working directory: ${workingDir}"
                                
                                // Process each artifact file with existence verification
                                artifactFiles.each { artifactPath ->
                                    artifactPath = artifactPath.trim()
                                    
                                    // Verify file exists before attempting upload
                                    def fileExists = sh(
                                        script: "test -f '${artifactPath}' && echo 'true' || echo 'false'",
                                        returnStdout: true
                                    ).trim()
                                    
                                    if (fileExists == 'true') {
                                        // Get relative path for target structure
                                        def relativePath = artifactPath.startsWith('./') ? artifactPath.substring(2) : artifactPath
                                        
                                        echo "📤 Processing file: ${relativePath}"
                                        
                                        try {
                                            // FIXED: Use proper jf rt u command syntax
                                            jf "rt u \"${artifactPath}\" ${ARTIFACTORY_REPO_BINARIES}/${relativePath} --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
                                            
                                            echo "✅ Successfully uploaded: ${relativePath}"
                                            
                                        } catch (Exception uploadException) {
                                            echo "❌ Failed to upload ${relativePath}: ${uploadException.getMessage()}"
                                            
                                            // Alternative approach: Upload with simpler syntax
                                            try {
                                                echo "🔄 Trying simplified upload..."
                                                
                                                // Create a temporary spec file for upload
                                                def specContent = """
                                                {
                                                    "files": [
                                                        {
                                                            "pattern": "${artifactPath}",
                                                            "target": "${ARTIFACTORY_REPO_BINARIES}/${relativePath}"
                                                        }
                                                    ]
                                                }
                                                """
                                                
                                                writeFile file: 'upload-spec.json', text: specContent
                                                
                                                jf "rt u --spec=upload-spec.json --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER}"
                                                
                                                echo "✅ Successfully uploaded with spec file: ${relativePath}"
                                                
                                            } catch (Exception specException) {
                                                echo "❌ Spec file approach also failed: ${specException.getMessage()}"
                                                
                                                // Final fallback: Direct upload without build info
                                                try {
                                                    echo "🔄 Trying direct upload..."
                                                    
                                                    jf "rt u \"${artifactPath}\" ${ARTIFACTORY_REPO_BINARIES}/${relativePath}"
                                                    
                                                    echo "✅ Successfully uploaded (direct): ${relativePath}"
                                                    
                                                } catch (Exception directException) {
                                                    echo "❌ All upload approaches failed for: ${relativePath}"
                                                    echo "Error: ${directException.getMessage()}"
                                                }
                                            }
                                        }
                                    } else {
                                        echo "⚠️ File does not exist, skipping: ${artifactPath}"
                                    }
                                }
                                
                                // Upload NuGet packages if they exist
                                def nugetPackages = sh(
                                    script: "find . -name '*.nupkg' -o -name '*.snupkg' | head -20",
                                    returnStdout: true
                                ).trim()
                                
                                if (nugetPackages) {
                                    echo "📦 Found NuGet packages, uploading..."
                                    nugetPackages.split('\n').findAll { it.trim() }.each { packagePath ->
                                        def packageExists = sh(
                                            script: "test -f '${packagePath}' && echo 'true' || echo 'false'",
                                            returnStdout: true
                                        ).trim()
                                        
                                        if (packageExists == 'true') {
                                            echo "📤 Uploading NuGet package: ${packagePath}"
                                            
                                            try {
                                                jf "rt u \"${packagePath}\" ${ARTIFACTORY_REPO_NUGET}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=true"
                                                echo "✅ Successfully uploaded NuGet package: ${packagePath}"
                                            } catch (Exception nugetException) {
                                                echo "❌ Failed to upload NuGet package ${packagePath}: ${nugetException.getMessage()}"
                                            }
                                        }
                                    }
                                }
                                
                                // Upload test results and coverage reports
                                def testResultsPath = "${workingDir}/${TEST_RESULTS_DIR}"
                                def testResultsExists = sh(
                                    script: "test -d '${testResultsPath}' && echo 'true' || echo 'false'",
                                    returnStdout: true
                                ).trim()
                                
                                if (testResultsExists == 'true') {
                                    echo "📊 Uploading test results..."
                                    try {
                                        jf "rt u \"${testResultsPath}/*\" ${ARTIFACTORY_REPO_REPORTS}/test-results/${JFROG_CLI_BUILD_NAME}/${JFROG_CLI_BUILD_NUMBER}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
                                        echo "✅ Test results uploaded successfully"
                                    } catch (Exception testException) {
                                        echo "❌ Failed to upload test results: ${testException.getMessage()}"
                                    }
                                }
                                
                                def coveragePath = "${workingDir}/${COVERAGE_REPORTS_DIR}"
                                def coverageExists = sh(
                                    script: "test -d '${coveragePath}' && echo 'true' || echo 'false'",
                                    returnStdout: true
                                ).trim()
                                
                                if (coverageExists == 'true') {
                                    echo "📊 Uploading coverage reports..."
                                    try {
                                        jf "rt u \"${coveragePath}/**\" ${ARTIFACTORY_REPO_REPORTS}/coverage/${JFROG_CLI_BUILD_NAME}/${JFROG_CLI_BUILD_NUMBER}/ --build-name=${JFROG_CLI_BUILD_NAME} --build-number=${JFROG_CLI_BUILD_NUMBER} --flat=false"
                                        echo "✅ Coverage reports uploaded successfully"
                                    } catch (Exception coverageException) {
                                        echo "❌ Failed to upload coverage reports: ${coverageException.getMessage()}"
                                    }
                                }
                                
                                // Publish build info
                                try {
                                    jf "rt bp ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
                                    echo "✅ Build info published successfully"
                                } catch (Exception buildInfoException) {
                                    echo "❌ Failed to publish build info: ${buildInfoException.getMessage()}"
                                }
                                
                            } else {
                                echo "⚠️ No artifacts found to upload"
                            }
                        } else {
                            echo "⚠️ No .NET artifacts found in bin/Release or bin/Debug directories"
                            echo "🔍 Checking alternative locations..."
                            
                            // Check for artifacts in other common locations
                            def alternativeArtifacts = sh(
                                script: "find . -name '*.dll' -o -name '*.exe' | grep -v '/obj/' | head -10",
                                returnStdout: true
                            ).trim()
                            
                            if (alternativeArtifacts) {
                                echo "📋 Found artifacts in alternative locations:"
                                echo "${alternativeArtifacts}"
                            } else {
                                echo "❌ No .NET artifacts found anywhere"
                            }
                        }
                        
                    } catch (Exception e) {
                        echo "❌ JFrog Artifactory upload failed: ${e.getMessage()}"
                        echo "📊 This is non-critical - marking as unstable"
                        currentBuild.result = 'UNSTABLE'
                    }
                }
            }
        }

        stage('Deployment') {
            steps {
                script {
                    echo "🚀 Deployment stage (to be implemented)"
                }
            }
        }
    }

    post {
        always {
            script {
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

// Helper functions for better maintainability
def downloadDependencyCheck() {
    sh """
        if [ ! -f "dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip" ]; then
            echo "📥 Downloading OWASP Dependency Check..."
            curl -L -o dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip \\
                "https://github.com/jeremylong/DependencyCheck/releases/download/v${DEPENDENCY_CHECK_VERSION}/dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip"
            unzip -q dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip
        fi
    """
}

def runDependencyCheck() {
    sh """
        echo "🔍 Scanning for vulnerable dependencies..."
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
            --suppression dependency-check-suppressions.xml || true
        
        # Fix HTML report for Jenkins - ensure proper encoding and paths
        if [ -f "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html" ]; then
            echo "🔧 Fixing HTML report for Jenkins compatibility..."
            
            # Create a Jenkins-compatible HTML report
            sed -i 's|href="http://|href="https://|g' ${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html
            sed -i 's|src="http://|src="https://|g' ${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html
            
            # Ensure proper UTF-8 encoding
            sed -i '1i\\<?xml version="1.0" encoding="UTF-8"?>' ${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html
        fi
        
        # Validate XML format
        if [ -f "${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.xml" ]; then
            echo "🔍 Validating XML report format..."
            xmllint --noout ${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.xml || echo "⚠️ XML validation warning"
        fi
    """
}

def processDependencyCheckResults() {
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

def installSemgrep() {
    sh """
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

        if [ ! -f \$TRIVY_DIR/trivy ]; then
            wget -q https://github.com/aquasecurity/trivy/releases/download/v\$TRIVY_VERSION/trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz
            tar -xzf trivy_\${TRIVY_VERSION}_Linux-64bit.tar.gz -C \$TRIVY_DIR trivy
            chmod +x \$TRIVY_DIR/trivy
            export PATH=\$TRIVY_DIR:\$PATH
        fi

        \$TRIVY_DIR/trivy --version
        \$TRIVY_DIR/trivy image --download-db-only
    """
}

def runTrivyScan() {
    sh """
        echo "🔍 Scanning filesystem for vulnerabilities..."

        TRIVY_DIR=\$(pwd)/trivy-bin
        mkdir -p ${SECURITY_REPORTS_DIR}/trivy

        \$TRIVY_DIR/trivy fs \\
            --format json \\
            --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.json \\
            --skip-dirs bin,obj,packages \\
            --timeout 10m \\
            . || true

        \$TRIVY_DIR/trivy fs \\
            --format sarif \\
            --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif \\
            --skip-dirs bin,obj,packages \\
            --timeout 10m \\
            . || true
    """

    echo "✅ Trivy scan completed"
}

def runLicenseCheck() {
    sh """
        echo "📋 Checking NuGet package licenses..."
        
        find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | while read project; do
            echo "Checking project: \$project"
            
            dotnet list "\$project" package --vulnerable --include-transitive > \\
                ${SECURITY_REPORTS_DIR}/license-check/vulnerable-packages.txt 2>&1 || true
            
            dotnet list "\$project" package --include-transitive > \\
                ${SECURITY_REPORTS_DIR}/license-check/all-packages.txt 2>&1 || true
        done
        
        echo "📊 License compliance summary:" > ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
        echo "Generated on: \$(date)" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
        echo "Project: \${JOB_NAME}" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
        echo "Build: \${BUILD_NUMBER}" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
    """
    
    echo "✅ License compliance check completed"
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
                       tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif")],
                       qualityGates: [[threshold: 1, type: 'TOTAL_ERROR', unstable: true]]
        }
        
        // Publish Trivy SARIF results
        if (fileExists("${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")) {
            recordIssues enabledForFailure: true,
                       tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")],
                       qualityGates: [[threshold: 5, type: 'TOTAL_HIGH', unstable: true]]
        }
        
        // // Publish standalone HTML report with enhanced configuration
        // if (fileExists("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.html")) {
        //     publishHTML([
        //         allowMissing: false,
        //         alwaysLinkToLastBuild: true,
        //         keepAll: true,
        //         reportDir: "${SECURITY_REPORTS_DIR}/dependency-check",
        //         reportFiles: 'dependency-check-report.html',
        //         reportName: 'OWASP Dependency Check Report',
        //         reportTitles: '',
        //         includes: '**/*',
        //         allowUnpublishedJobExecutions: false,
        //         ignoreResourceNotFound: false,
        //         reportBuildPolicy: 'ALWAYS',
        //         escapeUnderscores: false
        //     ])
        //     echo "✅ HTML report published successfully"
        // }
        
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