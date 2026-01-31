using DataAnalyticsApi.Data;
using DataAnalyticsApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataAnalyticsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidateRevenueController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CsvLoader _loader;
    private readonly ILogger<ValidateRevenueController> _logger;

    public ValidateRevenueController(AppDbContext db, CsvLoader loader, ILogger<ValidateRevenueController> logger)
    {
        _db = db;
        _loader = loader;
        _logger = logger;
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> TriggerDataRefresh([FromQuery] bool overwrite = false)
    {
        try
        {
            if (_loader != null)
            {
                var csvPath = HttpContext.RequestServices.GetService<IConfiguration>()?.GetValue<string>("Csv:Path") ?? "data/sales_sample.csv";
                var loaded = await _loader.LoadAsync(csvPath, overwrite);
                return Ok(new { loaded });
            }
        }
        catch (FileNotFoundException fx)
        {
            _logger.LogWarning(fx, "CSV not found during refresh");
            return NotFound("CSV file not found: " + fx.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TriggerDataRefresh failed");
            return StatusCode(500, "Refresh failed");
        }
    }

    [HttpGet("total")]
    public async Task<IActionResult> GetTotalRevenue([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        if (startDate.HasValue && endDate.HasValue && startDate > endDate) return BadRequest("startDate must be <= endDate");

        var query = _db.Orders.AsQueryable();
        if (startDate.HasValue) query = query.Where(o => o.OrderDate >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(o => o.OrderDate <= endDate.Value.Date);

        try
        {
            var total = await query.SumAsync(o => o.Quantity * o.UnitPrice * (1 - o.Discount) + o.ShippingCost);
            return Ok(new { total });
        }
        catch (Exception ex)
        {
            if (IsSqliteDecimalSumError(ex))
            {
                var list = await query.Select(o => new { o.Quantity, o.UnitPrice, o.Discount, o.ShippingCost }).ToListAsync();
                var total = list.Sum(x => x.Quantity * x.UnitPrice * (1 - x.Discount) + x.ShippingCost);
                return Ok(new { total });
            }
            _logger.LogError(ex, "GetTotalRevenue failed");
            return StatusCode(500, "Failed to calculate total revenue");
        }
    }

    [HttpGet("by_product")]
    public async Task<IActionResult> GetRevenueByProduct([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        if (startDate.HasValue && endDate.HasValue && startDate > endDate) return BadRequest("startDate must be <= endDate");

        var query = _db.Orders.AsQueryable();
        if (startDate.HasValue) query = query.Where(o => o.OrderDate >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(o => o.OrderDate <= endDate.Value.Date);

        try
        {
            var grouped = await query.GroupBy(o => o.ProductId)
                .Select(g => new { product_id = g.Key, revenue = g.Sum(o => o.Quantity * o.UnitPrice * (1 - o.Discount) + o.ShippingCost) })
                .ToListAsync();

            var productIds = grouped.Select(d => d.product_id).ToList();
            var names = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
            var result = grouped.Select(d => new { product_id = d.product_id, product_name = names.GetValueOrDefault(d.product_id), revenue = d.revenue });
            return Ok(result);
        }
        catch (Exception ex)
        {
            if (IsSqliteDecimalSumError(ex))
            {
                var list = await query.Select(o => new { o.ProductId, o.Quantity, o.UnitPrice, o.Discount, o.ShippingCost }).ToListAsync();
                var grouped = list.GroupBy(x => x.ProductId).Select(g => new { product_id = g.Key, revenue = g.Sum(x => x.Quantity * x.UnitPrice * (1 - x.Discount) + x.ShippingCost) }).ToList();
                var productIds = grouped.Select(d => d.product_id).ToList();
                var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
                var result = grouped.Select(d => new { product_id = d.product_id, product_name = products.GetValueOrDefault(d.product_id), revenue = d.revenue });
                return Ok(result);
            }
            _logger.LogError(ex, "GetRevenueByProduct failed");
            return StatusCode(500, "Failed to calculate revenue by product");
        }
    }

    [HttpGet("by_category")]
    public async Task<IActionResult> GetRevenueByCategory([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        if (startDate.HasValue && endDate.HasValue && startDate > endDate) return BadRequest("startDate must be <= endDate");

        var q = from o in _db.Orders
                join p in _db.Products on o.ProductId equals p.Id
                select new { o, p };
        if (startDate.HasValue) q = q.Where(x => x.o.OrderDate >= startDate.Value.Date);
        if (endDate.HasValue) q = q.Where(x => x.o.OrderDate <= endDate.Value.Date);

        try
        {
            // compute per-row revenue then group client-side to avoid provider decimal-sum issues
            var rows = await q.Select(x => new { category = x.p.Category, revenue = x.o.Quantity * x.o.UnitPrice * (1 - x.o.Discount) + x.o.ShippingCost }).ToListAsync();
            var grouped = rows.GroupBy(r => r.category).Select(g => new { category = g.Key, revenue = g.Sum(x => x.revenue) }).ToList();
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRevenueByCategory failed");
            return StatusCode(500, "Failed to calculate revenue by category");
        }
    }

    [HttpGet("by_region")]
    public async Task<IActionResult> GetRevenueByRegion([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        if (startDate.HasValue && endDate.HasValue && startDate > endDate) return BadRequest("startDate must be <= endDate");

        var query = _db.Orders.AsQueryable();
        if (startDate.HasValue) query = query.Where(o => o.OrderDate >= startDate.Value.Date);
        if (endDate.HasValue) query = query.Where(o => o.OrderDate <= endDate.Value.Date);

        try
        {
            var grouped = await query.GroupBy(o => o.Region).Select(g => new { region = g.Key, revenue = g.Sum(o => o.Quantity * o.UnitPrice * (1 - o.Discount) + o.ShippingCost) }).ToListAsync();
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            if (IsSqliteDecimalSumError(ex))
            {
                var list = await query.Select(o => new { o.Region, o.Quantity, o.UnitPrice, o.Discount, o.ShippingCost }).ToListAsync();
                var grouped = list.GroupBy(x => x.Region).Select(g => new { region = g.Key, revenue = g.Sum(x => x.Quantity * x.UnitPrice * (1 - x.Discount) + x.ShippingCost) }).ToList();
                return Ok(grouped);
            }
            _logger.LogError(ex, "GetRevenueByRegion failed");
            return StatusCode(500, "Failed to calculate revenue by region");
        }
    }

    // Debug endpoints
    [HttpGet("debug/orders/count")]
    public async Task<IActionResult> GetOrdersCount()
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        var count = await _db.Orders.CountAsync();
        return Ok(new { count });
    }

    [HttpGet("debug/orders/sample")]
    public async Task<IActionResult> GetOrdersSample([FromQuery] int limit = 20)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        var rows = await _db.Orders.OrderBy(o => o.Id).Take(limit).Select(o => new { o.Id, o.ProductId, o.OrderDate, o.Quantity, o.UnitPrice, o.Discount, o.ShippingCost }).ToListAsync();
        return Ok(new { count = rows.Count, sample = rows });
    }

    [HttpGet("debug/products/sample")]
    public async Task<IActionResult> GetProductsSample([FromQuery] int limit = 50)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        var rows = await _db.Products.OrderBy(p => p.Id).Take(limit).Select(p => new { p.Id, p.Name, p.Category }).ToListAsync();
        return Ok(new { count = rows.Count, sample = rows });
    }

    [HttpGet("debug/customers/sample")]
    public async Task<IActionResult> GetCustomersSample([FromQuery] int limit = 50)
    {
        if (_db == null) return StatusCode(500, "Database unavailable");
        var rows = await _db.Customers.OrderBy(c => c.Id).Take(limit).Select(c => new { c.Id, c.Name, c.Email, c.Address }).ToListAsync();
        return Ok(new { count = rows.Count, sample = rows });
    }

    private static bool IsSqliteDecimalSumError(Exception ex)
    {
        if (ex == null) return false;
        var msg = ex.Message ?? string.Empty;
        if (msg.IndexOf("cannot apply aggregate", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (msg.IndexOf("aggregate operator 'Sum'", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (ex.InnerException != null) return IsSqliteDecimalSumError(ex.InnerException);
        return false;
    }
}
