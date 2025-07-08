using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Calculator.Tests
{
    // Test collection for grouping related tests
    [Collection("Calculator Tests")]
    public class CalculatorTests : IDisposable
    {
        private readonly Calculator _calculator;
        private readonly ITestOutputHelper _output;

        // Constructor - runs before each test (similar to SetUp)
        public CalculatorTests(ITestOutputHelper output)
        {
            _calculator = new Calculator();
            _output = output;
            _output.WriteLine("Test initialized");
        }

        // Dispose - runs after each test (similar to TearDown)
        public void Dispose()
        {
            _output.WriteLine("Test completed");
        }

        #region Basic Facts Tests
        [Fact]
        public void Add_TwoPositiveNumbers_ReturnsSum()
        {
            // Arrange
            int a = 5, b = 3;
            
            // Act
            int result = _calculator.Add(a, b);
            
            // Assert
            Assert.Equal(8, result);
        }

        [Fact]
        public void Subtract_TwoNumbers_ReturnsDifference()
        {
            // Arrange & Act
            int result = _calculator.Subtract(10, 4);
            
            // Assert
            Assert.Equal(6, result);
        }

        [Fact]
        public void Multiply_TwoNumbers_ReturnsProduct()
        {
            Assert.Equal(15, _calculator.Multiply(3, 5));
        }
        #endregion

        #region Theory Tests with InlineData
        [Theory]
        [InlineData(10, 2, 5.0)]
        [InlineData(15, 3, 5.0)]
        [InlineData(7, 2, 3.5)]
        [InlineData(-10, 2, -5.0)]
        public void Divide_ValidNumbers_ReturnsCorrectQuotient(int dividend, int divisor, double expected)
        {
            // Act
            double result = _calculator.Divide(dividend, divisor);
            
            // Assert
            Assert.Equal(expected, result, precision: 1);
        }

        [Theory]
        [InlineData(2, true)]
        [InlineData(4, true)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(3, false)]
        [InlineData(-2, true)]
        [InlineData(-1, false)]
        public void IsEven_VariousNumbers_ReturnsExpectedResult(int number, bool expected)
        {
            // Act
            bool result = _calculator.IsEven(number);
            
            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(10, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(-10, false)]
        public void IsPositive_VariousNumbers_ReturnsExpectedResult(int number, bool expected)
        {
            Assert.Equal(expected, _calculator.IsPositive(number));
        }
        #endregion

        #region Theory Tests with MemberData
        public static IEnumerable<object[]> PowerTestData()
        {
            yield return new object[] { 2, 3, 8 };
            yield return new object[] { 5, 2, 25 };
            yield return new object[] { 3, 0, 1 };
            yield return new object[] { 10, 1, 10 };
            yield return new object[] { 0, 5, 0 };
        }

        [Theory]
        [MemberData(nameof(PowerTestData))]
        public void Power_ValidInputs_ReturnsCorrectResult(int baseNumber, int exponent, int expected)
        {
            // Act
            int result = _calculator.Power(baseNumber, exponent);
            
            // Assert
            Assert.Equal(expected, result);
        }

        public static IEnumerable<object[]> AverageTestData()
        {
            yield return new object[] { new int[] { 1, 2, 3, 4, 5 }, 3.0 };
            yield return new object[] { new int[] { 10, 20, 30 }, 20.0 };
            yield return new object[] { new int[] { 5 }, 5.0 };
            yield return new object[] { new int[] { -5, -10, -15 }, -10.0 };
        }

        [Theory]
        [MemberData(nameof(AverageTestData))]
        public void Average_ValidLists_ReturnsCorrectAverage(int[] numbers, double expected)
        {
            // Act
            double result = _calculator.Average(numbers);
            
            // Assert
            Assert.Equal(expected, result, precision: 1);
        }
        #endregion

        #region Theory Tests with ClassData
        public class FactorialTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { 0, 1 };
                yield return new object[] { 1, 1 };
                yield return new object[] { 2, 2 };
                yield return new object[] { 3, 6 };
                yield return new object[] { 4, 24 };
                yield return new object[] { 5, 120 };
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
                => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FactorialTestData))]
        public void Factorial_ValidNumbers_ReturnsCorrectFactorial(int n, int expected)
        {
            // Act
            int result = _calculator.Factorial(n);
            
            // Assert
            Assert.Equal(expected, result);
        }
        #endregion

        #region Exception Testing
        [Fact]
        public void Divide_ByZero_ThrowsDivideByZeroException()
        {
            // Act & Assert
            var exception = Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
            Assert.Equal("Division by zero is not allowed", exception.Message);
        }

        [Fact]
        public void Power_NegativeExponent_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _calculator.Power(2, -1));
            Assert.Equal("Exponent cannot be negative", exception.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new int[] { })]
        public void Average_NullOrEmptyList_ThrowsArgumentException(int[] numbers)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => _calculator.Average(numbers));
            Assert.Equal("List cannot be null or empty", exception.Message);
        }

        [Fact]
        public void Factorial_NegativeNumber_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _calculator.Factorial(-1));
        }
        #endregion

        #region Advanced Assertions
        [Fact]
        public void Divide_ResultsInDecimal_AssertWithPrecision()
        {
            // Act
            double result = _calculator.Divide(10, 3);
            
            // Assert - Multiple assertion types
            Assert.True(result > 3.0);
            Assert.True(result < 4.0);
            Assert.InRange(result, 3.0, 4.0);
            Assert.Equal(3.333, result, precision: 3);
        }

        [Fact]
        public void Add_MultipleAssertions_AllMustPass()
        {
            // Act
            int result = _calculator.Add(5, 3);
            
            // Assert - Multiple assertions
            Assert.Equal(8, result);
            Assert.True(result > 0);
            Assert.True(result.GetType() == typeof(int));
            Assert.NotEqual(0, result);
        }

        [Fact]
        public void Average_ListOfNumbers_ReturnsValidDouble()
        {
            // Arrange
            var numbers = new int[] { 1, 2, 3, 4, 5 };
            
            // Act
            double result = _calculator.Average(numbers);
            
            // Assert
            Assert.IsType<double>(result);
            Assert.False(double.IsNaN(result));
            Assert.False(double.IsInfinity(result));
        }
        #endregion

        #region Skip Tests
        [Fact(Skip = "Performance test - skip for regular runs")]
        public void Factorial_LargeNumber_PerformanceTest()
        {
            // This test is skipped
            _calculator.Factorial(10);
        }

        [Theory(Skip = "Integration test - requires external dependency")]
        [InlineData(1, 1)]
        public void IntegrationTest_Skipped(int a, int b)
        {
            // This test is skipped
            _calculator.Add(a, b);
        }
        #endregion

        #region Trait-based Tests (for test categorization)
        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Priority", "High")]
        public void Add_CategorizedTest_ReturnsSum()
        {
            Assert.Equal(7, _calculator.Add(3, 4));
        }

        [Fact]
        [Trait("Category", "Integration")]
        [Trait("Priority", "Medium")]
        public void Calculator_IntegrationTest_WorksCorrectly()
        {
            // Test multiple operations together
            int addResult = _calculator.Add(5, 3);
            int multiplyResult = _calculator.Multiply(addResult, 2);
            
            Assert.Equal(16, multiplyResult);
        }

        [Fact]
        [Trait("Category", "Performance")]
        [Trait("Priority", "Low")]
        public void Factorial_PerformanceTest_CompletesQuickly()
        {
            // Measure performance (simplified example)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _calculator.Factorial(5);
            
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 100);
        }
        #endregion

        #region Custom Test Output
        [Fact]
        public void Add_WithCustomOutput_DisplaysDebugInfo()
        {
            // Arrange
            int a = 10, b = 5;
            _output.WriteLine($"Testing Add with values: {a} and {b}");
            
            // Act
            int result = _calculator.Add(a, b);
            _output.WriteLine($"Result: {result}");
            
            // Assert
            Assert.Equal(15, result);
            _output.WriteLine("Test passed successfully!");
        }
        #endregion

        #region Conditional Tests
        [ConditionalFact]
        public void ConditionalTest_RunsOnlyOnWindows()
        {
            // This test only runs on Windows
            Assert.True(Environment.OSVersion.Platform == PlatformID.Win32NT);
        }

        // Custom conditional attribute would be defined elsewhere
        [Fact]
        public void Calculator_CrossPlatformTest_WorksEverywhere()
        {
            // Test that works on all platforms
            Assert.Equal(10, _calculator.Add(6, 4));
        }
        #endregion
    }

    // Custom conditional fact attribute
    public sealed class ConditionalFactAttribute : FactAttribute
    {
        public ConditionalFactAttribute()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Skip = "Only runs on Windows";
            }
        }
    }

    // Test collection definition for shared context
    [CollectionDefinition("Calculator Tests")]
    public class CalculatorTestCollection : ICollectionFixture<CalculatorTestFixture>
    {
        // This class has no code, and is never instantiated.
        // Its purpose is to be the place where collection definition attributes are applied.
    }

    // Shared test fixture
    public class CalculatorTestFixture : IDisposable
    {
        public Calculator Calculator { get; }

        public CalculatorTestFixture()
        {
            Calculator = new Calculator();
            // Setup shared resources
        }

        public void Dispose()
        {
            // Cleanup shared resources
        }
    }

    // Example of testing with shared fixture
    public class SharedCalculatorTests : IClassFixture<CalculatorTestFixture>
    {
        private readonly CalculatorTestFixture _fixture;

        public SharedCalculatorTests(CalculatorTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void SharedFixture_Add_ReturnsSum()
        {
            Assert.Equal(8, _fixture.Calculator.Add(3, 5));
        }
    }
}