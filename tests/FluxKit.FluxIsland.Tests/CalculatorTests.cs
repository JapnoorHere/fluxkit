using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class CalculatorTests
{
    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("-3+5", 2)]
    [InlineData("2^3^2", 512)]
    [InlineData("17 % 5", 2)]
    [InlineData("3 x 4", 12)]
    [InlineData("0.5 * 4", 2)]
    [InlineData("((2))", 2)]
    public void Follows_the_usual_order_of_operations(string text, double expected) =>
        Assert.Equal(expected, Calculator.Evaluate(text)!.Value, 9);

    [Theory]
    [InlineData("1/0")]
    [InlineData("2*")]
    [InlineData("(1+2")]
    [InlineData("abc")]
    [InlineData("1 2")]
    [InlineData("10^400")]
    public void Broken_or_impossible_sums_give_nothing(string text) => Assert.Null(Calculator.Evaluate(text));

    [Fact]
    public void Deep_nesting_is_refused_instead_of_overflowing() =>
        Assert.Null(Calculator.Evaluate(new string('(', 500) + "1" + new string(')', 500)));

    [Fact]
    public void Plain_words_with_an_x_are_not_maths() => Assert.False(Calculator.LooksLikeMaths("box"));
}
