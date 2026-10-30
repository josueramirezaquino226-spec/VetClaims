using Microsoft.AspNetCore.Authorization;
using VetClaims.Api.Services;
using Microsoft.EntityFrameworkCore;
using VetClaims.Api.Data;
using VetClaims.Api.Models;
using VetClaims.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VetClaimsDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("VetClaimsDatabase")));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Configure Jwt:Key as a secret.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("VetClaimsAuthDatabase")
        ?? "Data Source=vetclaims-auth.db"));

builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "VetClaims",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "VetClaimsClient",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VetClaimsDbContext>();
    db.Database.EnsureCreated();
    var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    authDb.Database.EnsureCreated();
}

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    AuthDbContext db,
    IPasswordHasher<AppUser> hasher,
    JwtTokenService tokens) =>
{
    var email = request.Email.Trim().ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') ||
        string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 10)
        return Results.BadRequest(new { message = "Provide a valid email and a password of at least 10 characters." });

    if (await db.Users.AnyAsync(user => user.Email == email))
        return Results.Conflict(new { message = "Email already registered." });

    var user = new AppUser { Email = email, Role = "Reviewer" };
    user.PasswordHash = hasher.HashPassword(user, request.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new AuthResponse(tokens.CreateToken(user), user.Email, user.Role));
}).AllowAnonymous();

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AuthDbContext db,
    IPasswordHasher<AppUser> hasher,
    JwtTokenService tokens) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
        return Results.Unauthorized();

    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(user => user.Email == email);

    if (user is null ||
        hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    return Results.Ok(new AuthResponse(tokens.CreateToken(user), user.Email, user.Role));
}).AllowAnonymous();

app.MapGet("/", () =>
    Results.Ok(new
    {
        application = "VetClaims",
        status = "Running"
    })).AllowAnonymous();

app.MapGet("/api/claims", async (VetClaimsDbContext db) =>
    Results.Ok(await db.Claims.ToListAsync()));

app.MapGet("/api/claims/{id:int}", async (int id, VetClaimsDbContext db) =>
{
    var claim = await db.Claims.FindAsync(id);

    return claim is null
        ? Results.NotFound(new { message = "Claim not found" })
        : Results.Ok(claim);
});

app.MapPost("/api/claims", async (
    CreateClaimRequest request,
    VetClaimsDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.OwnerName))
        return Results.BadRequest(new { message = "Owner name is required" });

    if (string.IsNullOrWhiteSpace(request.PetName))
        return Results.BadRequest(new { message = "Pet name is required" });

    if (string.IsNullOrWhiteSpace(request.Description))
        return Results.BadRequest(new { message = "Description is required" });

    var validationError = ClaimValidator.Validate(
        request.OwnerName,
        request.PetName,
        request.Description,
        request.InvoiceAmount,
        request.Deductible,
        request.CoveragePercentage
    );

    if (validationError != null)
    {
        return Results.BadRequest(new { error = validationError });
    }

    var eligibleAmount = Math.Max(
        0m,
        request.InvoiceAmount - request.Deductible);

    var coveredAmount =
        eligibleAmount * request.CoveragePercentage / 100;

    var claim = new Claim
{
        OwnerName = request.OwnerName,
        PetName = request.PetName,
        Description = request.Description,
        InvoiceAmount = request.InvoiceAmount,
        Deductible = request.Deductible,
        CoveragePercentage = request.CoveragePercentage,
        CoveredAmount = coveredAmount,
        Status = "Received"
};

    db.Claims.Add(claim);
    await db.SaveChangesAsync();

    return Results.Created($"/api/claims/{claim.Id}", claim);
});


app.MapPut("/api/claims/{id:int}", async (
    int id,
    UpdateClaimRequest request,
    VetClaimsDbContext db) =>
{
    var claim = await db.Claims.FindAsync(id);

    if (claim is null)
        return Results.NotFound(new { message = "Claim not found" });

    if (string.IsNullOrWhiteSpace(request.OwnerName))
        return Results.BadRequest(new { message = "Owner name is required" });

    if (string.IsNullOrWhiteSpace(request.PetName))
        return Results.BadRequest(new { message = "Pet name is required" });

    if (string.IsNullOrWhiteSpace(request.Description))
        return Results.BadRequest(new { message = "Description is required" });

    if (request.InvoiceAmount <= 0)
        return Results.BadRequest(new { message = "Invoice amount must be greater than zero" });

    if (request.Deductible < 0 || request.Deductible > request.InvoiceAmount)
        return Results.BadRequest(new { message = "Deductible must be between zero and invoice amount" });

    if (request.CoveragePercentage < 0 || request.CoveragePercentage > 100)
        return Results.BadRequest(new { message = "Coverage percentage must be between 0 and 100" });

    var coveredAmount = ClaimCalculator.Calculate(
        request.InvoiceAmount,
        request.Deductible,
        request.CoveragePercentage);

    claim.OwnerName = request.OwnerName;
    claim.PetName = request.PetName;
    claim.Description = request.Description;
    claim.InvoiceAmount = request.InvoiceAmount;
    claim.Deductible = request.Deductible;
    claim.CoveragePercentage = request.CoveragePercentage;
    claim.CoveredAmount = coveredAmount;

    await db.SaveChangesAsync();

    return Results.Ok(claim);
});

app.MapPatch("/api/claims/{id:int}/status", async (
    int id,
    UpdateClaimStatusRequest request,
    VetClaimsDbContext db) =>
{
    var claim = await db.Claims.FindAsync(id);

    if (claim is null)
        return Results.NotFound(new { message = "Claim not found" });

    var allowedStatuses = new[] { "Received", "UnderReview", "Approved", "Rejected" };

    if (!allowedStatuses.Contains(request.Status))
        return Results.BadRequest(new { message = "Invalid claim status" });

    claim.Status = request.Status;
    await db.SaveChangesAsync();

    return Results.Ok(claim);
});

app.MapDelete("/api/claims/{id:int}", async (
    int id,
    VetClaimsDbContext db) =>
{
    var claim = await db.Claims.FindAsync(id);

    if (claim is null)
        return Results.NotFound(new { message = "Claim not found" });

    db.Claims.Remove(claim);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();
public partial class Program { }
