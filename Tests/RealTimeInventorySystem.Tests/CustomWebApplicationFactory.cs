using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RealTimeInventorySystem.Data;
using RealTimeInventorySystem.Models;

namespace Tests.RealTimeInventorySystem.Tests;

public class CustomWebApplicationFactory
    : WebApplicationFactory<Program>
{

    private const string DatabaseName = "InventoryTests";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptors = services
                .Where(x =>
                    x.ServiceType ==
                    typeof(DbContextOptions<InventoryDbContext>))
                .ToList();  

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<InventoryDbContext>(options =>
            {
                options.UseInMemoryDatabase(DatabaseName);
            });

            services.Configure<RateLimiterOptions>(options =>
            {});

            var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<InventoryDbContext>();

            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            SeedDatabase(db);
        });
    }

    private static void SeedDatabase(
        InventoryDbContext db)
    {
        if (!db.Products.Any())
        {
            db.Products.Add(new Product
            {
                ProductId = 1,
                ProductCode = "P001",
                ProductName = "Test Product",
                Description = "Product for tests",
                Category = "Electronics",
                UnitPrice = 100,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.Warehouses.Any())
        {
            db.Warehouses.Add(new Warehouse
            {
                WarehouseId = 1,
                WarehouseName = "Main Warehouse",
                Location = "Test Location",
                Capacity = 1000,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (!db.Inventories.Any())
        {
            db.Inventories.Add(new Inventory
            {
                InventoryId = 1,
                ProductId = 1,
                WarehouseId = 1,
                Quantity = 10,
                ReservedQuantity = 0,
                Version = 0,
                LastUpdated = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}