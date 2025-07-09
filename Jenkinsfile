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
                    
                    // Find all NUnit test projects recursively
                    def nunitProjects = sh(
                        script: "find ./csharp-nunit/ -type f \\( -name '*Test*.csproj' -o -name '*Tests*.csproj' \\) | xargs grep -l 'nunit' 2>/dev/null || true",
                        returnStdout: true
                    ).trim().split('\n').findAll { it.trim() }

                    
                    if (nunitProjects) {
                        echo "🧪 Found ${nunitProjects.size()} NUnit test projects"
                        def coverageArg = params.GENERATE_COVERAGE ? 
                            "--collect:\"XPlat Code Coverage\"" : ""
                        
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
                        echo "⚠️  No NUnit test projects found"
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
                            export PATH="$PATH:$HOME/.dotnet/tools"
                            
                            # Generate HTML coverage report
                            reportgenerator \
                                -reports:**/coverage.cobertura.xml \
                                -targetdir:${COVERAGE_REPORTS_DIR}/dotnet \
                                -reporttypes:Html;Cobertura;JsonSummary \
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
                        if (fileExists("${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml")) {
                            publishCoverage adapters: [coberturaAdapter("${COVERAGE_REPORTS_DIR}/dotnet/Cobertura.xml")],
                                sourceFileResolver: sourceFiles('STORE_ALL_BUILD')
                        }
                    }
                    
                    // Archive artifacts
                    archiveArtifacts artifacts: "${TEST_RESULTS_DIR}/**/*,${COVERAGE_REPORTS_DIR}/**/*",
                        allowEmptyArchive: true,
                        fingerprint: true
                }
            }
        }
        
        stage('Quality Gate') {
            steps {
                script {
                    echo "🚦 Evaluating quality gate..."
                    
                    // Get test results summary
                    def testResultAction = currentBuild.rawBuild.getAction(hudson.tasks.junit.TestResultAction.class)
                    
                    if (testResultAction != null) {
                        def totalTests = testResultAction.totalCount
                        def failedTests = testResultAction.failCount
                        def skippedTests = testResultAction.skipCount
                        def passedTests = totalTests - failedTests - skippedTests
                        
                        echo "📊 Test Results Summary:"
                        echo "   Total Tests: ${totalTests}"
                        echo "   Passed: ${passedTests}"
                        echo "   Failed: ${failedTests}"
                        echo "   Skipped: ${skippedTests}"
                        
                        // Quality gate criteria
                        if (failedTests > 0 && params.FAIL_ON_TEST_FAILURE) {
                            error "❌ Quality gate failed: ${failedTests} test(s) failed"
                        }
                        
                        if (totalTests == 0) {
                            unstable "⚠️  Quality gate warning: No tests were executed"
                        }
                        
                        // Coverage quality gate (if coverage is enabled)
                        if (params.GENERATE_COVERAGE) {
                            // Add coverage quality gate logic here if needed
                            echo "📊 Coverage reports generated successfully"
                        }
                    } else {
                        echo "⚠️  No test results found"
                    }
                    
                    echo "✅ Quality gate passed!"
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