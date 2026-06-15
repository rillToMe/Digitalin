namespace be_digitalin.DTOs;

public record LoginRequest(string Username, string Password);
public record CustomerLoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Password, string FullName);