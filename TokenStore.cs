using Npgsql;

namespace Synk.Spotify;

internal class TokenStore
{
    private readonly CockroachDbContext dbContext;

    internal TokenStore()
    {
        dbContext = new();
    }

    internal async Task<IEnumerable<Token>> GetTokens()
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("SELECT id, userId, accessToken, refreshToken, expiresAt FROM tokens", connection);

        using var reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return Enumerable.Empty<Token>();
        }

        var result = new List<Token>();
        while (await reader.ReadAsync())
        {
            result.Add(new Token(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4)));
        }
        return result;
    }

    internal Task UpdateToken(Token refreshedToken)
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("UPDATE tokens SET accessToken = @accessToken, expiresAt = @expiresAt WHERE id = @id", connection);
        command.Parameters.AddWithValue("accessToken", refreshedToken.AccessToken);
        command.Parameters.AddWithValue("expiresAt", refreshedToken.ExpiresAt);

        return command.ExecuteNonQueryAsync();
    }
}

internal record Token(int Id, string? UserId, string AccessToken, string RefreshToken, DateTime ExpiresAt)
{
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;
}
