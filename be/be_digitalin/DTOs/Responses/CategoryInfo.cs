namespace be_digitalin.DTOs;

public record CategoryInfo(
    string Brand,       // Contoh: "MOBILE LEGENDS"
    string GameCode,    // Contoh: "mobile-legends" (buat URL ramah SEO seperti UniPin)
    string? ImageUrl,   // URL ikon game dari R2
    string Category,     // Contoh: "Games"
    string InputType // <-- Kasih tau frontend form apa yang harus dirender
);