package com.example;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.condition.*;
import org.junit.jupiter.api.extension.ExtensionContext;
import org.junit.jupiter.api.io.TempDir;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.*;
import org.junit.jupiter.params.converter.ConvertWith;
import org.junit.jupiter.params.converter.SimpleArgumentConverter;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.TimeUnit;
import java.util.stream.Stream;

import static org.junit.jupiter.api.Assertions.*;
import static org.junit.jupiter.api.Assumptions.*;
import static org.junit.jupiter.api.DynamicTest.dynamicTest;

/**
 * Comprehensive JUnit 5 test suite demonstrating all major features
 */
@DisplayName("🧮 Calculator Test Suite")
@TestMethodOrder(MethodOrderer.OrderAnnotation.class)
@TestInstance(TestInstance.Lifecycle.PER_CLASS)
public class CalculatorTest {

    private Calculator calculator;
    private static final String OS_NAME = System.getProperty("os.name");
    
    // Test instance per class (shared across test methods)
    private List<String> testExecutionOrder = new ArrayList<>();

    @BeforeAll
    void globalSetup() {
        System.out.println("🚀 Starting Calculator Test Suite");
        testExecutionOrder.clear();
    }

    @BeforeEach
    void setUp(TestInfo testInfo) {
        calculator = new Calculator();
        testExecutionOrder.add(testInfo.getDisplayName());
        System.out.println("📝 Executing: " + testInfo.getDisplayName());
    }

    @AfterEach
    void tearDown() {
        calculator = null;
    }

    @AfterAll
    void globalTeardown() {
        System.out.println("✅ Completed Calculator Test Suite");
        System.out.println("📊 Test execution order: " + testExecutionOrder);
    }

    // =================
    // BASIC TESTS
    // =================

    @Test
    @Order(1)
    @DisplayName("Basic arithmetic operations")
    @Tag("basic")
    void testBasicOperations() {
        assertAll("Basic operations",
            () -> assertEquals(8, calculator.add(5, 3), "Addition failed"),
            () -> assertEquals(2, calculator.subtract(5, 3), "Subtraction failed"),
            () -> assertEquals(15, calculator.multiply(5, 3), "Multiplication failed"),
            () -> assertEquals(2.5, calculator.divide(5, 2), 0.001, "Division failed")
        );
    }

    @Test
    @Order(2)
    @DisplayName("Boolean operations")
    @Tag("basic")
    void testBooleanOperations() {
        assertAll("Boolean operations",
            () -> assertTrue(calculator.isEven(4), "4 should be even"),
            () -> assertFalse(calculator.isEven(5), "5 should be odd"),
            () -> assertTrue(calculator.isPositive(42), "42 should be positive"),
            () -> assertFalse(calculator.isPositive(-5), "-5 should not be positive"),
            () -> assertFalse(calculator.isPositive(0), "0 should not be positive")
        );
    }

    // =================
    // CONDITIONAL TESTS
    // =================

    @Test
    @EnabledOnOs(OS.WINDOWS)
    @DisplayName("Windows-specific calculation")
    void testWindowsSpecificFeature() {
        // This test only runs on Windows
        assertTrue(calculator.isPositive(42));
    }

    @Test
    @EnabledOnOs({OS.LINUX, OS.MAC})
    @DisplayName("Unix-based system calculation")
    void testUnixSpecificFeature() {
        // This test only runs on Linux or Mac
        assertTrue(calculator.isEven(4));
    }

    @Test
    @EnabledOnJre(JRE.JAVA_11)
    @DisplayName("Java 11 specific feature")
    void testJava11Feature() {
        assertNotNull(calculator);
    }

    @Test
    @EnabledIfSystemProperty(named = "java.version", matches = ".*11.*")
    @DisplayName("System property based test")
    void testBasedOnSystemProperty() {
        assertTrue(calculator.isPositive(1));
    }

    @Test
    @EnabledIfEnvironmentVariable(named = "PATH", matches = ".*")
    @DisplayName("Environment variable based test")
    void testBasedOnEnvironmentVariable() {
        assertNotNull(System.getenv("PATH"));
    }

    @Test
    @DisabledOnOs(OS.SOLARIS)
    @DisplayName("Disabled on Solaris")
    void testDisabledOnSolaris() {
        assertEquals(9, calculator.power(3, 2));
    }

    @Test
    @DisabledIf("java.awt.GraphicsEnvironment#isHeadless")
    @DisplayName("Disabled if headless environment")
    void testDisabledIfHeadless() {
        assertEquals(16, calculator.power(4, 2));
    }

