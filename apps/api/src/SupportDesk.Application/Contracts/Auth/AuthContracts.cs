namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Who is signed in: enough to greet them.</summary>
public sealed record CurrentUserDto(int Id, string FullName, string Email);

/// <summary>The token, when it stops working, and who it belongs to.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);