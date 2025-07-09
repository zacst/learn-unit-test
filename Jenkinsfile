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
                    echo "   Branch: ${env.BRANCH_NAME}"
                    echo "   Commit: ${env.GIT_COMMIT_SHORT}"
                    echo "   Message: ${env.GIT_COMMIT_MSG}"
                    echo "   Test Framework: NUnit"
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
        
        stage('Restore .NET Dependencies') {
            steps {
                script {
                    echo "📦 Restoring .NET dependencies..."
                    
                    sh """
                        dotnet restore csharp-nunit/Calculator.sln --verbosity ${dotnetVerbosity}
                    """
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

                        dotnet build csharp-nunit/Calculator.sln --configuration Release --no-restore \\
                            --verbosity ${dotnetVerbosity}
                    """
                }
            }
        }
        
        stage('Run NUnit Tests') {
            steps {
                script {
                    echo "🧪 Running NUnit tests..."
                    
                    // Hardcoded list of NUnit test projects
                    def nunitProjects = [
                        './csharp-nunit/Calculator.Tests/Calculator.Tests.csproj',
                    ]

                    if (nunitProjects) {
                        echo "🧪 Running ${nunitProjects.size()} NUnit test project(s)"

                        def coverageArg = params.GENERATE_COVERAGE 
                            ? '--collect:"XPlat Code Coverage"' 
                            : ""

                        nunitProjects.each { project ->
                            echo "🧪 Running NUnit tests in: ${project}"
                            sh """
                                dotnet test '${project}' \
                                    --configuration Release \
                                    --no-build \
                                    --logger "trx;LogFileName=nunit-results-\$(basename '${project}' .csproj).trx" \
                                    --results-directory ${TEST_RESULTS_DIR} \
                                    ${coverageArg} \
                                    --verbosity ${dotnetVerbosity}
                            """
                        }
                    } else {
                        echo "⚠️  No NUnit test projects specified"
                    }
                }
            }
            post {
                always {
                    script {
                        // Find all matching TRX files
                        def trxFiles = sh(
                            script: "find ${TEST_RESULTS_DIR} -type f -name '*nunit-results*.trx' || true",
                            returnStdout: true
                        ).trim()

                        if (trxFiles) {
                            // Archive them manually as artifacts if you want to keep them
                            archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/*nunit-results*.trx", allowEmptyArchive: true

                            // Optional: Convert TRX to JUnit format and publish if needed
                            // e.g., using a custom script or a tool like trx2junit
                            // then:
                            // junit '**/converted-results.xml'
                        } else {
                            echo 'No test result files found.'
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
                    
                    // Publish coverage reports
                    if (params.GENERATE_COVERAGE) {
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
                        archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/**/*,${COVERAGE_REPORTS_DIR}/**/*",
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
                    echo "   Build Result: ${buildResult}"
                    echo "   Current Status: ${buildStatus}"
                    echo "   Build Number: ${env.BUILD_NUMBER}"
                    echo "   Branch: ${env.BRANCH_NAME}"
                    
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
                        
                        // Check for specific test result files
                        def trxFiles = sh(
                            script: "find ${TEST_RESULTS_DIR} -name '*.trx' -type f | wc -l",
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
                    """
                }
            }
        }
        
        success {
            script {
                echo "✅ Build completed successfully!"
                
                // Optional: Send success notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //     color: 'good', 
                //     message: "✅ NUnit tests passed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        failure {
            script {
                echo "❌ Build failed!"
                
                // Optional: Send failure notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //     color: 'danger', 
                //     message: "❌ NUnit tests failed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
                
                // Optional: Send email notification
                // emailext subject: "❌ Build Failed: ${env.JOB_NAME} #${env.BUILD_NUMBER}",
                //     body: "Build failed. Check console output for details.",
                //     to: env.EMAIL_RECIPIENTS
            }
        }
        
        unstable {
            script {
                echo "⚠️  Build completed with warnings!"
                
                // Optional: Send unstable notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //     color: 'warning', 
                //     message: "⚠️  NUnit tests completed with warnings for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        aborted {
            script {
                echo "🛑 Build was aborted!"
            }
        }
    }
}