    @Test
    @EnabledIf("'true'.equals(systemProperty.get('java.specification.version'))")
    @DisplayName("Enabled if Java specification version exists")
    void testEnabledIfJavaSpec() {
        assertTrue(calculator.isPositive(100));
    }

    // =================
    // ASSUMPTIONS
    // =================

    @Test
    @DisplayName("Tests with assumptions")
    void testWithAssumptions() {
        assumeTrue(OS_NAME.contains("Windows") || OS_NAME.contains("Mac") || OS_NAME.contains("Linux"));
        assumeFalse(System.getProperty("java.version").contains("1.8"));
        
        // This test only runs if assumptions are met
        assertEquals(25, calculator.power(5, 2));
    }

    @Test
    @DisplayName("Assumption with custom message")
    void testAssumptionWithMessage() {
        assumingThat(Runtime.getRuntime().maxMemory() > 100 * 1024 * 1024, () -> {
            // This code only runs if we have more than 100MB max memory
            assertEquals(120, calculator.factorial(5));
        });
    }

    @Test
    @DisplayName("Assumption with lambda")
    void testAssumptionWithLambda() {
        assumingThat(() -> !System.getProperty("os.name").isEmpty(), 
            () -> {
                assertEquals(8, calculator.add(3, 5));
            });
    }

    // =================
    // EXCEPTION TESTING
    // =================

    @Test
    @DisplayName("Exception testing with custom message verification")
    void testExceptionWithMessage() {
        ArithmeticException exception = assertThrows(ArithmeticException.class, () -> {
            calculator.divide(10, 0);
        });
        
        assertAll("Exception properties",
            () -> assertEquals("Division by zero is not allowed", exception.getMessage()),
            () -> assertNotNull(exception.getClass())
        );
    }

    @Test
    @DisplayName("Multiple exception scenarios")
    void testMultipleExceptions() {
        assertAll("Multiple exception scenarios",
            () -> assertThrows(ArithmeticException.class, () -> calculator.divide(1, 0)),
            () -> assertThrows(IllegalArgumentException.class, () -> calculator.factorial(-1)),
            () -> assertThrows(IllegalArgumentException.class, () -> calculator.power(2, -1)),
            () -> assertThrows(IllegalArgumentException.class, () -> calculator.average(null)),
            () -> assertThrows(IllegalArgumentException.class, () -> calculator.average(new ArrayList<>()))
        );
    }

    @Test
    @DisplayName("Exception does not throw")
    void testExceptionDoesNotThrow() {
        assertDoesNotThrow(() -> calculator.divide(10, 2));
        assertDoesNotThrow(() -> calculator.factorial(5));
        assertDoesNotThrow(() -> calculator.power(2, 3));
    }

    // =================
    // TIMEOUT TESTING
    // =================

    @Test
    @DisplayName("Timeout testing - should complete quickly")
    @Timeout(value = 2, unit = TimeUnit.SECONDS)
    void testTimeout() {
        // This test will fail if it takes more than 2 seconds
        assertEquals(1000000, calculator.power(10, 6));
    }

    @Test
    @DisplayName("Assertion timeout testing")
    void testAssertionTimeout() {
        assertTimeout(Duration.ofSeconds(1), () -> {
            return calculator.factorial(10);
        });
    }

    @Test
    @DisplayName("Preemptive timeout testing")
    void testPreemptiveTimeout() {
        assertTimeoutPreemptively(Duration.ofMillis(500), () -> {
            return calculator.add(1, 2);
        });
    }

    @Test
    @DisplayName("Timeout with result")
    void testTimeoutWithResult() {
        String result = assertTimeout(Duration.ofSeconds(1), () -> {
            calculator.multiply(5, 6);
            return "Test completed";
        });
        assertEquals("Test completed", result);
    }

    // =================
    // PARAMETERIZED TESTS
    // =================

    @ParameterizedTest
    @DisplayName("Value source parameterized test")
    @ValueSource(ints = {1, 3, 5, 7, 9, 11, 13})
    void testOddNumbers(int number) {
        assertFalse(calculator.isEven(number), number + " should be odd");
    }

    @ParameterizedTest
    @DisplayName("Value source with doubles")
    @ValueSource(doubles = {1.5, 2.5, 3.5})
    void testDoubleValues(double value) {
        assertTrue(value > 0);
    }

    @ParameterizedTest
    @DisplayName("Value source with strings")
    @ValueSource(strings = {"1", "2", "3"})
    void testStringValues(String value) {
        assertTrue(Integer.parseInt(value) > 0);
    }

