pipeline {
    agent any
    
    environment {
        // .NET Configuration
        DOTNET_VERSION = '6.0'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = 'true'
        
        // Java Configuration
        JAVA_HOME = tool('JDK11') // Configure this in Jenkins Global Tool Configuration
        MAVEN_HOME = tool('Maven') // Configure this in Jenkins Global Tool Configuration
        
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
        choice(
            name: 'TEST_FRAMEWORKS',
            choices: ['ALL', 'DOTNET_ONLY', 'JAVA_ONLY', 'NUNIT_ONLY', 'XUNIT_ONLY', 'JUNIT_ONLY'],
            description: 'Choose which test frameworks to run'
        )
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
                    echo "   Test Frameworks: ${params.TEST_FRAMEWORKS}"
                }
            }
        }
        
        stage('Environment Setup') {
            parallel {
                stage('Setup .NET Environment') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'NUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'XUNIT_ONLY', actual: params.TEST_FRAMEWORKS
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
                
                stage('Setup Java Environment') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JAVA_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🔧 Setting up Java environment..."
                            
                            sh """
                                echo "📦 Java Version:"
                                java -version
                                echo "📦 Maven Version:"
                                mvn -version
                            """
                        }
                    }
                }
            }
        }
        
        stage('Restore Dependencies') {
            parallel {
                stage('Restore .NET Dependencies') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'NUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'XUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "📦 Restoring .NET dependencies..."

                            def dotnetVerbosity
                            switch (params.LOG_LEVEL) {
                                case 'INFO':
                                    dotnetVerbosity = 'n' // 'normal'
                                    break
                                case 'DEBUG':
                                    dotnetVerbosity = 'd' // 'detailed'
                                    break
                                case 'WARN':
                                    dotnetVerbosity = 'm' // 'minimal' (closest for warn)
                                    break
                                case 'ERROR':
                                    dotnetVerbosity = 'q' // 'quiet' (closest for error)
                                    break
                                default:
                                    dotnetVerbosity = 'n' // Default to normal
                                    break
                            }
                            
                            // Find all .NET project files
                            def dotnetProjects = sh(
                                script: "find . -name '*.csproj' -o -name '*.sln' | head -1",
                                returnStdout: true
                            ).trim()
                            
                            if (dotnetProjects) {
                                sh """
                                    dotnet restore --verbosity ${dotnetverbosity}
                                """
                            } else {
                                echo "⚠️  No .NET project files found, skipping restore"
                            }
                        }
                    }
                }
                
                stage('Restore Java Dependencies') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JAVA_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "📦 Restoring Java dependencies..."
                            
                            // Changed: Use find to check for pom.xml recursively
                            def hasMaven = sh(script: "find . -name 'pom.xml' | head -1 || true", returnStdout: true).trim() != ""
                            // Changed: Use find to check for build.gradle recursively
                            def hasGradle = sh(script: "find . -name 'build.gradle' -o -name 'build.gradle.kts' | head -1 || true", returnStdout: true).trim() != ""
                            
                            if (hasMaven) {
                                // Changed: Execute Maven from the directory containing pom.xml
                                def pomDir = sh(script: "dirname $(find . -name 'pom.xml' | head -1)", returnStdout: true).trim()
                                dir(pomDir) { // Change directory to where the pom.xml is
                                    sh """
                                        mvn dependency:resolve -q
                                    """
                                }
                            } else if (hasGradle) {
                                // Changed: Execute Gradle from the directory containing build.gradle
                                def gradleDir = sh(script: "dirname $(find . -name 'build.gradle' -o -name 'build.gradle.kts' | head -1)", returnStdout: true).trim()
                                dir(gradleDir) { // Change directory to where the build.gradle is
                                    sh """
                                        ./gradlew dependencies --quiet
                                    """
                                }
                            } else {
                                echo "⚠️  No Java build files found, skipping dependency restore"
                            }
                        }
                    }
                }
            }
        }
        
        stage('Build Projects') {
            parallel {
                stage('Build .NET Projects') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'NUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'XUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🔨 Building .NET projects..."
                            
                            sh """
                                mkdir -p ${TEST_RESULTS_DIR}
                                mkdir -p ${COVERAGE_REPORTS_DIR}
                                
                                dotnet build --configuration Release --no-restore \
                                    --verbosity ${params.LOG_LEVEL.toLowerCase()}
                            """
                        }
                    }
                }
                
                stage('Build Java Projects') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JAVA_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🔨 Building Java projects..."
                            
                            def hasMaven = fileExists('pom.xml')
                            def hasGradle = fileExists('build.gradle') || fileExists('build.gradle.kts')
                            
                            sh """
                                mkdir -p ${TEST_RESULTS_DIR}
                                mkdir -p ${COVERAGE_REPORTS_DIR}
                            """
                            
                            if (hasMaven) {
                                sh """
                                    mvn compile test-compile -q
                                """
                            } else if (hasGradle) {
                                sh """
                                    ./gradlew compileJava compileTestJava --quiet
                                """
                            }
                        }
                    }
                }
            }
        }
        
        stage('Run Unit Tests') {
            parallel {
                stage('Run NUnit Tests') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'NUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running NUnit tests..."
                            
                            def nunitProjects = sh(
                                script: "find . -name '*Test*.csproj' -o -name '*Tests*.csproj' | grep -i nunit || true",
                                returnStdout: true
                            ).trim()
                            
                            if (nunitProjects) {
                                def coverageArg = params.GENERATE_COVERAGE ? 
                                    "--collect:\"XPlat Code Coverage\" --settings:CodeCoverage.runsettings" : ""
                                
                                sh """
                                    dotnet test ${nunitProjects} \
                                        --configuration Release \
                                        --no-build \
                                        --logger "trx;LogFileName=nunit-results.trx" \
                                        --results-directory ${TEST_RESULTS_DIR} \
                                        ${coverageArg} \
                                        --verbosity ${params.LOG_LEVEL.toLowerCase()}
                                """
                            } else {
                                echo "⚠️  No NUnit test projects found"
                            }
                        }
                    }
                    post {
                        always {
                            script {
                                if (fileExists("${TEST_RESULTS_DIR}/nunit-results.trx")) {
                                    mstest testResultsFile: "${TEST_RESULTS_DIR}/nunit-results.trx"
                                }
                            }
                        }
                    }
                }
                
                stage('Run xUnit Tests') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'XUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running xUnit tests..."
                            
                            def xunitProjects = sh(
                                script: "find . -name '*Test*.csproj' -o -name '*Tests*.csproj' | grep -i xunit || true",
                                returnStdout: true
                            ).trim()
                            
                            if (xunitProjects) {
                                def coverageArg = params.GENERATE_COVERAGE ? 
                                    "--collect:\"XPlat Code Coverage\" --settings:CodeCoverage.runsettings" : ""
                                
                                sh """
                                    dotnet test ${xunitProjects} \
                                        --configuration Release \
                                        --no-build \
                                        --logger "trx;LogFileName=xunit-results.trx" \
                                        --results-directory ${TEST_RESULTS_DIR} \
                                        ${coverageArg} \
                                        --verbosity ${params.LOG_LEVEL.toLowerCase()}
                                """
                            } else {
                                echo "⚠️  No xUnit test projects found"
                            }
                        }
                    }
                    post {
                        always {
                            script {
                                if (fileExists("${TEST_RESULTS_DIR}/xunit-results.trx")) {
                                    mstest testResultsFile: "${TEST_RESULTS_DIR}/xunit-results.trx"
                                }
                            }
                        }
                    }
                }
                
                stage('Run JUnit Tests') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JAVA_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "🧪 Running JUnit tests..."
                            
                            def hasMaven = fileExists('pom.xml')
                            def hasGradle = fileExists('build.gradle') || fileExists('build.gradle.kts')
                            
                            if (hasMaven) {
                                def coverageProfile = params.GENERATE_COVERAGE ? "-Pcoverage" : ""
                                
                                sh """
                                    mvn test ${coverageProfile} \
                                        -Dmaven.test.failure.ignore=true \
                                        -Dsurefire.rerunFailingTestsCount=2 \
                                        -Dtest.results.dir=${TEST_RESULTS_DIR}
                                """
                            } else if (hasGradle) {
                                def coverageTask = params.GENERATE_COVERAGE ? "jacocoTestReport" : ""
                                
                                sh """
                                    ./gradlew test ${coverageTask} \
                                        --continue \
                                        -Dtest.results.dir=${TEST_RESULTS_DIR}
                                """
                            } else {
                                echo "⚠️  No Java build files found"
                            }
                        }
                    }
                    post {
                        always {
                            script {
                                // Publish JUnit test results
                                def junitResults = sh(
                                    script: "find . -name 'TEST-*.xml' -o -name 'junit-*.xml' | head -10",
                                    returnStdout: true
                                ).trim()
                                
                                if (junitResults) {
                                    junit testResultsPattern: "**/TEST-*.xml,**/junit-*.xml"
                                }
                            }
                        }
                    }
                }
            }
        }
        
        stage('Generate Coverage Reports') {
            when {
                equals expected: true, actual: params.GENERATE_COVERAGE
            }
            parallel {
                stage('Generate .NET Coverage Report') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'DOTNET_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'NUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'XUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "📊 Generating .NET coverage report..."
                            
                            // Check if coverage files exist
                            def coverageFiles = sh(
                                script: "find . -name 'coverage.cobertura.xml' | head -5",
                                returnStdout: true
                            ).trim()
                            
                            if (coverageFiles) {
                                sh """
                                    # Install ReportGenerator if not already installed
                                    dotnet tool install --global dotnet-reportgenerator-globaltool || true
                                    
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
                
                stage('Generate Java Coverage Report') {
                    when {
                        anyOf {
                            equals expected: 'ALL', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JAVA_ONLY', actual: params.TEST_FRAMEWORKS
                            equals expected: 'JUNIT_ONLY', actual: params.TEST_FRAMEWORKS
                        }
                    }
                    steps {
                        script {
                            echo "📊 Generating Java coverage report..."
                            
                            def hasMaven = fileExists('pom.xml')
                            def hasGradle = fileExists('build.gradle') || fileExists('build.gradle.kts')
                            
                            if (hasMaven) {
                                // Maven with JaCoCo
                                sh """
                                    if [ -f target/site/jacoco/jacoco.xml ]; then
                                        cp -r target/site/jacoco ${COVERAGE_REPORTS_DIR}/java/ || true
                                    fi
                                """
                            } else if (hasGradle) {
                                // Gradle with JaCoCo
                                sh """
                                    if [ -d build/reports/jacoco ]; then
                                        cp -r build/reports/jacoco ${COVERAGE_REPORTS_DIR}/java/ || true
                                    fi
                                """
                            }
                        }
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
                        
                        // Java Coverage
                        if (fileExists("${COVERAGE_REPORTS_DIR}/java/jacoco.xml")) {
                            publishCoverage adapters: [jacocoAdapter("${COVERAGE_REPORTS_DIR}/java/jacoco.xml")],
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
                        find . -name '*.tmp' -delete || true
                        find . -name 'TestResults' -type d -exec rm -rf {} + || true
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
                //     message: "✅ Unit tests passed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        failure {
            script {
                echo "❌ Build failed!"
                
                // Optional: Send failure notification
                // slackSend channel: env.SLACK_CHANNEL, 
                //     color: 'danger', 
                //     message: "❌ Unit tests failed for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
                
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
                //     message: "⚠️  Unit tests completed with warnings for ${env.JOB_NAME} #${env.BUILD_NUMBER}"
            }
        }
        
        aborted {
            script {
                echo "🛑 Build was aborted!"
            }
        }
    }
}