namespace DataAnalyticsApi.Models;

public class Customer
{
    // Customer IDs in CSV like "C456"
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}
