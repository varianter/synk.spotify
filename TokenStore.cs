namespace Synk.Spotify;

internal class TokenStore
{
    private readonly CockroachDbContext dbContext;
    private readonly Logger logger = new($"{nameof(TokenStore)}: ");

    internal TokenStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task<IEnumerable<Token>> GetTokens()
    {
        logger.LogInfo("Getting tokens from database.");
        var result = new List<Token>();
        await using var command = dbContext.CreateCommand("SELECT id, userId, accessToken, refreshToken FROM tokens");
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                result.Add(new Token(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
        }
        logger.LogInfo($"Retrieved {result.Count} tokens from database.");
        return result;
    }

    internal async Task UpdateToken(Token refreshedToken)
    {
        logger.LogInfo("Updating token in database.");
        await using var command = dbContext.CreateCommand("UPDATE tokens SET accessToken = @accessToken, refreshToken = @refreshToken WHERE id = @id");
        command.Parameters.AddWithValue("accessToken", refreshedToken.AccessToken);
        command.Parameters.AddWithValue("refreshToken", refreshedToken.RefreshToken);
        command.Parameters.AddWithValue("id", refreshedToken.Id);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("Token updated in database.");
    }

    internal async Task SetUserForToken(Guid tokenId, string userId)
    {
        logger.LogInfo("Setting user for token in database.");
        await using var command = dbContext.CreateCommand("UPDATE tokens SET userId = @userId WHERE id = @id");
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("id", tokenId);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("User set for token in database.");
    }
}

internal record Token(Guid Id, string? UserId, string AccessToken, string RefreshToken);
