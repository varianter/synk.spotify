using Npgsql;

namespace Synk.Spotify;

internal class UserStore
{
    private readonly CockroachDbContext dbContext;

    internal UserStore()
    {
        dbContext = new();
    }

    internal async Task<UserInfo?> GetUserInfo(string userId)
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("SELECT id, accessToken, refreshToken, expiresAt, lastSync FROM users WHERE id = @userId", connection);
        command.Parameters.AddWithValue("userId", userId);

        using var reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return null;
        }
        if (reader.RecordsAffected is not 1)
        {
            throw new Exception("More than one user with the same id");
        }

        await reader.ReadAsync();
        return new UserInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3), reader.IsDBNull(4) ? null : reader.GetDateTime(4));
    }

    internal Task CreateUser(string id)
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("INSERT INTO users (id) VALUES (@id)", connection);
        command.Parameters.AddWithValue("id", id);

        return command.ExecuteNonQueryAsync();
    }

    internal Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        using var connection = dbContext.GetConnection();

        var command = new NpgsqlCommand("UPDATE users SET lastSync = @lastSync WHERE id = @id", connection);
        command.Parameters.AddWithValue("lastSync", lastSyncTime);
        command.Parameters.AddWithValue("id", id);

        return command.ExecuteNonQueryAsync();
    }
}

internal record UserInfo(string UserId, string AccessToken, string RefreshToken, DateTime ExpiresAt, DateTime? LastSync);