    @ParameterizedTest
    @DisplayName("Enum source parameterized test")
    @EnumSource(value = TestEnum.class, names = {"POSITIVE", "ZERO"})
    void testEnumValues(TestEnum testEnum) {
        assertTrue(testEnum.getValue() >= 0);
    }

    @ParameterizedTest
    @DisplayName("Enum source with mode")
    @EnumSource(value = TestEnum.class, mode = EnumSource.Mode.EXCLUDE, names = {"NEGATIVE"})
    void testEnumValuesWithMode(TestEnum testEnum) {
        assertTrue(testEnum.getValue() >= 0);
    }

    @ParameterizedTest
    @DisplayName("CSV source parameterized test")
    @CsvSource({
        "1, 1, 1",
        "2, 2, 4", 
        "3, 3, 9",
        "4, 4, 16",
        "5, 5, 25"
    })
    void testSquareNumbers(int base, int exponent, int expected) {
        assertEquals(expected, calculator.power(base, exponent));
    }

    @ParameterizedTest
    @DisplayName("CSV source with custom delimiter")
    @CsvSource(value = {
        "10|2|5.0",
        "20|4|5.0",
        "30|6|5.0"
    }, delimiter = '|')
    void testDivisionWithCustomDelimiter(int dividend, int divisor, double expected) {
        assertEquals(expected, calculator.divide(dividend, divisor), 0.001);
    }

    // CSV file source test removed as it requires external test-data.csv file
    // @ParameterizedTest
    // @DisplayName("CSV file source parameterized test")
    // @CsvFileSource(resources = "/test-data.csv", numLinesToSkip = 1)
    // void testWithCsvFile(int a, int b, int expected) {
    //     assertEquals(expected, calculator.add(a, b));
    // }

    @ParameterizedTest
    @DisplayName("Method source parameterized test")
    @MethodSource("provideNumbersForTesting")
    void testWithMethodSource(int number, boolean expectedEven) {
        assertEquals(expectedEven, calculator.isEven(number));
    }

    private static Stream<Arguments> provideNumbersForTesting() {
        return Stream.of(
            Arguments.of(2, true),
            Arguments.of(3, false),
            Arguments.of(4, true),
            Arguments.of(5, false)
        );
    }

    @ParameterizedTest
    @DisplayName("Method source for factorial")
    @MethodSource("provideFactorialData")
    void testFactorialWithMethodSource(int input, int expected) {
        assertEquals(expected, calculator.factorial(input));
    }

    private static Stream<Arguments> provideFactorialData() {
        return Stream.of(
            Arguments.of(0, 1),
            Arguments.of(1, 1),
            Arguments.of(2, 2),
            Arguments.of(3, 6),
            Arguments.of(4, 24),
            Arguments.of(5, 120)
        );
    }

    @ParameterizedTest
    @DisplayName("Arguments source parameterized test")
    @ArgumentsSource(CustomArgumentsProvider.class)
    void testWithArgumentsSource(String input, int expected) {
        assertEquals(expected, Integer.parseInt(input));
    }

    @ParameterizedTest
    @DisplayName("Custom converter parameterized test")
    @ValueSource(strings = {"1", "2", "3"})
    void testWithCustomConverter(@ConvertWith(StringToIntegerConverter.class) Integer number) {
        assertTrue(number > 0);
    }

    @ParameterizedTest
    @DisplayName("Null and empty source test")
    @NullAndEmptySource
    @ValueSource(strings = {" ", "   ", "\t", "\n"})
    void testNullAndEmpty(String input) {
        assertTrue(input == null || input.trim().isEmpty());
    }

    @ParameterizedTest
    @DisplayName("Null source test")
    @NullSource
    void testNullSource(String input) {
        assertNull(input);
    }

    @ParameterizedTest
    @DisplayName("Empty source test")
    @EmptySource
    void testEmptySource(String input) {
        assertTrue(input.isEmpty());
    }

    // =================
    // DYNAMIC TESTS
    // =================

    @TestFactory
    @DisplayName("Dynamic tests for factorial")
    Collection<DynamicTest> dynamicFactorialTests() {
        return Arrays.asList(
            dynamicTest("factorial of 0", () -> assertEquals(1, calculator.factorial(0))),
            dynamicTest("factorial of 1", () -> assertEquals(1, calculator.factorial(1))),
            dynamicTest("factorial of 2", () -> assertEquals(2, calculator.factorial(2))),
            dynamicTest("factorial of 3", () -> assertEquals(6, calculator.factorial(3))),
            dynamicTest("factorial of 4", () -> assertEquals(24, calculator.factorial(4)))
        );
    }

