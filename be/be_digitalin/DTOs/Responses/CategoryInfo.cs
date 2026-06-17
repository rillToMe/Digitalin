namespace be_digitalin.DTOs;

public record CategoryInfo(
    string Brand,       // Contoh: "MOBILE LEGENDS"
    string GameCode,    // Contoh: "mobile-legends"
    string? ImageUrl,   // URL icon game dari R2
    string Category,     
    string InputType 
);