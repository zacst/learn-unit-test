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
        DEPENDENCY_CHECK_VERSION = '8.4.0'
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
                            // For .trx files (MSTest format)
                            publishTestResults testResultsPattern: "${TEST_RESULTS_DIR}/*.trx"
                            echo "✅ Test results published to Jenkins UI"
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
                                // Create security reports directory
                                sh "mkdir -p ${SECURITY_REPORTS_DIR}/dependency-check"
                                
                                // Download and run OWASP Dependency Check
                                sh """
                                    # Download OWASP Dependency Check if not exists
                                    if [ ! -f "dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip" ]; then
                                        echo "📥 Downloading OWASP Dependency Check..."
                                        curl -L -o dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip \\
                                            "https://github.com/jeremylong/DependencyCheck/releases/download/v${DEPENDENCY_CHECK_VERSION}/dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip"
                                        unzip -q dependency-check-${DEPENDENCY_CHECK_VERSION}-release.zip
                                    fi
                                    
                                    # Run dependency check on project files
                                    echo "🔍 Scanning for vulnerable dependencies..."
                                    ./dependency-check/bin/dependency-check.sh \\
                                        --scan . \\
                                        --format ALL \\
                                        --format JSON \\
                                        --format XML \\
                                        --out ${SECURITY_REPORTS_DIR}/dependency-check \\
                                        --project "\${JOB_NAME}" \\
                                        --failOnCVSS 7 \\
                                        --enableRetired \\
                                        --enableExperimental \\
                                        --log ${SECURITY_REPORTS_DIR}/dependency-check/dependency-check.log \\
                                        --exclude "**/*test*/**" \\
                                        --exclude "**/*Test*/**" \\
                                        --exclude "**/bin/**" \\
                                        --exclude "**/obj/**" \\
                                        --exclude "**/packages/**" \\
                                        --exclude "**/node_modules/**" \\
                                        --suppression dependency-check-suppressions.xml || true
                                """
                                
                                // Parse results
                                def dependencyCheckResults = readFile("${SECURITY_REPORTS_DIR}/dependency-check/dependency-check-report.json")
                                def jsonSlurper = new groovy.json.JsonSlurper()
                                def report = jsonSlurper.parseText(dependencyCheckResults)
                                
                                def vulnerabilityCount = report.dependencies?.sum { dep -> 
                                    dep.vulnerabilities?.size() ?: 0 
                                } ?: 0
                                
                                echo "📊 Dependency Check Results: ${vulnerabilityCount} vulnerabilities found"
                                
                                // Set environment variable for later use
                                env.DEPENDENCY_VULNERABILITIES = vulnerabilityCount.toString()
                                
                            } catch (Exception e) {
                                echo "❌ Dependency Check failed: ${e.getMessage()}"
                                env.DEPENDENCY_VULNERABILITIES = "ERROR"
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
                                
                                // Install Semgrep via pip
                                sh """
                                    echo "📦 Installing Semgrep..."
                                    pip3 install semgrep --user --quiet || pip install semgrep --user --quiet || true
                                    
                                    # Add user bin to PATH
                                    export PATH="\$PATH:\$HOME/.local/bin"
                                    
                                    # Verify installation
                                    semgrep --version || echo "Semgrep installation may have issues"
                                """
                                
                                // Run Semgrep scan
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
                                    
                                    # Generate SARIF format for better Jenkins integration
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
                                
                                // Parse Semgrep results
                                if (fileExists("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.json")) {
                                    def semgrepResults = readFile("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.json")
                                    def jsonSlurper = new groovy.json.JsonSlurper()
                                    def report = jsonSlurper.parseText(semgrepResults)
                                    
                                    def issueCount = report.results?.size() ?: 0
                                    def criticalIssues = report.results?.findAll { 
                                        it.extra?.severity == 'ERROR' || it.extra?.severity == 'HIGH' 
                                    }?.size() ?: 0
                                    
                                    echo "📊 Semgrep Results: ${issueCount} issues found (${criticalIssues} critical)"
                                    
                                    env.SEMGREP_ISSUES = issueCount.toString()
                                    env.SEMGREP_CRITICAL = criticalIssues.toString()
                                } else {
                                    echo "⚠️ Semgrep results file not found"
                                    env.SEMGREP_ISSUES = "0"
                                    env.SEMGREP_CRITICAL = "0"
                                }
                                
                            } catch (Exception e) {
                                echo "❌ Semgrep scan failed: ${e.getMessage()}"
                                env.SEMGREP_ISSUES = "ERROR"
                                env.SEMGREP_CRITICAL = "ERROR"
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
                                
                                // Install Trivy
                                sh """
                                    echo "📦 Installing Trivy..."
                                    if ! command -v trivy &> /dev/null; then
                                        # Install Trivy for Ubuntu/Debian
                                        wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
                                        echo "deb https://aquasecurity.github.io/trivy-repo/deb generic main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
                                        sudo apt-get update
                                        sudo apt-get install -y trivy
                                    fi
                                    
                                    # Update vulnerability database
                                    trivy image --download-db-only
                                """
                                
                                // Scan filesystem for vulnerabilities
                                sh """
                                    echo "🔍 Scanning filesystem for vulnerabilities..."
                                    trivy fs \\
                                        --format json \\
                                        --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.json \\
                                        --skip-files "**/bin/**,**/obj/**,**/packages/**" \\
                                        --timeout 10m \\
                                        . || true
                                    
                                    # Generate SARIF format
                                    trivy fs \\
                                        --format sarif \\
                                        --output ${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif \\
                                        --skip-files "**/bin/**,**/obj/**,**/packages/**" \\
                                        --timeout 10m \\
                                        . || true
                                """
                                
                                echo "✅ Trivy scan completed"
                                
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
                                
                                // Use dotnet list package to check licenses
                                sh """
                                    echo "📋 Checking NuGet package licenses..."
                                    
                                    # Find all project files
                                    find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | while read project; do
                                        echo "Checking project: \$project"
                                        
                                        # List packages with vulnerabilities
                                        dotnet list "\$project" package --vulnerable --include-transitive > \\
                                            ${SECURITY_REPORTS_DIR}/license-check/vulnerable-packages.txt 2>&1 || true
                                        
                                        # List all packages
                                        dotnet list "\$project" package --include-transitive > \\
                                            ${SECURITY_REPORTS_DIR}/license-check/all-packages.txt 2>&1 || true
                                    done
                                    
                                    # Check for problematic licenses (basic check)
                                    echo "📊 License compliance summary:" > ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
                                    echo "Generated on: \$(date)" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
                                    echo "Project: \${JOB_NAME}" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
                                    echo "Build: \${BUILD_NUMBER}" >> ${SECURITY_REPORTS_DIR}/license-check/license-summary.txt
                                """
                                
                                echo "✅ License compliance check completed"
                                
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
                        echo "📊 Processing security scan results..."
                        
                        // Archive all security reports
                        try {
                            archiveArtifacts artifacts: "${SECURITY_REPORTS_DIR}/**/*", 
                                           allowEmptyArchive: true,
                                           fingerprint: true
                            echo "✅ Security reports archived"
                        } catch (Exception e) {
                            echo "⚠️ Could not archive security reports: ${e.getMessage()}"
                        }
                        
                        // Publish security test results
                        try {
                            // Publish SARIF results if available
                            if (fileExists("${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif")) {
                                recordIssues enabledForFailure: true, 
                                           tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/semgrep/semgrep-results.sarif")],
                                           qualityGates: [[threshold: 1, type: 'TOTAL_ERROR', unstable: true]]
                            }
                            
                            if (fileExists("${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")) {
                                recordIssues enabledForFailure: true,
                                           tools: [sarif(pattern: "${SECURITY_REPORTS_DIR}/trivy/trivy-fs-results.sarif")],
                                           qualityGates: [[threshold: 5, type: 'TOTAL_HIGH', unstable: true]]
                            }
                            
                            echo "✅ Security issues published to Jenkins UI"
                        } catch (Exception e) {
                            echo "⚠️ Could not publish security results: ${e.getMessage()}"
                        }
                        
                        // Generate security summary
                        def securitySummary = """
                        🔒 Security Scan Summary:
                        ========================
                        Dependency Vulnerabilities: ${env.DEPENDENCY_VULNERABILITIES ?: 'N/A'}
                        SAST Issues: ${env.SEMGREP_ISSUES ?: 'N/A'}
                        Critical SAST Issues: ${env.SEMGREP_CRITICAL ?: 'N/A'}
                        Scan Level: ${params.SECURITY_SCAN_LEVEL}
                        """
                        
                        echo securitySummary
                        
                        // Write summary to file
                        writeFile file: "${SECURITY_REPORTS_DIR}/security-summary.txt", text: securitySummary
                        
                        // Determine if build should fail based on security issues
                        if (params.FAIL_ON_SECURITY_ISSUES) {
                            def criticalIssues = (env.SEMGREP_CRITICAL ?: "0").toInteger()
                            def dependencyVulns = env.DEPENDENCY_VULNERABILITIES
                            
                            if (criticalIssues > 0) {
                                error "❌ Build failed due to ${criticalIssues} critical security issues"
                            }
                            
                            if (dependencyVulns != "0" && dependencyVulns != "ERROR" && dependencyVulns != null) {
                                currentBuild.result = 'UNSTABLE'
                                echo "⚠️ Build marked unstable due to dependency vulnerabilities"
                            }
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