    @TestFactory
    @DisplayName("Dynamic tests from stream")
    Stream<DynamicTest> dynamicTestsFromStream() {
        return Stream.of(2, 4, 6, 8, 10)
            .map(number -> dynamicTest("Testing even number: " + number, 
                () -> assertTrue(calculator.isEven(number))));
    }

    @TestFactory
    @DisplayName("Dynamic tests with complex logic")
    Stream<DynamicTest> dynamicTestsWithComplexLogic() {
        List<Integer> numbers = Arrays.asList(1, 2, 3, 4, 5);
        
        return numbers.stream()
            .map(number -> dynamicTest("Testing number: " + number, () -> {
                if (number % 2 == 0) {
                    assertTrue(calculator.isEven(number));
                } else {
                    assertFalse(calculator.isEven(number));
                }
                assertTrue(calculator.isPositive(number));
            }));
    }

    @TestFactory
    @DisplayName("Dynamic tests for average calculation")
    Stream<DynamicTest> dynamicAverageTests() {
        return Stream.of(
            Arrays.asList(1, 2, 3),
            Arrays.asList(10, 20, 30),
            Arrays.asList(5, 15, 25, 35)
        ).map(numbers -> dynamicTest("Testing average of: " + numbers, () -> {
            double expected = numbers.stream().mapToInt(Integer::intValue).average().orElse(0.0);
            assertEquals(expected, calculator.average(numbers), 0.001);
        }));
    }

    // =================
    // NESTED TESTS
    // =================

    @Nested
    @DisplayName("Advanced mathematical operations")
    class AdvancedMathOperations {
        
        @Test
        @DisplayName("Complex number operations")
        void testComplexOperations() {
            int result = calculator.add(
                calculator.multiply(2, calculator.power(3, 2)),
                calculator.subtract(20, calculator.factorial(3))
            );
            assertEquals(32, result); // 2 * 9 + (20 - 6) = 18 + 14 = 32
        }

        @Test
        @DisplayName("Average calculation")
        void testAverageCalculation() {
            List<Integer> numbers = Arrays.asList(1, 2, 3, 4, 5);
            assertEquals(3.0, calculator.average(numbers), 0.001);
        }

        @Nested
        @DisplayName("Edge cases")
        class EdgeCases {
            
            @Test
            @DisplayName("Very large numbers")
            void testLargeNumbers() {
                assertEquals(1000000, calculator.power(10, 6));
            }

            @Test
            @DisplayName("Zero operations")
            void testZeroOperations() {
                assertAll("Zero operations",
                    () -> assertEquals(0, calculator.multiply(0, 999)),
                    () -> assertEquals(5, calculator.add(0, 5)),
                    () -> assertEquals(-5, calculator.subtract(0, 5)),
                    () -> assertEquals(1, calculator.factorial(0))
                );
            }

            @Test
            @DisplayName("Negative number handling")
            void testNegativeNumbers() {
                assertAll("Negative number operations",
                    () -> assertEquals(-5, calculator.add(-2, -3)),
                    () -> assertEquals(1, calculator.subtract(-2, -3)),
                    () -> assertEquals(6, calculator.multiply(-2, -3)),
                    () -> assertFalse(calculator.isPositive(-5))
                );
            }
        }

        @Nested
        @DisplayName("Power operations")
        class PowerOperations {
            
            @Test
            @DisplayName("Power with zero exponent")
            void testPowerZeroExponent() {
                assertEquals(1, calculator.power(5, 0));
            }

            @Test
            @DisplayName("Power with one exponent")
            void testPowerOneExponent() {
                assertEquals(7, calculator.power(7, 1));
            }
        }
    }

    @Nested
    @DisplayName("List operations")
    class ListOperations {
        
        @Test
        @DisplayName("Average of single element")
        void testAverageSingleElement() {
            assertEquals(5.0, calculator.average(Arrays.asList(5)), 0.001);
        }

        @Test
        @DisplayName("Average of mixed positive and negative")
        void testAverageMixed() {
            List<Integer> numbers = Arrays.asList(-10, 0, 10);
            assertEquals(0.0, calculator.average(numbers), 0.001);
        }
    }

    // =================
    // TEMPORARY DIRECTORY TESTS
    // =================

    @Test
    @DisplayName("Temporary directory test")
    void testWithTempDir(@TempDir Path tempDir) throws IOException {
        Path testFile = tempDir.resolve("test.txt");
        Files.writeString(testFile, "Hello JUnit 5");
        
        assertTrue(Files.exists(testFile));
        assertEquals("Hello JUnit 5", Files.readString(testFile));
    }

