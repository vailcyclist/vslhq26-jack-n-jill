using FunnyCodeAnalyzer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FunnyCodeAnalyzer.Api.Data;

internal sealed class HumorDbContext : DbContext
{
    public HumorDbContext(DbContextOptions<HumorDbContext> options)
        : base(options)
    {
    }

    public DbSet<HumorRecord> HumorRecords => Set<HumorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HumorRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.UserIdentifier).HasColumnName("UserToken").HasMaxLength(256).IsRequired();
            entity.Property(record => record.IssueTopic).HasMaxLength(128).IsRequired();
            entity.Property(record => record.HumorMode).HasMaxLength(64).IsRequired();
            entity.Property(record => record.HumorText).HasMaxLength(2000).IsRequired();
            entity.Property(record => record.Channel).HasMaxLength(64).IsRequired();
            entity.Property(record => record.Recipient).HasMaxLength(256).IsRequired();
            entity.Property(record => record.Source).HasMaxLength(64).IsRequired();
            entity.HasIndex(record => new { record.UserIdentifier, record.CreatedUtc });
        });
    }
}