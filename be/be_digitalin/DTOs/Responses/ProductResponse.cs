namespace be_digitalin.DTOs;

public record ProductResponse(
    int Id,
    string Name,
    decimal Price,
    string GameCode
    );