    @Test
    @DisplayName("Temporary directory with calculations")
    void testCalculationsWithTempDir(@TempDir Path tempDir) throws IOException {
        Path resultFile = tempDir.resolve("results.txt");
        
        int result = calculator.add(calculator.multiply(5, 3), calculator.power(2, 3));
        Files.writeString(resultFile, "Result: " + result);
        
        assertTrue(Files.exists(resultFile));
        assertEquals("Result: 23", Files.readString(resultFile));
    }

    // =================
    // REPEATED TESTS
    // =================

    @RepeatedTest(value = 5, name = "Repetition {currentRepetition} of {totalRepetitions}")
    @DisplayName("Repeated test for random operations")
    void testRepeated(RepetitionInfo repetitionInfo) {
        int randomA = (int) (Math.random() * 100);
        int randomB = (int) (Math.random() * 100);
        
        int result = calculator.add(randomA, randomB);
        assertEquals(randomA + randomB, result);
        
        System.out.println("Repetition " + repetitionInfo.getCurrentRepetition() + 
                         " of " + repetitionInfo.getTotalRepetitions());
    }

    @RepeatedTest(3)
    @DisplayName("Repeated factorial test")
    void testRepeatedFactorial() {
        assertEquals(24, calculator.factorial(4));
    }

    @RepeatedTest(value = 4, name = "Division test {currentRepetition}")
    void testRepeatedDivision() {
        assertEquals(2.5, calculator.divide(5, 2), 0.001);
    }

    // =================
    // CUSTOM EXTENSIONS AND HELPERS
    // =================

    @Test
    @DisplayName("Test info injection")
    void testWithTestInfo(TestInfo testInfo) {
        assertEquals("testWithTestInfo(TestInfo)", testInfo.getDisplayName());
        assertTrue(testInfo.getTags().isEmpty());
        assertEquals(CalculatorTest.class, testInfo.getTestClass().get());
        assertEquals("testWithTestInfo", testInfo.getTestMethod().get().getName());
    }

    @Test
    @DisplayName("Test reporter injection")
    void testWithTestReporter(TestReporter testReporter) {
        Map<String, String> values = new HashMap<>();
        values.put("user", System.getProperty("user.name"));
        values.put("os", System.getProperty("os.name"));
        values.put("java-version", System.getProperty("java.version"));
        
        testReporter.publishEntry(values);
        testReporter.publishEntry("Custom message", "This is a test report entry");
        testReporter.publishEntry("calculation-result", String.valueOf(calculator.add(5, 3)));
        
        assertTrue(true); // Placeholder assertion
    }

    @Test
    @DisplayName("Combined test info and reporter")
    void testWithBothInjections(TestInfo testInfo, TestReporter testReporter) {
        testReporter.publishEntry("test-name", testInfo.getDisplayName());
        testReporter.publishEntry("test-tags", testInfo.getTags().toString());
        
        assertEquals(8, calculator.add(3, 5));
    }

    // =================
    // TAGS AND GROUPING
    // =================

    @Test
    @Tag("fast")
    @Tag("unit")
    @DisplayName("Fast unit test")
    void testFastUnit() {
        assertEquals(10, calculator.add(4, 6));
    }

    @Test
    @Tag("slow")
    @Tag("integration")
    @DisplayName("Slow integration test")
    void testSlowIntegration() {
        // Simulate slow test
        int result = 0;
        for (int i = 0; i < 1000; i++) {
            result = calculator.add(result, 1);
        }
        assertEquals(1000, result);
    }

    @Test
    @Tag("math")
    @Tag("advanced")
    @DisplayName("Advanced math test")
    void testAdvancedMath() {
        int result = calculator.factorial(5);
        assertEquals(120, result);
    }

    // =================
    // SUPPORTING CLASSES AND ENUMS
    // =================

    enum TestEnum {
        POSITIVE(1),
        ZERO(0),
        NEGATIVE(-1);
        
        private final int value;
        
        TestEnum(int value) {
            this.value = value;
        }
        
        public int getValue() {
            return value;
        }
    }

    static class CustomArgumentsProvider implements ArgumentsProvider {
        @Override
        public Stream<? extends Arguments> provideArguments(ExtensionContext context) {
            return Stream.of(
                Arguments.of("10", 10),
                Arguments.of("20", 20),
                Arguments.of("30", 30)
            );
        }
    }

    static class StringToIntegerConverter extends SimpleArgumentConverter {
        @Override
        protected Object convert(Object source, Class<?> targetType) {
            assertEquals(String.class, source.getClass());
            return Integer.valueOf((String) source);
        }
    }
}