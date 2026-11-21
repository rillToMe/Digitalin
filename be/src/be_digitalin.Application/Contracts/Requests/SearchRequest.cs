namespace be_digitalin.DTOs;

public record SearchRequest(string? Q, int Limit = 10);
