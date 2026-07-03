using GhPrRules.Calculator;
using Xunit;

namespace GhPrRules.Calculator.Tests;

public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    public void Add_ReturnsSum(int a, int b, int expected)
    {
        Assert.Equal(expected, _calculator.Add(a, b));
    }

    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(0, 5, -5)]
    public void Subtract_ReturnsDifference(int a, int b, int expected)
    {
        Assert.Equal(expected, _calculator.Subtract(a, b));
    }

    [Theory]
    [InlineData(4, 3, 12)]
    [InlineData(-2, 3, -6)]
    public void Multiply_ReturnsProduct(int a, int b, int expected)
    {
        Assert.Equal(expected, _calculator.Multiply(a, b));
    }

    [Fact]
    public void Divide_ReturnsQuotient()
    {
        Assert.Equal(4, _calculator.Divide(8, 2));
    }

    [Fact]
    public void Divide_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(1, 0));
    }
}
