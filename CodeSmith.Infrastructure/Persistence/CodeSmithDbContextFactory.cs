using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodeSmith.Infrastructure.Persistence;

public class CodeSmithDbContextFactory : IDesignTimeDbContextFactory<CodeSmithDbContext>
{
    public CodeSmithDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CodeSmithDbContext>();

        // Use the same Entra ID connection string that worked from the command line
        optionsBuilder.UseSqlServer(
            "Server=tcp:sql-codesmith-prod-centralus-001.database.windows.net,1433;Initial Catalog=db-codesmith-prod-centralus-001;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

        return new CodeSmithDbContext(optionsBuilder.Options);
    }
}