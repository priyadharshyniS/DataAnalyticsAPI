using System.ComponentModel.DataAnnotations.Schema;

namespace DataAnalyticsApi.Models;

public class Order
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    // ProductId is a string (e.g., "P123")
    public string ProductId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? Region { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; } // fraction: 0.1 = 10%
    public decimal ShippingCost { get; set; }
    public string? PaymentMethod { get; set; }

    [NotMapped]
    public decimal NetRevenue => Quantity * UnitPrice * (1 - Discount) + ShippingCost;
}
