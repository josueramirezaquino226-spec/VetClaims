using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public class VetClaimsApiFactory : WebApplicationFactory<Program>
{
    private readonly string authDatabasePath = Path.Combine(Path.GetTempPath(), $"vetclaims-auth-test-{Guid.NewGuid():N}.db");
        private readonly string databasePath = Path.Combine(
        Path.GetTempPath(), $"vetclaims-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VetClaimsDatabase"] = $"Data Source={databasePath}",
                ["ConnectionStrings:VetClaimsAuthDatabase"] = $"Data Source={authDatabasePath}",
        ["Jwt:Key"] = "TEST_ONLY_vetclaims_jwt_key_12345678901234567890",
        ["Jwt:Issuer"] = "VetClaims",
        ["Jwt:Audience"] = "VetClaimsClient"
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(databasePath))
            File.Delete(databasePath);
    }
}

public class ClaimsApiIntegrationTests
    : IClassFixture<VetClaimsApiFactory>
{
    private readonly HttpClient client;

    public ClaimsApiIntegrationTests(VetClaimsApiFactory factory)
    {
        client = factory.CreateClient();
    }


    private async Task AuthenticateAsync()
    {
        var email = $"test-{Guid.NewGuid():N}@vetclaims.test";
        const string password = "TestPassword123!";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { Email = email, Password = password });

        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = email, Password = password });

        login.EnsureSuccessStatusCode();

        var json = await login.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var token = doc.RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task PostClaim_ValidRequest_ReturnsCreated()
    {
            await AuthenticateAsync();
        var response = await client.PostAsJsonAsync("/api/claims", new
        {
            ownerName = "Juan",
            petName = "Max",
            description = "Consulta veterinaria",
            invoiceAmount = 1500,
            deductible = 200,
            coveragePercentage = 80
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostClaim_InvalidAmount_ReturnsBadRequest()
    {
            await AuthenticateAsync();
        var response = await client.PostAsJsonAsync("/api/claims", new
        {
            ownerName = "Juan",
            petName = "Max",
            description = "Consulta",
            invoiceAmount = 0,
            deductible = 0,
            coveragePercentage = 80
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetClaim_MissingId_ReturnsNotFound()
    {
            await AuthenticateAsync();
        var response = await client.GetAsync("/api/claims/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutClaim_MissingId_ReturnsNotFound()
    {
            await AuthenticateAsync();
        var response = await client.PutAsJsonAsync("/api/claims/999999", new
        {
            ownerName = "Juan",
            petName = "Max",
            description = "Consulta",
            invoiceAmount = 1500,
            deductible = 200,
            coveragePercentage = 80
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_MissingId_ReturnsNotFound()
    {
            await AuthenticateAsync();
        var response = await client.PatchAsJsonAsync(
            "/api/claims/999999/status", new { status = "Approved" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteClaim_MissingId_ReturnsNotFound()
    {
            await AuthenticateAsync();
        var response = await client.DeleteAsync("/api/claims/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task Claim_FullLifecycle_WorksCorrectly()
    {
            await AuthenticateAsync();
        var create = await client.PostAsJsonAsync("/api/claims", new
        {
            ownerName = "Maria",
            petName = "Luna",
            description = "Revision veterinaria",
            invoiceAmount = 2000,
            deductible = 300,
            coveragePercentage = 80
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var json = await create.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("id").GetInt32();

        var get = await client.GetAsync($"/api/claims/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/claims/{id}", new
        {
            ownerName = "Maria",
            petName = "Luna",
            description = "Revision actualizada",
            invoiceAmount = 2500,
            deductible = 300,
            coveragePercentage = 80
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var patch = await client.PatchAsJsonAsync(
            $"/api/claims/{id}/status",
            new { status = "Approved" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var delete = await client.DeleteAsync($"/api/claims/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetAsync($"/api/claims/{id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }


    [Fact]
    public async Task PatchStatus_InvalidStatus_ReturnsBadRequest()
    {
            await AuthenticateAsync();
        var create = await client.PostAsJsonAsync("/api/claims", new
        {
            ownerName = "Pedro",
            petName = "Rocky",
            description = "Consulta",
            invoiceAmount = 1000,
            deductible = 100,
            coveragePercentage = 80
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var json = await create.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var id = document.RootElement.GetProperty("id").GetInt32();

        var response = await client.PatchAsJsonAsync(
            $"/api/claims/{id}/status",
            new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetClaims_ReturnsOk()
    {
            await AuthenticateAsync();
        var response = await client.GetAsync("/api/claims");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

}
