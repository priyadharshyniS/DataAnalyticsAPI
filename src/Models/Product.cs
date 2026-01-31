namespace DataAnalyticsApi.Models;

public class Product
{
    // Product IDs in provided CSV are strings like "P123"
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}
