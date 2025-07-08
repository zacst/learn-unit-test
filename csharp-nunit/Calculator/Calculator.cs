using System;
using System.Collections.Generic;
using System.Linq;

namespace Calculator
{
    public class Calculator
    {
        public virtual int Add(int a, int b)
        {
            return a + b;
        }

        public virtual int Subtract(int a, int b)
        {
            return a - b;
        }

        public virtual int Multiply(int a, int b)
        {
            return a * b;
        }

        public virtual double Divide(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Division by zero is not allowed");
            }
            return (double)a / b;
        }

        public virtual bool IsEven(int number)
        {
            return number % 2 == 0;
        }

        public virtual bool IsPositive(int number)
        {
            return number > 0;
        }

        public virtual int Power(int baseNumber, int exponent)
        {
            if (exponent < 0)
            {
                throw new ArgumentException("Exponent cannot be negative");
            }
            return (int)Math.Pow(baseNumber, exponent);
        }

        public virtual double Average(IEnumerable<int> numbers)
        {
            if (numbers == null || !numbers.Any())
            {
                throw new ArgumentException("List cannot be null or empty");
            }
            return numbers.Average();
        }

        public virtual int Factorial(int n)
        {
            if (n < 0)
            {
                throw new ArgumentException("Factorial is not defined for negative numbers");
            }
            if (n == 0 || n == 1)
            {
                return 1;
            }
            return n * Factorial(n - 1);
        }
    }
}