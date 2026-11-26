namespace LifeAchievements.Shared.Dtos;

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

public record AuthTokenResponse(string AccessToken, string RefreshToken, long ExpiresIn, string TokenType);
