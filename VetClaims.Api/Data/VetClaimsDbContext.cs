using Microsoft.EntityFrameworkCore;
using VetClaims.Api.Models;

namespace VetClaims.Api.Data;

public class VetClaimsDbContext : DbContext
{
    public VetClaimsDbContext(
        DbContextOptions<VetClaimsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Claim> Claims => Set<Claim>();
}
