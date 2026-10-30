namespace VetClaims.Api.Models;

public sealed class Claim
{
    public int Id { get; set; }
    public string OwnerName { get; set; } = "";
    public string PetName { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal InvoiceAmount { get; set; }
    public decimal Deductible { get; set; }
    public decimal CoveragePercentage { get; set; }
    public decimal CoveredAmount { get; set; }
    public string Status { get; set; } = "Received";
}

public sealed record CreateClaimRequest(
    string OwnerName,
    string PetName,
    string Description,
    decimal InvoiceAmount,
    decimal Deductible,
    decimal CoveragePercentage
);

public sealed record UpdateClaimRequest(
    string OwnerName,
    string PetName,
    string Description,
    decimal InvoiceAmount,
    decimal Deductible,
    decimal CoveragePercentage
);

public sealed record UpdateClaimStatusRequest(
    string Status
);
