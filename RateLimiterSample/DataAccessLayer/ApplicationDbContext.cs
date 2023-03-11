using Microsoft.EntityFrameworkCore;
using RateLimiterSample.DataAccessLayer.Entities;

namespace RateLimiterSample.DataAccessLayer;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        _ = modelBuilder.Entity<Account>(entity =>
        {
            _ = entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            _ = entity.Property(e => e.ApiKey)
                .IsRequired()
                .HasMaxLength(50)
                .IsUnicode(false);
            _ = entity.Property(e => e.UserName)
                .IsRequired()
                .HasMaxLength(50);

            _ = entity.HasOne(d => d.Subscription).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.SubscriptionId)
                .HasConstraintName("FK_Accounts_Subscriptions");
        });

        _ = modelBuilder.Entity<Subscription>(entity =>
        {
            _ = entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            _ = entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);
        });
    }
}
