namespace Synk.Spotify;

public class TokenStore
{
    private readonly CockroachDbContext dbContext;
    private readonly Logger logger = new($"{nameof(TokenStore)}: ");

    private const string SynkUserId = "31sybnmjumjvhac63y4ctdigtfea";


    public TokenStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public Task<IEnumerable<Token>> GetListenerTokens()
    {
        return GetTokens($"WHERE user_id != '{SynkUserId}'");
    }

    private async Task<IEnumerable<Token>> GetTokens(string? filter = null)
    {
        logger.LogInfo("Getting tokens from database.");
        var result = new List<Token>();
        await using var command = dbContext.CreateCommand($"SELECT id, user_id, access_token, refresh_token FROM tokens {filter}");
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

    public async Task UpdateToken(Token refreshedToken)
    {
        logger.LogInfo("Updating token in database.");
        await using var command = dbContext.CreateCommand("UPDATE tokens SET access_token = @accessToken, refresh_token = @refreshToken WHERE id = @id");
        command.Parameters.AddWithValue("accessToken", refreshedToken.AccessToken);
        command.Parameters.AddWithValue("refreshToken", refreshedToken.RefreshToken);
        command.Parameters.AddWithValue("id", refreshedToken.Id);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("Token updated in database.");
    }

    public async Task SetUserForToken(Guid tokenId, string userId)
    {
        logger.LogInfo("Setting user for token in database.");
        await using var command = dbContext.CreateCommand("UPDATE tokens SET user_id = @userId WHERE id = @id");
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("id", tokenId);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("User set for token in database.");
    }

    internal Task<Token> GetSynkTokens()
    {
        logger.LogInfo("Getting Synk tokens from database.");
        return GetTokens($"WHERE user_id = '{SynkUserId}'").ContinueWith(t => t.Result.Single());
    }
}

public record Token(Guid Id, string? UserId, string AccessToken, string RefreshToken);
