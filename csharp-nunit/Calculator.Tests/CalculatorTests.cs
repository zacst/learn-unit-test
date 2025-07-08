using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Calculator;

namespace Calculator.Tests
{
    /// <summary>
    /// Comprehensive test suite demonstrating all major NUnit features
    /// </summary>
    [TestFixture]
    [Description("Tests for the Calculator class demonstrating NUnit features")]
    [Category("Calculator")]
    [Author("Test Author")]
    public class CalculatorTests
    {
        private Calculator calculator = null!;
        private List<int> testNumbers = null!;

        #region Setup and Teardown

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Runs once before all tests in the fixture
            Console.WriteLine("Setting up test fixture - runs once");
            testNumbers = new List<int> { 1, 2, 3, 4, 5 };
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            // Runs once after all tests in the fixture
            Console.WriteLine("Tearing down test fixture - runs once");
            testNumbers?.Clear();
        }

        [SetUp]
        public void SetUp()
        {
            // Runs before each test
            calculator = new Calculator();
            Console.WriteLine($"Setting up test: {TestContext.CurrentContext.Test.Name}");
        }

        [TearDown]
        public void TearDown()
        {
            // Runs after each test
            Console.WriteLine($"Tearing down test: {TestContext.CurrentContext.Test.Name}");
            // calculator will be garbage collected, no need to set to null
        }

        #endregion

        #region Basic Tests with Various Assertions

