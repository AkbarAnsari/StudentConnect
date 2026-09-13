using StudentConnect.Domain;
using Xunit;

namespace StudentConnect.Api.Tests.Models;

public class ResultCategoryTests
{
    [Theory]
    [InlineData(100, "Excellent")]
    [InlineData(90, "Excellent")]
    [InlineData(89.9, "Very Good")]
    [InlineData(75, "Very Good")]
    [InlineData(74.9, "Good")]
    [InlineData(60, "Good")]
    [InlineData(59.9, "Needs Improvement")]
    [InlineData(40, "Needs Improvement")]
    [InlineData(39.9, "Needs Support")]
    [InlineData(0, "Needs Support")]
    public void From_ReturnsExpectedCategory(double percentage, string expected)
    {
        Assert.Equal(expected, ResultCategory.From(percentage));
    }
}
