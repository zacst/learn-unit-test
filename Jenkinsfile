pipeline {
    agent any

    environment {
        // .NET Configuration
        DOTNET_VERSION = '6.0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

        // Test Results Configuration
        TEST_RESULTS_DIR = 'test-results'
        COVERAGE_REPORTS_DIR = 'coverage-reports'

        // SonarQube Configuration
        SONARQUBE_URL = 'http://localhost:9000' // normal case
        SONAR_PROJECT_KEY = 'your-project-key'   // random

        // JFrog Server Configured in Jenkins Global Tool Configuration
        
        // JFrog CLI Build Info
        JFROG_CLI_BUILD_NAME = "${JOB_NAME}"
        JFROG_CLI_BUILD_NUMBER = "${BUILD_NUMBER}"

        // Repository Names (adjust to your JFrog setup)
        ARTIFACTORY_REPO_BINARIES = 'libs-release-local'  // For DLLs/EXEs
        ARTIFACTORY_REPO_NUGET = 'nuget-local'           // For NuGet packages
        ARTIFACTORY_REPO_REPORTS = 'reports-local'        // For test/coverage reports
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
    }

    stages {
        stage('Initialize') {
            steps {
                script {
                    switch (params.LOG_LEVEL) {
                        case 'INFO':
                            dotnetVerbosity = 'n' // normal
                            break
                        case 'DEBUG':
                            dotnetVerbosity = 'd' // detailed
                            break
                        case 'WARN':
                            dotnetVerbosity = 'm' // minimal
                            break
                        case 'ERROR':
                            dotnetVerbosity = 'q' // quiet
                            break
                        default:
                            dotnetVerbosity = 'n'
                    }
                    echo "🔧 dotnetVerbosity set to: ${dotnetVerbosity}"

                    // Initialize project arrays globally
                    env.nunitProjects = ''
                }
            }
        }

        stage('Checkout') {
            steps {
                script {
                    echo "🔄 Checking out source code..."
                    checkout scm

                    // Get commit information
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

                    // Check if .NET SDK is installed
                    def dotnetInstalled = sh(
                        script: "dotnet --version",
                        returnStatus: true
                    )

                    if (dotnetInstalled != 0) {
                        error "❌ .NET SDK not found. Please install .NET SDK ${DOTNET_VERSION}"
                    }

                    // Display .NET version
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

                    // Function to detect test framework from project file
                    def detectTestFramework = { projectPath ->
                        def projectContent = readFile(projectPath)
                        if (projectContent.contains('Microsoft.NET.Test.Sdk')) {
                            if (projectContent.contains('NUnit')) {
                                return 'NUNIT'
                            }
                        }
                        return 'UNKNOWN'
                    }

                    // Find all test projects
                    def allTestProjects = sh(
                        script: "find . -name '*.csproj' -path '*/Test*' -o -name '*.csproj' -path '*/*Test*' | head -50",
                        returnStdout: true
                    ).trim().split('\n').findAll { it.trim() }

                    // Initialize as proper lists
                    def nunitProjectsList = []

                    if (allTestProjects && allTestProjects[0]) {
                        echo "📁 Found ${allTestProjects.size()} potential test project(s)"

                        allTestProjects.each { project ->
                            if (fileExists(project)) {
                                def framework = detectTestFramework(project)
                                echo "    📋 ${project} -> ${framework}"

                                if (framework == 'NUNIT') {
                                    nunitProjectsList.add(project)
                                }
                            }
                        }
                    }

                    // Override with hardcoded projects if AUTO detection fails
                    if (nunitProjectsList.isEmpty()) {
                        echo "📋 Using hardcoded NUnit projects"
                        nunitProjectsList = ['./csharp-nunit/Calculator.Tests/Calculator.Tests.csproj']
                    }

                    // Store in environment variables for use in other stages
                    env.nunitProjects = nunitProjectsList.join(',')

                    echo "📊 NUnit Projects Summary:"
                    echo "    NUnit Projects: ${nunitProjectsList.size()}"
                    nunitProjectsList.each { echo "       - ${it}" }
                }
            }
        }

        stage('Restore .NET Dependencies') {
            steps {
                script {
                    echo "📦 Restoring .NET dependencies..."

                    // Find solution files
                    def solutionFiles = sh(
                        script: "find . -name '*.sln' | head -10",
                        returnStdout: true
                    ).trim().split('\n').findAll { it.trim() }

                    if (solutionFiles && solutionFiles[0]) {
                        solutionFiles.each { sln ->
                            if (fileExists(sln)) {
                                echo "📦 Restoring solution: ${sln}"
                                sh """
                                    dotnet restore '${sln}' --verbosity ${dotnetVerbosity}
                                """
                            }
                        }
                    } else {
                        echo "📦 No solution files found, restoring all projects..."
                        sh """
                            dotnet restore --verbosity ${dotnetVerbosity}
                        """
                    }
                }
            }
        }

        stage('Build .NET Project') {
            steps {
                script {
                    echo "🔨 Building .NET project..."

                    sh """
                        mkdir -p ${TEST_RESULTS_DIR}
                        mkdir -p ${COVERAGE_REPORTS_DIR}
                    """

                    // Find solution files
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
                }
            }
        }

        stage('Run NUnit Tests') {
            when {
                expression {
                    return env.nunitProjects && env.nunitProjects.trim() != ''
                }
            }
            steps {
                script {
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
                    } else {
                        echo "⚠️  No NUnit test projects found"
                    }
                }
            }
            post {
                always {
                    script {
                        // Find all matching TRX files
                        def trxFiles = sh(
                            script: "find ${TEST_RESULTS_DIR} -type f -name '*-results*.trx' 2>/dev/null || true",
                            returnStdout: true
                        ).trim()

                        if (trxFiles) {
                            echo "📊 Found test result files:"
                            trxFiles.split('\n').each { file ->
                                if (file.trim()) {
                                    echo "    - ${file}"
                                }
                            }

                            // Archive them as artifacts
                            archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*-results*.trx", allowEmptyArchive: true
                        } else {
                            echo '⚠️  No test result files found.'
                        }
                    }
                }
            }
        }

        stage('Generate Coverage Report') {
            when {
                equals expected: true, actual: params.GENERATE_COVERAGE
            }
            steps {
                script {
                    echo "📊 Generating coverage report..."

                    // Find all coverage files recursively
                    def coverageFiles = sh(
                        script: "find . -type f -name 'coverage.cobertura.xml' 2>/dev/null || true",
                        returnStdout: true
                    ).trim()

                    if (coverageFiles) {
                        echo "📊 Found coverage files:"
                        coverageFiles.split('\n').each { file ->
                            if (file.trim()) {
                                echo "    - ${file}"
                            }
                        }

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
                        echo "⚠️  No coverage files found"
                    }
                }
            }
        }

        stage('SAST (SonarQube)') {
            steps {
                withCredentials([string(credentialsId: 'sonarqube-token', variable: 'SONAR_TOKEN')]) {
                    sh '''
                        echo "Installing dotnet-sonarscanner (if not already installed)..."
                        dotnet tool install --global dotnet-sonarscanner || true
                        export PATH="$PATH:$HOME/.dotnet/tools"

                        echo "Starting SonarQube scan..."
                        dotnet sonarscanner begin \
                            /k:"$SONAR_PROJECT_KEY" \
                            /d:sonar.host.url="$SONARQUBE_URL" \
                            /d:sonar.login="$SONAR_TOKEN"

                        dotnet build --no-restore

                        dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN"
                    '''
                }
            }
        }

        stage('Publish Reports') {
            steps {
                script {
                    echo "📈 Publishing test reports and artifacts..."

                    // Publish test results
                    if (fileExists("${TEST_RESULTS_DIR}")) {
                        echo "📊 Publishing test results from: ${TEST_RESULTS_DIR}/*.trx"
                        try {
                            // Archive TRX files as artifacts
                            archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*.trx", allowEmptyArchive: true
                        } catch (Exception e) {
                            echo "⚠️  Warning: Could not publish test results: ${e.getMessage()}"
                        }
                    }

                    // Publish coverage reports
                    if (params.GENERATE_COVERAGE) {
                        def coberturaFile = "${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml"
                        if (fileExists(coberturaFile)) {
                            echo "📊 Publishing coverage report from: ${coberturaFile}"
                            try {
                                recordCoverage tools: [[parser: 'COBERTURA', pattern: coberturaFile]],
                                    sourceCodeRetention: 'EVERY_BUILD'
                                echo "✅ Coverage report published successfully"
                            } catch (Exception e) {
                                echo "⚠️  Warning: Could not publish coverage report: ${e.getMessage()}"
                            }
                        } else {
                            echo "⚠️  No coverage file found at: ${coberturaFile}"
                        }
                    }

                    // Archive artifacts
                    try {
                        archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/**,${COVERAGE_REPORTS_DIR}/**",
                            allowEmptyArchive: true,
                            fingerprint: true
                        echo "✅ Artifacts archived successfully"
                    } catch (Exception e) {
                        echo "⚠️  Warning: Could not archive artifacts: ${e.getMessage()}"
                    }
                }
            }
        }

        stage('Quality Gate') {
            steps {
                script {
                    echo "🚦 Evaluating quality gate..."

                    def buildResult = currentBuild.result ?: 'SUCCESS'
                    def buildStatus = currentBuild.currentResult

                    echo "📊 Build Status Summary:"
                    echo "    Build Result: ${buildResult}"
                    echo "    Current Status: ${buildStatus}"
                    echo "    Build Number: ${env.BUILD_NUMBER}"
                    echo "    Branch: ${env.BRANCH_NAME}"

                    // Get project counts safely
                    def nunitCount = (env.nunitProjects && env.nunitProjects.trim() != '') ? env.nunitProjects.split(',').size() : 0
                    echo "    NUnit Projects: ${nunitCount}"

                    // Quality gate criteria based on build status
                    if (buildStatus == 'FAILURE') {
                        error "❌ Quality gate failed: Build has failed"
                    }

                    if (buildStatus == 'UNSTABLE') {
                        echo "⚠️  Quality gate warning: Build is unstable"
                    }

                    // Check for test results files
                    def testResultsExist = fileExists("${TEST_RESULTS_DIR}")
                    if (testResultsExist) {
                        echo "✅ Test results directory found: ${TEST_RESULTS_DIR}"

                        def trxFiles = sh(
                            script: "find ${TEST_RESULTS_DIR} -name '*.trx' -type f 2>/dev/null | wc -l || echo 0",
                            returnStdout: true
                        ).trim() as Integer

                        if (trxFiles > 0) {
                            echo "📊 Found ${trxFiles} test result file(s)"
                        } else {
                            echo "⚠️  No test result files found in ${TEST_RESULTS_DIR}"
                        }
                    } else {
                        echo "⚠️  Test results directory not found: ${TEST_RESULTS_DIR}"
                    }

                    // Check for coverage reports if coverage generation is enabled
                    if (params.GENERATE_COVERAGE) {
                        def coverageReportExists = fileExists("${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml")
                        if (coverageReportExists) {
                            echo "📊 Coverage reports generated successfully"
                        } else {
                            echo "⚠️  Coverage reports not found"
                        }
                    }

                    // Overall quality gate assessment
                    if (buildStatus == 'SUCCESS') {
                        echo "✅ Quality gate passed!"
                    } else if (buildStatus == 'UNSTABLE') {
                        unstable "⚠️  Quality gate completed with warnings"
                    } else {
                        error "❌ Quality gate failed due to build issues"
                    }
                }
            }
        }

        stage('Upload to JFrog Artifactory') {
            steps {
                script {
                    echo "📦 Uploading .NET artifacts to JFrog Artifactory..."
                    
                    // Test JFrog connection
                    jf 'rt ping'
                    
                    // Find and list built artifacts
                    echo "🔍 Searching for .NET artifacts..."
                    sh """
                        find . -name '*.dll' -path '*/bin/Release/*' | head -20
                        find . -name '*.exe' -path '*/bin/Release/*' | head -20
                        find . -name '*.nupkg' | head -20
                    """
                    
                    // Upload DLLs and EXEs from Release builds
                    def artifactsFound = sh(
                        script: "find . -name '*.dll' -path '*/bin/Release/*' -o -name '*.exe' -path '*/bin/Release/*' | wc -l",
                        returnStdout: true
                    ).trim()
                    
                    if (artifactsFound.toInteger() > 0) {
                        echo "📤 Uploading compiled binaries..."
                        
                        // Upload DLLs
                        jf """rt u "**/bin/Release/**/*.dll" ${ARTIFACTORY_REPO_BINARIES}/ \
                            --build-name=${JFROG_CLI_BUILD_NAME} \
                            --build-number=${JFROG_CLI_BUILD_NUMBER} \
                            --flat=false \
                            --regexp=true"""
                        
                        // Upload EXEs
                        jf """rt u "**/bin/Release/**/*.exe" ${ARTIFACTORY_REPO_BINARIES}/ \
                            --build-name=${JFROG_CLI_BUILD_NAME} \
                            --build-number=${JFROG_CLI_BUILD_NUMBER} \
                            --flat=false \
                            --regexp=true"""
                    } else {
                        echo "⚠️ No compiled binaries found in Release folders"
                    }
                    
                    // Upload NuGet packages if they exist
                    def nugetPackages = sh(
                        script: "find . -name '*.nupkg' | wc -l",
                        returnStdout: true
                    ).trim()
                    
                    if (nugetPackages.toInteger() > 0) {
                        echo "📦 Uploading NuGet packages..."
                        jf """rt u "**/*.nupkg" ${ARTIFACTORY_REPO_NUGET}/ \
                            --build-name=${JFROG_CLI_BUILD_NAME} \
                            --build-number=${JFROG_CLI_BUILD_NUMBER} \
                            --flat=false \
                            --regexp=true"""
                    }
                    
                    // Upload test results and coverage reports if they exist
                    if (fileExists(TEST_RESULTS_DIR)) {
                        echo "📊 Uploading test results..."
                        jf """rt u "${TEST_RESULTS_DIR}/**/*" ${ARTIFACTORY_REPO_REPORTS}/test-results/ \
                            --build-name=${JFROG_CLI_BUILD_NAME} \
                            --build-number=${JFROG_CLI_BUILD_NUMBER} \
                            --flat=false \
                            --regexp=true"""
                    }
                    
                    if (fileExists(COVERAGE_REPORTS_DIR)) {
                        echo "📈 Uploading coverage reports..."
                        jf """rt u "${COVERAGE_REPORTS_DIR}/**/*" ${ARTIFACTORY_REPO_REPORTS}/coverage/ \
                            --build-name=${JFROG_CLI_BUILD_NAME} \
                            --build-number=${JFROG_CLI_BUILD_NUMBER} \
                            --flat=false \
                            --regexp=true"""
                    }
                    
                    // Collect and publish build info
                    echo "🔗 Publishing build information..."
                    jf "rt bp ${JFROG_CLI_BUILD_NAME} ${JFROG_CLI_BUILD_NUMBER}"
                }
            }
        }

        // CD
        stage('Deployment') {
            steps {
                script {
                    echo "🚀 Deployment stage (to be implemented)"
                    // deployment steps
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
                echo "⚠️  Build completed with warnings!"
            }
        }

        aborted {
            script {
                echo "🛑 Build was aborted!"
            }
        }
    }
}