        [Test]
        [Description("Tests basic addition functionality")]
        [Category("Basic")]
        public void Add_TwoPositiveNumbers_ReturnsSum()
        {
            // Arrange
            int a = 5;
            int b = 3;
            int expected = 8;

            // Act
            int result = calculator.Add(a, b);

            // Assert - Multiple assertion styles
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(expected)); // Using constraint model instead of classic
            Assert.IsTrue(result == expected);
            Assert.That(result, Is.GreaterThan(0));
            Assert.That(result, Is.InRange(1, 10));
        }

        [Test]
        [Category("Basic")]
        public void Subtract_TwoNumbers_ReturnsDifference()
        {
            // Arrange & Act
            int result = calculator.Subtract(10, 4);

            // Assert
            Assert.That(result, Is.EqualTo(6));
            Assert.That(result, Is.Positive);
        }

        [Test]
        [Category("Basic")]
        public void Multiply_TwoNumbers_ReturnsProduct()
        {
            // Assert
            Assert.That(calculator.Multiply(4, 3), Is.EqualTo(12));
            Assert.That(calculator.Multiply(-2, 3), Is.EqualTo(-6));
            Assert.That(calculator.Multiply(0, 5), Is.EqualTo(0));
        }

        #endregion

        #region TestCase and TestCaseSource Examples

        [TestCase(10, 2, 5.0)]
        [TestCase(15, 3, 5.0)]
        [TestCase(7, 2, 3.5)]
        [TestCase(-10, 2, -5.0)]
        [Category("TestCase")]
        public void Divide_ValidInputs_ReturnsQuotient(int dividend, int divisor, double expected)
        {
            double result = calculator.Divide(dividend, divisor);
            Assert.That(result, Is.EqualTo(expected).Within(0.001));
        }

        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(0, true)]
        [TestCase(-4, true)]
        [TestCase(-3, false)]
        [Category("TestCase")]
        public void IsEven_VariousNumbers_ReturnsCorrectResult(int number, bool expected)
        {
            bool result = calculator.IsEven(number);
            Assert.That(result, Is.EqualTo(expected));
        }

        // TestCaseSource examples
        private static IEnumerable<TestCaseData> PowerTestCases()
        {
            yield return new TestCaseData(2, 3, 8).SetName("Power_2_to_3");
            yield return new TestCaseData(5, 2, 25).SetName("Power_5_to_2");
            yield return new TestCaseData(10, 0, 1).SetName("Power_10_to_0");
            yield return new TestCaseData(-2, 2, 4).SetName("Power_negative_2_to_2");
        }

        [TestCaseSource(nameof(PowerTestCases))]
        [Category("TestCaseSource")]
        public void Power_ValidInputs_ReturnsCorrectResult(int baseNumber, int exponent, int expected)
        {
            int result = calculator.Power(baseNumber, exponent);
            Assert.That(result, Is.EqualTo(expected));
        }

        private static readonly object[] FactorialTestCases = 
        {
            new object[] { 0, 1 },
            new object[] { 1, 1 },
            new object[] { 5, 120 },
            new object[] { 4, 24 }
        };

        [TestCaseSource(nameof(FactorialTestCases))]
        [Category("TestCaseSource")]
        public void Factorial_ValidInputs_ReturnsCorrectResult(int input, int expected)
        {
            int result = calculator.Factorial(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        #endregion

        #region Exception Testing

        [Test]
        [Category("Exceptions")]
        public void Divide_ByZero_ThrowsDivideByZeroException()
        {
            // Assert.Throws returns the exception for further inspection
            var exception = Assert.Throws<DivideByZeroException>(() => calculator.Divide(10, 0));
            Assert.That(exception.Message, Is.EqualTo("Division by zero is not allowed"));
        }

        [Test]
        [Category("Exceptions")]
        public void Power_NegativeExponent_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => calculator.Power(2, -1));
        }

        [Test]
        [Category("Exceptions")]
        public void Average_NullList_ThrowsArgumentException()
        {
            List<int>? nullList = null;
            Assert.Throws<ArgumentException>(() => calculator.Average(nullList!));
        }

        [Test]
        [Category("Exceptions")]
        public void Average_EmptyList_ThrowsArgumentException()
        {
            var emptyList = new List<int>();
            Assert.Throws<ArgumentException>(() => calculator.Average(emptyList));
        }

        [Test]
        [Category("Exceptions")]
        public void Factorial_NegativeNumber_ThrowsArgumentException()
        {
            Assert.That(() => calculator.Factorial(-1), Throws.ArgumentException);
        }

        #endregion

        #region Parameterized Tests

        [TestCase(1, ExpectedResult = true)]
        [TestCase(0, ExpectedResult = false)]
        [TestCase(-1, ExpectedResult = false)]
        [TestCase(10, ExpectedResult = true)]
        [Category("Parameterized")]
        public bool IsPositive_VariousNumbers_ReturnsExpectedResult(int number)
        {
            return calculator.IsPositive(number);
        }

        #endregion

        #region Collection and String Assertions

        [Test]
        [Category("Collections")]
        public void Average_ValidList_ReturnsCorrectAverage()
        {
            // Arrange
            var numbers = new List<int> { 2, 4, 6, 8, 10 };
            double expected = 6.0;

            // Act
            double result = calculator.Average(numbers);

            // Assert - Various collection and numeric assertions
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(result, Is.TypeOf<double>());
            Assert.That(numbers, Is.Not.Null);
            Assert.That(numbers, Has.Count.EqualTo(5));
            Assert.That(numbers, Contains.Item(6));
            Assert.That(numbers, Is.All.Positive);
            Assert.That(numbers, Is.Ordered);
        }

        #endregion

        #region Constraint-based Assertions

        [Test]
        [Category("Constraints")]
        public void Add_MultipleConstraints_DemonstratesConstraintModel()
        {
            int result = calculator.Add(5, 3);

            // Combining constraints
            Assert.That(result, Is.EqualTo(8).And.GreaterThan(0).And.LessThan(10));
            Assert.That(result, Is.EqualTo(8) | Is.EqualTo(9)); // Or constraint
            Assert.That(result, Is.Not.EqualTo(0)); // Changed from Is.Not.Null since int can't be null
            Assert.That(result, Is.AssignableTo<int>());
        }

        #endregion

        #region Conditional Tests

        [Test]
        [Category("Conditional")]
        [Platform("Win")]
        public void WindowsOnlyTest()
        {
            // This test only runs on Windows
            Assert.That(calculator.Add(1, 1), Is.EqualTo(2));
        }

        [Test]
        [Category("Conditional")]
        [Ignore("Temporarily disabled")]
        public void TemporarilyDisabledTest()
        {
            // This test is ignored
            Assert.Fail("This test should not run");
        }

        #endregion

        #region Test Context and Properties

        [Test]
        [Property("Priority", "High")]
        [Property("Owner", "TestTeam")]
        [Category("Properties")]
        public void TestWithProperties()
        {
            // Access test context
            string testName = TestContext.CurrentContext.Test.Name;
            Console.WriteLine($"Running test: {testName}");
            
            // Test properties can be accessed
            Assert.That(TestContext.CurrentContext.Test.Properties["Priority"].Contains("High"));
            
            int result = calculator.Add(2, 3);
            Assert.That(result, Is.EqualTo(5));
        }

        #endregion

        #region Retry and Timeout

        [Test]
        [Retry(3)]
        [Category("Retry")]
        public void TestWithRetry()
        {
            // This test will retry up to 3 times if it fails
            int result = calculator.Multiply(2, 3);
            Assert.That(result, Is.EqualTo(6));
        }

        [Test]
        [Timeout(5000)]
        [Category("Timeout")]
        public void TestWithTimeout()
        {
            // This test must complete within 5 seconds
            System.Threading.Thread.Sleep(100); // Simulate some work
            int result = calculator.Add(1, 1);
            Assert.That(result, Is.EqualTo(2));
        }

        #endregion

        #region Multiple Asserts

        [Test]
        [Category("MultipleAsserts")]
        public void MultipleAsserts_UsingAssertMultiple()
        {
            int addResult = calculator.Add(2, 3);
            int subtractResult = calculator.Subtract(5, 2);
            int multiplyResult = calculator.Multiply(3, 4);

            Assert.Multiple(() =>
            {
                Assert.That(addResult, Is.EqualTo(5), "Addition failed");
                Assert.That(subtractResult, Is.EqualTo(3), "Subtraction failed");
                Assert.That(multiplyResult, Is.EqualTo(12), "Multiplication failed");
                Assert.That(calculator.IsEven(addResult), Is.False, "IsEven check failed");
                Assert.That(calculator.IsPositive(subtractResult), Is.True, "IsPositive check failed");
            });
        }

        #endregion

        #region Range and Random Tests

        [Test]
        [Category("Range")]
        public void Add_RandomValues_WithinExpectedRange([Random(1, 10, 5)] int a, [Random(1, 10, 5)] int b)
        {
            int result = calculator.Add(a, b);
            Assert.That(result, Is.InRange(2, 20));
            Assert.That(result, Is.EqualTo(a + b));
        }

        [Test]
        [Category("Range")]
        public void Multiply_RangeOfValues_ReturnsCorrectResults([Range(1, 5)] int a, [Range(1, 3)] int b)
        {
            int result = calculator.Multiply(a, b);
            Assert.That(result, Is.EqualTo(a * b));
            Assert.That(result, Is.Positive);
        }

        #endregion

        #region Theory Tests (Combinatorial)

        [Test]
        [Category("Theory")]
        public void Add_CombinatorialTest([Values(1, 2, 3)] int a, [Values(4, 5)] int b)
        {
            // This creates 6 test cases (3 * 2 combinations)
            int result = calculator.Add(a, b);
            Assert.That(result, Is.EqualTo(a + b));
            Assert.That(result, Is.GreaterThan(a));
            Assert.That(result, Is.GreaterThan(b));
        }

        #endregion

        #region Custom Assertions

        [Test]
        [Category("Custom")]
        public void CustomAssertions_Example()
        {
            int result = calculator.Add(5, 5);
            
            // Custom assertion method
            AssertIsEvenAndPositive(result);
            
            // Using Is.EqualTo with custom message
            Assert.That(result, Is.EqualTo(10), "The sum of 5 and 5 should be 10");
        }

        private void AssertIsEvenAndPositive(int number)
        {
            Assert.That(calculator.IsEven(number), Is.True, $"Number {number} should be even");
            Assert.That(calculator.IsPositive(number), Is.True, $"Number {number} should be positive");
        }

        #endregion

        #region Performance Tests

        [Test]
        [Category("Performance")]
        [Explicit("Long running test")]
        public void Factorial_LargeNumber_PerformanceTest()
        {
            // Explicit tests must be run intentionally
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            int result = calculator.Factorial(10);
            
            stopwatch.Stop();
            
            Assert.That(result, Is.EqualTo(3628800));
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), "Factorial should complete quickly");
        }

        #endregion
    }

    #region Nested Test Classes

    [TestFixture]
    [Category("Nested")]
    public class NestedCalculatorTests
    {
        [TestFixture]
        [Category("ArithmeticOperations")]
        public class ArithmeticOperationsTests
        {
            private Calculator calculator = null!;

            [SetUp]
            public void SetUp()
            {
                calculator = new Calculator();
            }

            [Test]
            public void Add_BasicTest()
            {
                Assert.That(calculator.Add(1, 1), Is.EqualTo(2));
            }

            [Test]
            public void Subtract_BasicTest()
            {
                Assert.That(calculator.Subtract(3, 1), Is.EqualTo(2));
            }
        }

        [TestFixture]
        [Category("UtilityMethods")]
        public class UtilityMethodsTests
        {
            private Calculator calculator = null!;

            [SetUp]
            public void SetUp()
            {
                calculator = new Calculator();
            }

            [Test]
            public void IsEven_BasicTest()
            {
                Assert.That(calculator.IsEven(2), Is.True);
            }

            [Test]
            public void IsPositive_BasicTest()
            {
                Assert.That(calculator.IsPositive(1), Is.True);
            }
        }
    }

    #endregion
}