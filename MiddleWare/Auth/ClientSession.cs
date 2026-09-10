namespace MiddleWare.Auth;

public sealed record ClientSession(string ClientNo, string CallbackUrl, string Token, DateTimeOffset IssuedAt);
