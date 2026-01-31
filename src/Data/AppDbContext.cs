using DataAnalyticsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAnalyticsApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Product>().HasKey(p => p.Id);
        modelBuilder.Entity<Customer>().HasKey(c => c.Id);
        modelBuilder.Entity<Order>().HasOne<Product>().WithMany().HasForeignKey(o => o.ProductId).HasPrincipalKey(p => p.Id);
        modelBuilder.Entity<Order>().HasOne<Customer>().WithMany().HasForeignKey(o => o.CustomerId).HasPrincipalKey(c => c.Id);
    }
}
