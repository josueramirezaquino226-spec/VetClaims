namespace VetClaims.Api.Services;

public static class ClaimCalculator
{
    public static decimal Calculate(
        decimal invoiceAmount,
        decimal deductible,
        decimal coveragePercentage)
    {
        var eligibleAmount = Math.Max(0m, invoiceAmount - deductible);

        return eligibleAmount * coveragePercentage / 100m;
    }
}
