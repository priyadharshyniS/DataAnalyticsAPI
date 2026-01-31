using CsvHelper;
using CsvHelper.Configuration;
using DataAnalyticsApi.Data;
using DataAnalyticsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAnalyticsApi.Services;

public class CsvRecord
{
    public int OrderId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Region { get; set; }
    public DateTime OrderDate { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingCost { get; set; }
    public string? PaymentMethod { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerAddress { get; set; }
}

public class CsvLoader
{
    private readonly AppDbContext _db;
    private readonly ILogger<CsvLoader> _logger;

    public CsvLoader(AppDbContext db, ILogger<CsvLoader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> LoadAsync(string path, bool overwrite = false, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            _logger.LogWarning("CSV file not found: {path}", path);
            throw new FileNotFoundException(path);
        }

        var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, config);
        var records = csv.GetRecords<CsvRecord>();

        var list = records.ToList();

        using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (overwrite)
        {
            _db.Orders.RemoveRange(_db.Orders);
            _db.Products.RemoveRange(_db.Products);
            _db.Customers.RemoveRange(_db.Customers);
            await _db.SaveChangesAsync(ct);
        }

        // Phase 1: upsert distinct products and customers first
        var productIds = list.Select(r => r.ProductId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var existingProducts = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(ct);
        var productById = existingProducts.ToDictionary(p => p.Id, p => p);

        foreach (var pid in productIds)
        {
            var rec = list.FirstOrDefault(r => r.ProductId == pid);
            if (rec == null) continue;
            if (!productById.TryGetValue(pid, out var prod))
            {
                prod = new Product { Id = pid, Name = rec.ProductName ?? string.Empty, Category = rec.Category };
                _db.Products.Add(prod);
                productById[pid] = prod;
            }
            else
            {
                prod.Name = rec.ProductName ?? prod.Name;
                prod.Category = rec.Category ?? prod.Category;
                _db.Products.Update(prod);
            }
        }

        var customerIds = list.Select(r => r.CustomerId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var existingCustomers = await _db.Customers.Where(c => customerIds.Contains(c.Id)).ToListAsync(ct);
        var customerById = existingCustomers.ToDictionary(c => c.Id, c => c);

        foreach (var cid in customerIds)
        {
            var rec = list.FirstOrDefault(r => r.CustomerId == cid);
            if (rec == null) continue;
            if (!customerById.TryGetValue(cid, out var cust))
            {
                cust = new Models.Customer { Id = cid, Name = rec.CustomerName, Email = rec.CustomerEmail, Address = rec.CustomerAddress };
                _db.Customers.Add(cust);
                customerById[cid] = cust;
            }
            else
            {
                cust.Name = rec.CustomerName ?? cust.Name;
                cust.Email = rec.CustomerEmail ?? cust.Email;
                cust.Address = rec.CustomerAddress ?? cust.Address;
                _db.Customers.Update(cust);
            }
        }

        await _db.SaveChangesAsync(ct);

        // Phase 2: upsert orders
        var count = 0;
        foreach (var r in list)
        {
            if (ct.IsCancellationRequested) break;

            var existing = await _db.Orders.FindAsync(new object[] { r.OrderId }, ct);
            if (existing == null)
            {
                var order = new Order
                {
                    Id = r.OrderId,
                    OrderDate = r.OrderDate,
                    ProductId = r.ProductId,
                    CustomerId = string.IsNullOrWhiteSpace(r.CustomerId) ? null : r.CustomerId,
                    Region = r.Region,
                    Quantity = r.Quantity,
                    UnitPrice = r.UnitPrice,
                    Discount = r.Discount,
                    ShippingCost = r.ShippingCost,
                    PaymentMethod = r.PaymentMethod
                };
                _db.Orders.Add(order);
            }
            else
            {
                existing.OrderDate = r.OrderDate;
                existing.ProductId = r.ProductId;
                existing.CustomerId = string.IsNullOrWhiteSpace(r.CustomerId) ? existing.CustomerId : r.CustomerId;
                existing.Region = r.Region;
                existing.Quantity = r.Quantity;
                existing.UnitPrice = r.UnitPrice;
                existing.Discount = r.Discount;
                existing.ShippingCost = r.ShippingCost;
                existing.PaymentMethod = r.PaymentMethod;
                _db.Orders.Update(existing);
            }

            count++;
            if (count % 500 == 0)
            {
                await _db.SaveChangesAsync(ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Loaded {count} records from {path}", count, path);
        return count;
    }
}
