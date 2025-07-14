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
        // JUnit Test Results Configuration
        JUNIT_TEST_RESULTS_DIR = 'java-junit/target/surefire-reports'
        
        // // Notification Configuration
        // SLACK_CHANNEL = '#ci-cd' // Optional: Configure for Slack notifications
        // EMAIL_RECIPIENTS = 'team@company.com' // Optional: Configure for email notifications
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
        choice(
            name: 'TEST_FRAMEWORK',
            choices: ['AUTO', 'NUNIT', 'XUNIT', 'BOTH', 'JUNIT'],
            description: 'Choose test framework to run (AUTO detects automatically)'
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
                    echo "🧪 Test framework selection: ${params.TEST_FRAMEWORK}"
                    
                    // Initialize project arrays globally
                    env.nunitProjects = ''
                    env.xunitProjects = ''
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
            when {
                expression { 
                    return params.TEST_FRAMEWORK != 'JUNIT'
                }
            }
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
        
        stage('Discover Test Projects') {
            when {
                expression { 
                    return params.TEST_FRAMEWORK != 'JUNIT'
                }
            }
            steps {
                script {
                    echo "🔍 Discovering test projects..."
                    
                    // Function to detect test framework from project file
                    def detectTestFramework = { projectPath ->
                        def projectContent = readFile(projectPath)
                        if (projectContent.contains('Microsoft.NET.Test.Sdk')) {
                            if (projectContent.contains('NUnit')) {
                                return 'NUNIT'
                            } else if (projectContent.contains('xunit')) {
                                return 'XUNIT'
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
                    def xunitProjectsList = []
                    
                    if (allTestProjects && allTestProjects[0]) {
                        echo "📁 Found ${allTestProjects.size()} potential test project(s)"
                        
                        allTestProjects.each { project ->
                            if (fileExists(project)) {
                                def framework = detectTestFramework(project)
                                echo "    📋 ${project} -> ${framework}"
                                
                                if (framework == 'NUNIT') {
                                    nunitProjectsList.add(project)
                                } else if (framework == 'XUNIT') {
                                    xunitProjectsList.add(project)
                                }
                            }
                        }
                    }
                    
                    // Override with hardcoded projects if AUTO detection fails or specific framework is selected
                    if (params.TEST_FRAMEWORK == 'NUNIT' || 
                        (params.TEST_FRAMEWORK == 'AUTO' && nunitProjectsList.isEmpty() && xunitProjectsList.isEmpty())) {
                        echo "📋 Using hardcoded NUnit projects"
                        nunitProjectsList = ['./csharp-nunit/Calculator.Tests/Calculator.Tests.csproj']
                    }
                    
                    if (params.TEST_FRAMEWORK == 'XUNIT' || params.TEST_FRAMEWORK == 'BOTH') {
                        echo "📋 Adding XUnit projects (add your XUnit project paths here)"
                        // Add your XUnit project paths here:
                        // xunitProjectsList.add('./csharp-xunit/Calculator.Tests/Calculator.Tests.csproj')
                    }
                    
                    // Store in environment variables for use in other stages
                    env.nunitProjects = nunitProjectsList.join(',')
                    env.xunitProjects = xunitProjectsList.join(',')
                    
                    echo "📊 Test Projects Summary:"
                    echo "    NUnit Projects: ${nunitProjectsList.size()}"
                    nunitProjectsList.each { echo "      - ${it}" }
                    echo "    XUnit Projects: ${xunitProjectsList.size()}"
                    xunitProjectsList.each { echo "      - ${it}" }
                }
            }
        }
        
        stage('Restore .NET Dependencies') {
            when {
                expression { 
                    return params.TEST_FRAMEWORK != 'JUNIT'
                }
            }
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
            when {
                expression { 
                    return params.TEST_FRAMEWORK != 'JUNIT'
                }
            }
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
        
        stage('Run Tests') {
            parallel {
                stage('Run NUnit Tests') {
                    when {
                        expression { 
                            return params.TEST_FRAMEWORK != 'JUNIT' && 
                                   env.nunitProjects && env.nunitProjects.trim() != '' &&
                                   (params.TEST_FRAMEWORK == 'AUTO' || params.TEST_FRAMEWORK == 'NUNIT' || params.TEST_FRAMEWORK == 'BOTH')
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
                }
                
                stage('Run XUnit Tests') {
                    when {
                        expression { 
                            return params.TEST_FRAMEWORK != 'JUNIT' && 
                                   env.xunitProjects && env.xunitProjects.trim() != '' &&
                                   (params.TEST_FRAMEWORK == 'AUTO' || params.TEST_FRAMEWORK == 'XUNIT' || params.TEST_FRAMEWORK == 'BOTH')
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running XUnit tests..."
                            
                            def xunitProjectsList = env.xunitProjects.split(',').findAll { it.trim() }
                            
                            if (xunitProjectsList && xunitProjectsList.size() > 0) {
                                echo "🧪 Running ${xunitProjectsList.size()} XUnit test project(s)"

                                def coverageArg = params.GENERATE_COVERAGE 
                                    ? '--collect:"XPlat Code Coverage"' 
                                    : ""

                                xunitProjectsList.each { project ->
                                    project = project.trim()
                                    if (project) {
                                        echo "🧪 Running XUnit tests in: ${project}"
                                        def projectName = project.split('/')[-1].replace('.csproj', '')
                                        sh """
                                            dotnet test '${project}' \\
                                                --configuration Release \\
                                                --no-build \\
                                                --logger "trx;LogFileName=xunit-results-${projectName}.trx" \\
                                                --results-directory ${TEST_RESULTS_DIR} \\
                                                ${coverageArg} \\
                                                --verbosity ${dotnetVerbosity}
                                        """
                                    }
                                }
                            } else {
                                echo "⚠️  No XUnit test projects found"
                            }
                        }
                    }
                }

                stage('Run JUnit Tests') {
                    when {
                        expression { 
                            return params.TEST_FRAMEWORK == 'JUNIT'
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running JUnit tests (Maven Surefire)..."
                            
                            // Create the junit test results directory
                            sh "mkdir -p ${JUNIT_TEST_RESULTS_DIR}"
                            
                            // Change to the Java project directory if it exists
                            if (fileExists('java-junit')) {
                                dir('java-junit') {
                                    sh "mvn test"
                                }
                            } else {
                                // Run from root directory if no specific java directory
                                sh "mvn test"
                            }
                            
                            echo "✅ JUnit tests execution completed."
                        }
                    }
                }
            }
            post {
                always {
                    script {
                        // Find all matching TRX files for .NET tests
                        if (params.TEST_FRAMEWORK != 'JUNIT') {
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
                                echo '⚠️  No .NET test result files found.'
                            }
                        }
                        
                        // Archive JUnit test results
                        if (params.TEST_FRAMEWORK == 'JUNIT') {
                            def junitFiles = sh(
                                script: "find ${JUNIT_TEST_RESULTS_DIR} -type f -name 'TEST-*.xml' 2>/dev/null || true",
                                returnStdout: true
                            ).trim()

                            if (junitFiles) {
                                echo "📊 Found JUnit test result files:"
                                junitFiles.split('\n').each { file ->
                                    if (file.trim()) {
                                        echo "    - ${file}"
                                    }
                                }
                                
                                // Archive them as artifacts
                                archiveArtifacts artifacts: "${JUNIT_TEST_RESULTS_DIR}/TEST-*.xml", allowEmptyArchive: true
                            } else {
                                echo '⚠️  No JUnit test result files found.'
                            }
                        }
                    }
                }
            }
        }
        
        stage('Generate Coverage Report') {
            when {
                allOf {
                    equals expected: true, actual: params.GENERATE_COVERAGE
                    not { equals expected: 'JUNIT', actual: params.TEST_FRAMEWORK }
                }
            }
            steps {
                script {
                    echo "📊 Generating .NET coverage report..."
                    
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
                        echo "⚠️  No .NET coverage files found"
                    }
                }
            }
        }
        
        stage('Publish Reports') {
            steps {
                script {
                    echo "📈 Publishing test reports and artifacts..."
                    
                    // Publish .NET test results
                    if (params.TEST_FRAMEWORK != 'JUNIT' && fileExists("${TEST_RESULTS_DIR}")) {
                        echo "📊 Publishing .NET test results from: ${TEST_RESULTS_DIR}/*.trx"
                        try {
                            // The `mstest` publisher is for MSTest XML files.
                            // If you want to publish generic JUnit from .NET TRX, you might need a conversion tool
                            // or use the `junit` publisher if your .NET tests can produce JUnit XML directly.
                            // For this example, we'll assume the TRX files are already archived.
                            // If you had a tool to convert TRX to JUnit, you'd use something like:
                            // sh "trx2junit ${TEST_RESULTS_DIR}/*.trx > ${TEST_RESULTS_DIR}/junit-results.xml"
                            // junit "${TEST_RESULTS_DIR}/junit-results.xml"

                            // Since you're archiving TRX files directly, we'll keep that.
                            // If you want a visual representation in Jenkins for .NET tests,
                            // you'll need the MSTest plugin or convert TRX to JUnit.
                        } catch (Exception e) {
                            echo "⚠️  Warning: Could not publish .NET test results: ${e.getMessage()}"
                        }
                    }

                    // Publish JUnit test results
                    if (params.TEST_FRAMEWORK == 'JUNIT') {
                        echo "📊 Publishing JUnit test results from: ${JUNIT_TEST_RESULTS_DIR}/*.xml"
                        try {
                            if (fileExists("${JUNIT_TEST_RESULTS_DIR}")) {
                                def junitFiles = sh(
                                    script: "find ${JUNIT_TEST_RESULTS_DIR} -name 'TEST-*.xml' -type f 2>/dev/null || true",
                                    returnStdout: true
                                ).trim()
                                
                                if (junitFiles) {
                                    junit "${JUNIT_TEST_RESULTS_DIR}/TEST-*.xml"
                                    echo "✅ JUnit test reports published successfully"
                                } else {
                                    echo "⚠️  No JUnit test result files found to publish"
                                }
                            } else {
                                echo "⚠️  JUnit test results directory not found: ${JUNIT_TEST_RESULTS_DIR}"
                            }
                        } catch (Exception e) {
                            echo "⚠️  Warning: Could not publish JUnit test reports: ${e.getMessage()}"
                        }
                    }
                    
                    // Publish coverage reports
                    if (params.GENERATE_COVERAGE && params.TEST_FRAMEWORK != 'JUNIT') {
                        // .NET Coverage
                        def coberturaFile = "${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml"
                        if (fileExists(coberturaFile)) {
                            echo "📊 Publishing .NET coverage report from: ${coberturaFile}"
                            try {
                                recordCoverage tools: [[parser: 'COBERTURA', pattern: coberturaFile]],
                                    sourceCodeRetention: 'EVERY_BUILD'
                                echo "✅ Coverage report published successfully"
                            } catch (Exception e) {
                                echo "⚠️  Warning: Could not publish coverage report: ${e.getMessage()}"
                                // Don't fail the build, just warn
                            }
                        } else {
                            echo "⚠️  No .NET coverage file found at: ${coberturaFile}"
                        }
                    }
                    
                    // Archive artifacts
                    try {
                        archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/**,${COVERAGE_REPORTS_DIR}/**,${JUNIT_TEST_RESULTS_DIR}/**",
                            allowEmptyArchive: true,
                            fingerprint: true
                        echo "✅ Artifacts archived successfully"
                    } catch (Exception e) {
                        echo "⚠️  Warning: Could not archive artifacts: ${e.getMessage()}"
                        // Don't fail the build, just warn
                    }
                }
            }
        }
        
        stage('Quality Gate') {
            steps {
                script {
                    echo "🚦 Evaluating quality gate..."
                    
                    // Check build status and results using approved methods
                    def buildResult = currentBuild.result ?: 'SUCCESS'
                    def buildStatus = currentBuild.currentResult
                    
                    echo "📊 Build Status Summary:"
                    echo "    Build Result: ${buildResult}"
                    echo "    Current Status: ${buildStatus}"
                    echo "    Build Number: ${env.BUILD_NUMBER}"
                    echo "    Branch: ${env.BRANCH_NAME}"
                    echo "    Test Framework: ${params.TEST_FRAMEWORK}"
                    
                    // Get project counts safely
                    def nunitCount = (env.nunitProjects && env.nunitProjects.trim() != '') ? env.nunitProjects.split(',').size() : 0
                    def xunitCount = (env.xunitProjects && env.xunitProjects.trim() != '') ? env.xunitProjects.split(',').size() : 0
                    
                    echo "    NUnit Projects: ${nunitCount}"
                    echo "    XUnit Projects: ${xunitCount}"
                    
                    // Quality gate criteria based on build status
                    if (buildStatus == 'FAILURE') {
                        error "❌ Quality gate failed: Build has failed"
                    }
                    
                    if (buildStatus == 'UNSTABLE') {
                        echo "⚠️  Quality gate warning: Build is unstable"
                    }
                    
                    // Check for test results files (.NET)
                    if (params.TEST_FRAMEWORK != 'JUNIT') {
                        def testResultsExist = fileExists("${TEST_RESULTS_DIR}")
                        if (testResultsExist) {
                            echo "✅ .NET Test results directory found: ${TEST_RESULTS_DIR}"
                            
                            // Check for specific test result files
                            def trxFiles = sh(
                                script: "find ${TEST_RESULTS_DIR} -name '*.trx' -type f 2>/dev/null | wc -l || echo 0",
                                returnStdout: true
                            ).trim() as Integer
                            
                            if (trxFiles > 0) {
                                echo "📊 Found ${trxFiles} .NET test result file(s)"
                            } else {
                                echo "⚠️  No .NET test result files found in ${TEST_RESULTS_DIR}"
                            }
                        } else {
                            echo "⚠️  .NET Test results directory not found: ${TEST_RESULTS_DIR}"
                        }
                    }

                    // Check for JUnit test results files
                    if (params.TEST_FRAMEWORK == 'JUNIT') {
                        def junitResultsExist = fileExists("${JUNIT_TEST_RESULTS_DIR}")
                        if (junitResultsExist) {
                            echo "✅ JUnit test results directory found: ${JUNIT_TEST_RESULTS_DIR}"
                            def junitXmlFiles = sh(
                                script: "find ${JUNIT_TEST_RESULTS_DIR} -name 'TEST-*.xml' -type f 2>/dev/null | wc -l || echo 0",
                                returnStdout: true
                            ).trim() as Integer
                            if (junitXmlFiles > 0) {
                                echo "📊 Found ${junitXmlFiles} JUnit test result file(s)"
                            } else {
                                echo "⚠️  No JUnit test result files found in ${JUNIT_TEST_RESULTS_DIR}"
                            }
                        } else {
                            echo "⚠️  JUnit test results directory not found: ${JUNIT_TEST_RESULTS_DIR}"
                        }
                    }
                    
                    // Check for coverage reports if coverage generation is enabled (only for .NET)
                    if (params.GENERATE_COVERAGE && params.TEST_FRAMEWORK != 'JUNIT') {
                        def coverageReportExists = fileExists("${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml")
                        if (coverageReportExists) {
                            echo "📊 .NET Coverage reports generated successfully"
                        } else {
                            echo "⚠️  .NET Coverage reports not found"
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
    }
    
    post {
        always {
            script {
                echo "🧹 Post-build cleanup..."
                
                // Wrap sh commands in node block to provide FilePath context
                node {
                    sh """
                        find . -type f -name '*.tmp' -delete || true
                        find . -type d -name 'TestResults' -exec rm -rf {} + || true
                        find . -type d -name 'surefire-reports' -exec rm -rf {} + || true
                    """
                }
            }
        }
        
        success {
            script {
                echo "✅ Build completed successfully!"
                
                // Optional: Send success notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //    color: 'good', 
                //    message: "✅ Tests passed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        failure {
            script {
                echo "❌ Build failed!"
                
                // Optional: Send failure notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //    color: 'danger', 
                //    message: "❌ Tests failed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
                
                // Optional: Send email notification
                // emailext subject: "❌ Build Failed: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                //    body: "Build failed. Check console output for details.",
                //    to: env.EMAIL_RECIPIENTS
            }
        }
        
        unstable {
            script {
                echo "⚠️  Build completed with warnings!"
                
                // Optional: Send unstable notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //    color: 'warning', 
                //    message: "⚠️  Tests completed with warnings for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        aborted {
            script {
                echo "🛑 Build was aborted!"
            }
        }
    }
}