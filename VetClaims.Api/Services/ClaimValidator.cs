namespace VetClaims.Api.Services;

public static class ClaimValidator
{
    public static string? Validate(
        string ownerName,
        string petName,
        string description,
        decimal invoiceAmount,
        decimal deductible,
        decimal coveragePercentage)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            return "Owner name is required";

        if (string.IsNullOrWhiteSpace(petName))
            return "Pet name is required";

        if (string.IsNullOrWhiteSpace(description))
            return "Description is required";

        if (invoiceAmount <= 0)
            return "Invoice amount must be greater than zero";

        if (deductible < 0 || deductible > invoiceAmount)
            return "Deductible must be between zero and invoice amount";

        if (coveragePercentage < 0 || coveragePercentage > 100)
            return "Coverage percentage must be between 0 and 100";

        return null;
    }
}
