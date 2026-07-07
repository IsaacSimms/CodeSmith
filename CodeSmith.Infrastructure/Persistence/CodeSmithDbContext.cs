// == CodeSmith DbContext for Usage Data Layer == //
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence;

public class CodeSmithDbContext : DbContext
{
    public CodeSmithDbContext(DbContextOptions<CodeSmithDbContext> options)
        : base(options)
    {
    }

    public DbSet<CreditBalance> CreditBalances => Set<CreditBalance>();
    public DbSet<UsageLedgerEntry> UsageLedgerEntries => Set<UsageLedgerEntry>();
    public DbSet<IpFreeUsage> IpFreeUsages => Set<IpFreeUsage>();
    public DbSet<ProcessedStripeEvent> ProcessedStripeEvents => Set<ProcessedStripeEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CreditBalance: PK on objectId, concurrency via RowVersion
        modelBuilder.Entity<CreditBalance>(b =>
        {
            b.HasKey(x => x.ObjectId);
            b.Property(x => x.PaidCreditsBalance).HasPrecision(18, 6);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.ObjectId);
        });

        // Ledger: append-only, indexed for per-user queries
        modelBuilder.Entity<UsageLedgerEntry>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.CostUsd).HasPrecision(18, 6);
            b.Property(x => x.ProviderCostUsd).HasPrecision(18, 6);
            b.Property(x => x.Type).HasConversion<int>();
            b.HasIndex(x => new { x.ObjectId, x.TimestampUtc });
            b.HasIndex(x => x.TimestampUtc);
        });

        // ProcessedStripeEvent: webhook dedup keyed by Stripe event id
        modelBuilder.Entity<ProcessedStripeEvent>(b =>
        {
            b.HasKey(x => x.EventId);
        });

        // IpFreeUsage: aggregate cap per IP (string key)
        modelBuilder.Entity<IpFreeUsage>(b =>
        {
            b.HasKey(x => x.Ip);
            b.HasIndex(x => x.Ip);
        });
    }
}
