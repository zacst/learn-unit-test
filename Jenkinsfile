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
                            
                            sh """
                                if [ "${params.TEST_FRAMEWORKS}" = "ALL" ] || [ "${params.TEST_FRAMEWORKS}" = "DOTNET_ONLY" ] || [ "${params.TEST_FRAMEWORKS}" = "NUNIT_ONLY" ]; then
                                    dotnet restore csharp-nunit/Calculator/Calculator.sln --verbosity ${dotnetVerbosity}
                                fi

                                if [ "${params.TEST_FRAMEWORKS}" = "ALL" ] || [ "${params.TEST_FRAMEWORKS}" = "DOTNET_ONLY" ] || [ "${params.TEST_FRAMEWORKS}" = "XUNIT_ONLY" ]; then
                                    dotnet restore csharp-xunit/Calculator/Calculator.sln --verbosity ${dotnetVerbosity}
                                fi
                            """
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
                            
                            // Find all Maven and Gradle build files recursively
                            def allPomFiles = sh(
                                script: "find . -type f -name 'pom.xml'",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            def allGradleFiles = sh(
                                script: "find . -type f \\( -name 'build.gradle' -o -name 'build.gradle.kts' \\)",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (allPomFiles) {
                                echo "📦 Found ${allPomFiles.size()} Maven projects"
                                allPomFiles.each { pomFile ->
                                    def pomDir = sh(script: "dirname '${pomFile}'", returnStdout: true).trim()
                                    echo "📦 Restoring Maven dependencies in: ${pomDir}"
                                    dir(pomDir) {
                                        sh "mvn dependency:resolve -q"
                                    }
                                }
                            } else if (allGradleFiles) {
                                echo "📦 Found ${allGradleFiles.size()} Gradle projects"
                                allGradleFiles.each { gradleFile ->
                                    def gradleDir = sh(script: "dirname '${gradleFile}'", returnStdout: true).trim()
                                    echo "📦 Restoring Gradle dependencies in: ${gradleDir}"
                                    dir(gradleDir) {
                                        sh "./gradlew dependencies --quiet"
                                    }
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

                                if [ "${params.TEST_FRAMEWORKS}" = "ALL" ] || [ "${params.TEST_FRAMEWORKS}" = "DOTNET_ONLY" ] || [ "${params.TEST_FRAMEWORKS}" = "NUNIT_ONLY" ]; then
                                    dotnet build csharp-nunit/Calculator/Calculator.sln --configuration Release --no-restore \\
                                        --verbosity ${params.LOG_LEVEL.toLowerCase()}
                                fi

                                if [ "${params.TEST_FRAMEWORKS}" = "ALL" ] || [ "${params.TEST_FRAMEWORKS}" = "DOTNET_ONLY" ] || [ "${params.TEST_FRAMEWORKS}" = "XUNIT_ONLY" ]; then
                                    dotnet build csharp-xunit/Calculator/Calculator.sln --configuration Release --no-restore \\
                                        --verbosity ${params.LOG_LEVEL.toLowerCase()}
                                fi
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
                            
                            sh """
                                mkdir -p ${TEST_RESULTS_DIR}
                                mkdir -p ${COVERAGE_REPORTS_DIR}
                            """
                            
                            // Build all Maven projects recursively
                            def allPomFiles = sh(
                                script: "find . -type f -name 'pom.xml'",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            def allGradleFiles = sh(
                                script: "find . -type f \\( -name 'build.gradle' -o -name 'build.gradle.kts' \\)",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (allPomFiles) {
                                echo "🔨 Building ${allPomFiles.size()} Maven projects"
                                allPomFiles.each { pomFile ->
                                    def pomDir = sh(script: "dirname '${pomFile}'", returnStdout: true).trim()
                                    echo "🔨 Building Maven project in: ${pomDir}"
                                    dir(pomDir) {
                                        sh "mvn compile test-compile -q"
                                    }
                                }
                            } else if (allGradleFiles) {
                                echo "🔨 Building ${allGradleFiles.size()} Gradle projects"
                                allGradleFiles.each { gradleFile ->
                                    def gradleDir = sh(script: "dirname '${gradleFile}'", returnStdout: true).trim()
                                    echo "🔨 Building Gradle project in: ${gradleDir}"
                                    dir(gradleDir) {
                                        sh "./gradlew compileJava compileTestJava --quiet"
                                    }
                                }
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
                            
                            // Find all NUnit test projects recursively
                            def nunitProjects = sh(
                                script: "find . -type f \\( -name '*Test*.csproj' -o -name '*Tests*.csproj' \\) | xargs grep -l 'nunit' 2>/dev/null || true",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (nunitProjects) {
                                echo "🧪 Found ${nunitProjects.size()} NUnit test projects"
                                def coverageArg = params.GENERATE_COVERAGE ? 
                                    "--collect:\"XPlat Code Coverage\" --settings:CodeCoverage.runsettings" : ""
                                
                                nunitProjects.each { project ->
                                    echo "🧪 Running NUnit tests in: ${project}"
                                    sh """
                                        dotnet test '${project}' \
                                            --configuration Release \
                                            --no-build \
                                            --logger "trx;LogFileName=nunit-results-\$(basename '${project}' .csproj).trx" \
                                            --results-directory ${TEST_RESULTS_DIR} \
                                            ${coverageArg} \
                                            --verbosity ${params.LOG_LEVEL.toLowerCase()}
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
                                // Publish all NUnit test results
                                def trxFiles = sh(
                                    script: "find ${TEST_RESULTS_DIR} -type f -name '*nunit-results*.trx' || true",
                                    returnStdout: true
                                ).trim()
                                
                                if (trxFiles) {
                                    mstest testResultsFile: "${TEST_RESULTS_DIR}/*nunit-results*.trx"
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
                            
                            // Find all xUnit test projects recursively
                            def xunitProjects = sh(
                                script: "find . -type f \\( -name '*Test*.csproj' -o -name '*Tests*.csproj' \\) | xargs grep -l 'xunit' 2>/dev/null || true",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (xunitProjects) {
                                echo "🧪 Found ${xunitProjects.size()} xUnit test projects"
                                def coverageArg = params.GENERATE_COVERAGE ? 
                                    "--collect:\"XPlat Code Coverage\" --settings:CodeCoverage.runsettings" : ""
                                
                                xunitProjects.each { project ->
                                    echo "🧪 Running xUnit tests in: ${project}"
                                    sh """
                                        dotnet test '${project}' \
                                            --configuration Release \
                                            --no-build \
                                            --logger "trx;LogFileName=xunit-results-\$(basename '${project}' .csproj).trx" \
                                            --results-directory ${TEST_RESULTS_DIR} \
                                            ${coverageArg} \
                                            --verbosity ${params.LOG_LEVEL.toLowerCase()}
                                    """
                                }
                            } else {
                                echo "⚠️  No xUnit test projects found"
                            }
                        }
                    }
                    post {
                        always {
                            script {
                                // Publish all xUnit test results
                                def trxFiles = sh(
                                    script: "find ${TEST_RESULTS_DIR} -type f -name '*xunit-results*.trx' || true",
                                    returnStdout: true
                                ).trim()
                                
                                if (trxFiles) {
                                    mstest testResultsFile: "${TEST_RESULTS_DIR}/*xunit-results*.trx"
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
                            
                            // Find all Maven and Gradle projects recursively
                            def allPomFiles = sh(
                                script: "find . -type f -name 'pom.xml'",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            def allGradleFiles = sh(
                                script: "find . -type f \\( -name 'build.gradle' -o -name 'build.gradle.kts' \\)",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (allPomFiles) {
                                echo "🧪 Running JUnit tests in ${allPomFiles.size()} Maven projects"
                                def coverageProfile = params.GENERATE_COVERAGE ? "-Pcoverage" : ""
                                
                                allPomFiles.each { pomFile ->
                                    def pomDir = sh(script: "dirname '${pomFile}'", returnStdout: true).trim()
                                    echo "🧪 Running JUnit tests in Maven project: ${pomDir}"
                                    dir(pomDir) {
                                        sh """
                                            mvn test ${coverageProfile} \
                                                -Dmaven.test.failure.ignore=true \
                                                -Dsurefire.rerunFailingTestsCount=2 \
                                                -Dtest.results.dir=${TEST_RESULTS_DIR}
                                        """
                                    }
                                }
                            } else if (allGradleFiles) {
                                echo "🧪 Running JUnit tests in ${allGradleFiles.size()} Gradle projects"
                                def coverageTask = params.GENERATE_COVERAGE ? "jacocoTestReport" : ""
                                
                                allGradleFiles.each { gradleFile ->
                                    def gradleDir = sh(script: "dirname '${gradleFile}'", returnStdout: true).trim()
                                    echo "🧪 Running JUnit tests in Gradle project: ${gradleDir}"
                                    dir(gradleDir) {
                                        sh """
                                            ./gradlew test ${coverageTask} \
                                                --continue \
                                                -Dtest.results.dir=${TEST_RESULTS_DIR}
                                        """
                                    }
                                }
                            } else {
                                echo "⚠️  No Java build files found"
                            }
                        }
                    }
                    post {
                        always {
                            script {
                                // Publish JUnit test results from all subdirectories
                                def junitResults = sh(
                                    script: "find . -type f \\( -name 'TEST-*.xml' -o -name 'junit-*.xml' \\)",
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
                            
                            // Find all coverage files recursively
                            def coverageFiles = sh(
                                script: "find . -type f -name 'coverage.cobertura.xml'",
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
                            
                            // Find all Maven and Gradle projects recursively
                            def allPomFiles = sh(
                                script: "find . -type f -name 'pom.xml'",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            def allGradleFiles = sh(
                                script: "find . -type f \\( -name 'build.gradle' -o -name 'build.gradle.kts' \\)",
                                returnStdout: true
                            ).trim().split('\n').findAll { it.trim() }
                            
                            if (allPomFiles) {
                                echo "📊 Collecting Maven coverage reports from ${allPomFiles.size()} projects"
                                allPomFiles.each { pomFile ->
                                    def pomDir = sh(script: "dirname '${pomFile}'", returnStdout: true).trim()
                                    def projectName = sh(script: "basename '${pomDir}'", returnStdout: true).trim()
                                    sh """
                                        if [ -f '${pomDir}/target/site/jacoco/jacoco.xml' ]; then
                                            mkdir -p ${COVERAGE_REPORTS_DIR}/java/${projectName}
                                            cp -r '${pomDir}/target/site/jacoco'/* ${COVERAGE_REPORTS_DIR}/java/${projectName}/ || true
                                        fi
                                    """
                                }
                            } else if (allGradleFiles) {
                                echo "📊 Collecting Gradle coverage reports from ${allGradleFiles.size()} projects"
                                allGradleFiles.each { gradleFile ->
                                    def gradleDir = sh(script: "dirname '${gradleFile}'", returnStdout: true).trim()
                                    def projectName = sh(script: "basename '${gradleDir}'", returnStdout: true).trim()
                                    sh """
                                        if [ -d '${gradleDir}/build/reports/jacoco' ]; then
                                            mkdir -p ${COVERAGE_REPORTS_DIR}/java/${projectName}
                                            cp -r '${gradleDir}/build/reports/jacoco'/* ${COVERAGE_REPORTS_DIR}/java/${projectName}/ || true
                                        fi
                                    """
                                }
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
                        
                        // Java Coverage - find all jacoco.xml files recursively
                        def jacocoFiles = sh(
                            script: "find ${COVERAGE_REPORTS_DIR}/java -type f -name 'jacoco.xml' | head -10",
                            returnStdout: true
                        ).trim().split('\n').findAll { it.trim() }
                        
                        jacocoFiles.each { jacocoFile ->
                            if (fileExists(jacocoFile)) {
                                publishCoverage adapters: [jacocoAdapter(jacocoFile)],
                                    sourceFileResolver: sourceFiles('STORE_ALL_BUILD')
                            }
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