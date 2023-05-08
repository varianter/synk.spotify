namespace Synk.Spotify;

internal class UserStore
{
    private readonly CockroachDbContext dbContext;

    internal UserStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task<UserInfo?> GetUserInfo(string userId)
    {
        await using var command = dbContext.CreateCommand("SELECT id, lastSync FROM users WHERE id = @userId");
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            return null;
        }
        await reader.ReadAsync();
        return new UserInfo(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
        // TODO: Verify no other rows
    }

    internal async Task CreateUser(string id)
    {
        await using var command = dbContext.CreateCommand("INSERT INTO users (id) VALUES (@id)");
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    internal async Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        await using var command = dbContext.CreateCommand("UPDATE users SET lastSync = @lastSync WHERE id = @id");
        command.Parameters.AddWithValue("lastSync", lastSyncTime);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }
}

internal record UserInfo(string UserId, DateTime? LastSync);
