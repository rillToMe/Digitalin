namespace be_digitalin.Models;

public class GameProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string GameCode { get; set; } = string.Empty;

    public List<TopupOrder> Orders { get; set; } = new();
    
    public string BuyerSkuCode { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    public string? ImageUrl { get; set; }

    public decimal? CustomMargin { get; set; }
    
    public bool IsActive { get; set; } = true;

}