using be_digitalin.Constants;
namespace be_digitalin.Models;

public class GameMetadata
{
    public int Id { get; set; }
    public string Brand { get; set; } = string.Empty; // Contoh: "MOBILE LEGENDS"
    
    // Tipe Form: "USER_ID_ONLY", "USER_ID_ZONE", "USER_ID_SERVER"
    public string InputType { get; set; } = AppConstants.InputTypes.UserIdOnly; 
    public string? ServerList { get; set; }
    public string? ImageUrl { get; set; }
}