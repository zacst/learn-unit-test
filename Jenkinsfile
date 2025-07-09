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
        JUNIT_TEST_RESULTS_DIR = 'java-junit/target/surefire-reports' // Added for JUnit
        
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
            choices: ['AUTO', 'NUNIT', 'XUNIT', 'BOTH', 'JUNIT'], // Added JUNIT
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
                    nunitProjects = []
                    xunitProjects = []
                    
                    if (allTestProjects && allTestProjects[0]) {
                        echo "📁 Found ${allTestProjects.size()} potential test project(s)"
                        
                        allTestProjects.each { project ->
                            if (fileExists(project)) {
                                def framework = detectTestFramework(project)
                                echo "    📋 ${project} -> ${framework}"
                                
                                if (framework == 'NUNIT') {
                                    nunitProjects.add(project)
                                } else if (framework == 'XUNIT') {
                                    xunitProjects.add(project)
                                }
                            }
                        }
                    }
                    
                    // Override with hardcoded projects if AUTO detection fails or specific framework is selected
                    if (params.TEST_FRAMEWORK == 'NUNIT' || 
                        (params.TEST_FRAMEWORK == 'AUTO' && nunitProjects.isEmpty() && xunitProjects.isEmpty())) {
                        echo "📋 Using hardcoded NUnit projects"
                        nunitProjects = ['./csharp-nunit/Calculator.Tests/Calculator.Tests.csproj']
                    }
                    
                    if (params.TEST_FRAMEWORK == 'XUNIT' || params.TEST_FRAMEWORK == 'BOTH') {
                        echo "📋 Adding XUnit projects (add your XUnit project paths here)"
                        // Add your XUnit project paths here:
                        // xunitProjects.add('./csharp-xunit/Calculator.Tests/Calculator.Tests.csproj')
                    }
                    
                    echo "📊 Test Projects Summary:"
                    echo "    NUnit Projects: ${nunitProjects.size()}"
                    nunitProjects.each { echo "      - ${it}" }
                    echo "    XUnit Projects: ${xunitProjects.size()}"
                    xunitProjects.each { echo "      - ${it}" }
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
                            return nunitProjects && nunitProjects.size() > 0 &&
                                    (params.TEST_FRAMEWORK == 'AUTO' || params.TEST_FRAMEWORK == 'NUNIT' || params.TEST_FRAMEWORK == 'BOTH')
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running NUnit tests..."
                            
                            if (nunitProjects && nunitProjects.size() > 0) {
                                echo "🧪 Running ${nunitProjects.size()} NUnit test project(s)"

                                def coverageArg = params.GENERATE_COVERAGE 
                                    ? '--collect:"XPlat Code Coverage"' 
                                    : ""

                                nunitProjects.each { project ->
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
                            } else {
                                echo "⚠️  No NUnit test projects found"
                            }
                        }
                    }
                }
                
                stage('Run XUnit Tests') {
                    when {
                        expression { 
                            return xunitProjects && xunitProjects.size() > 0 &&
                                    (params.TEST_FRAMEWORK == 'AUTO' || params.TEST_FRAMEWORK == 'XUNIT' || params.TEST_FRAMEWORK == 'BOTH')
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running XUnit tests..."
                            
                            if (xunitProjects && xunitProjects.size() > 0) {
                                echo "🧪 Running ${xunitProjects.size()} XUnit test project(s)"

                                def coverageArg = params.GENERATE_COVERAGE 
                                    ? '--collect:"XPlat Code Coverage"' 
                                    : ""

                                xunitProjects.each { project ->
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
                            // Assuming your JUnit tests are part of a Maven project
                            // and the reports are generated in target/surefire-reports/
                            sh "mvn test"
                            echo "✅ JUnit tests execution completed."
                        }
                    }
                }
            }
            post {
                always {
                    script {
                        // Find all matching TRX files
                        def trxFiles = sh(
                            script: "find ${TEST_RESULTS_DIR} -type f -name '*-results*.trx' || true",
                            returnStdout: true
                        ).trim()

                        if (trxFiles) {
                            echo "📊 Found test result files:"
                            trxFiles.split('\n').each { file ->
                                echo "    - ${file}"
                            }
                            
                            // Archive them as artifacts
                            archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*-results*.trx", allowEmptyArchive: true

                            // Optional: Convert TRX to JUnit format and publish if needed
                            // publishTestResults adapters: [[$class: 'MSTestResultsTestDataPublisher', testResultsFile: "${TEST_RESULTS_DIR}/*.trx"]]
                        } else {
                            echo '⚠️  No .NET test result files found.'
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
                    echo "📊 Generating .NET coverage report..."
                    
                    // Find all coverage files recursively
                    def coverageFiles = sh(
                        script: "find . -type f -name 'coverage.cobertura.xml'",
                        returnStdout: true
                    ).trim()
                    
                    if (coverageFiles) {
                        echo "📊 Found coverage files:"
                        coverageFiles.split('\n').each { file ->
                            echo "    - ${file}"
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
                    if (params.TEST_FRAMEWORK == 'JUNIT' || (params.TEST_FRAMEWORK == 'AUTO' && fileExists("${JUNIT_TEST_RESULTS_DIR}/TEST-*.xml"))) {
                        echo "📊 Publishing JUnit test results from: ${JUNIT_TEST_RESULTS_DIR}/*.xml"
                        try {
                            junit "${JUNIT_TEST_RESULTS_DIR}/TEST-*.xml"
                            echo "✅ JUnit test reports published successfully"
                        } catch (Exception e) {
                            echo "⚠️  Warning: Could not publish JUnit test reports: ${e.getMessage()}"
                        }
                    }
                    
                    // Publish coverage reports
                    if (params.GENERATE_COVERAGE && params.TEST_FRAMEWORK != 'JUNIT') { // Only for .NET coverage
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
                        archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/**,${COVERAGE_REPORTS_DIR}/**,${JUNIT_TEST_RESULTS_DIR}/**", // Updated for JUnit
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
                    echo "    NUnit Projects: ${nunitProjects?.size() ?: 0}"
                    echo "    XUnit Projects: ${xunitProjects?.size() ?: 0}"
                    
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
                                script: "find ${TEST_RESULTS_DIR} -name '*.trx' -type f | wc -l",
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
                    if (params.TEST_FRAMEWORK == 'JUNIT' || (params.TEST_FRAMEWORK == 'AUTO' && fileExists("${JUNIT_TEST_RESULTS_DIR}/TEST-*.xml"))) {
                        def junitResultsExist = fileExists("${JUNIT_TEST_RESULTS_DIR}")
                        if (junitResultsExist) {
                            echo "✅ JUnit test results directory found: ${JUNIT_TEST_RESULTS_DIR}"
                            def junitXmlFiles = sh(
                                script: "find ${JUNIT_TEST_RESULTS_DIR} -name 'TEST-*.xml' -type f | wc -l",
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
                        find . -type d -name 'surefire-reports' -exec rm -rf {} + || true // Clean up JUnit reports
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