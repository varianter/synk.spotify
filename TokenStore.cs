namespace Synk.Spotify;

public class TokenStore
{
    public Task<IEnumerable<Token>> GetTokens()
    {
        throw new NotImplementedException();
    }

    public Task UpdateToken(Token refreshedToken)
    {
        throw new NotImplementedException();
    }
}

public record Token(string AccessToken, string RefreshToken, DateTime ExpiresAt)
{
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
}
