namespace Synk.Spotify;

internal class TokenStore
{
    private readonly CockroachDbContext dbContext;

    internal TokenStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task<IEnumerable<Token>> GetTokens()
    {
        var result = new List<Token>();
        await using var command = dbContext.CreateCommand("SELECT id, userId, accessToken, refreshToken, expiresAt FROM tokens");
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(new Token(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
            }
        }
        return result;
    }

    internal async Task UpdateToken(Token refreshedToken)
    {
        await using var command = dbContext.CreateCommand("UPDATE tokens SET accessToken = @accessToken, refreshToken = @refreshToken, expiresAt = @expiresAt WHERE id = @id");
        command.Parameters.AddWithValue("accessToken", refreshedToken.AccessToken);
        command.Parameters.AddWithValue("expiresAt", refreshedToken.ExpiresAt);
        command.Parameters.AddWithValue("refreshToken", refreshedToken.RefreshToken);
        command.Parameters.AddWithValue("id", refreshedToken.Id);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task SetUserForToken(Guid tokenId, string userId)
    {
        await using var command = dbContext.CreateCommand("UPDATE tokens SET userId = @userId WHERE id = @id");
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("id", tokenId);
        await command.ExecuteNonQueryAsync();
    }
}

internal record Token(Guid Id, string? UserId, string AccessToken, string RefreshToken, DateTime ExpiresAt)
{
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
}
