namespace be_digitalin.DTOs;

public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int Limit);