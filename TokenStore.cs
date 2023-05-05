namespace Synk.Spotify;

public class TokenStore
{
    public Task<IEnumerable<Token>> GetTokens()
    {
        throw new NotImplementedException();
    }
}

public record Token(string AccessToken, string RefreshToken, DateTime ExpiresAt);
