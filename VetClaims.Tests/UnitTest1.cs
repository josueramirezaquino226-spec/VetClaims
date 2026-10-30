using VetClaims.Api.Services;

namespace VetClaims.Tests;

public class ClaimCalculatorTests
{
    [Theory]
    [InlineData(1500, 200, 80, 1040)]
    [InlineData(1500, 200, 100, 1300)]
    [InlineData(1500, 200, 0, 0)]
    [InlineData(1500, 1500, 80, 0)]
    public void Calculate_ReturnsExpectedCoveredAmount(
        decimal invoice, decimal deductible,
        decimal coverage, decimal expected)
    {
        var result = ClaimCalculator.Calculate(
            invoice, deductible, coverage);

        Assert.Equal(expected, result);
    }
}

public class ClaimValidatorTests
{
    [Fact]
    public void Validate_ValidData_ReturnsNull()
    {
        var result = ClaimValidator.Validate(
            "Juan", "Max", "Consulta", 1500, 200, 80);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "Max", "Consulta", 1500, 200, 80, "Owner name is required")]
    [InlineData("Juan", "", "Consulta", 1500, 200, 80, "Pet name is required")]
    [InlineData("Juan", "Max", "", 1500, 200, 80, "Description is required")]
    [InlineData("Juan", "Max", "Consulta", 0, 0, 80, "Invoice amount must be greater than zero")]
    [InlineData("Juan", "Max", "Consulta", 1500, 1600, 80, "Deductible must be between zero and invoice amount")]
    [InlineData("Juan", "Max", "Consulta", 1500, 200, 110, "Coverage percentage must be between 0 and 100")]
    public void Validate_InvalidData_ReturnsExpectedError(
        string owner, string pet, string description,
        decimal invoice, decimal deductible,
        decimal coverage, string expected)
    {
        var result = ClaimValidator.Validate(
            owner, pet, description,
            invoice, deductible, coverage);

        Assert.Equal(expected, result);
    }
}
