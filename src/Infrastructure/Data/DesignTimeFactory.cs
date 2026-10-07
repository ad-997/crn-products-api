using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>Enables migration tooling without starting HTTP or needing a JWT secret.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? "Server=localhost,1433;Database=CrnAssessment;User Id=sa;Encrypt=True;TrustServerCertificate=True";
        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options
        );
    }
}
