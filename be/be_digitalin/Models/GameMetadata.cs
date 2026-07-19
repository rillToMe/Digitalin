namespace be_digitalin.Models;

public class GameMetadata
{
    public int Id { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string InputSchemaJson { get; set; } = "[]";

    public string TargetTemplate { get; set; } = "{userId}";
}