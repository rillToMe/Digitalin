namespace be_digitalin.DTOs.Requests;

public record CreateFaqRequest(
    string Question,
    string Answer,
    string Category,
    bool Featured);

public record UpdateFaqRequest(
    string Question,
    string Answer,
    string Category,
    bool Featured);
