﻿using Microsoft.EntityFrameworkCore;
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
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ApiKey)
                .IsRequired()
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.Subscription).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.SubscriptionId)
                .HasConstraintName("FK_Accounts_Subscriptions");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);
        });
    }
}
