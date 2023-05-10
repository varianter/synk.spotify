namespace Synk.Spotify;

internal class UserStore
{
    private readonly CockroachDbContext dbContext;
    private readonly Logger logger = new Logger("UserStore: ");

    internal UserStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    internal async Task<UserInfo?> GetUserInfo(string userId)
    {
        logger.LogInfo("Getting user info for " + userId);
        await using var command = dbContext.CreateCommand("SELECT id, lastSync FROM users WHERE id = @userId");
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!reader.HasRows)
        {
            logger.LogWarning("User not found.");
            return null;
        }
        await reader.ReadAsync();
        var userInfo = new UserInfo(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1));
        if (await reader.ReadAsync())
        {
            logger.LogWarning("Found more than one user with id " + userId);
        }
        return userInfo;
    }

    internal async Task CreateUser(string id)
    {
        logger.LogInfo("Creating user " + id);
        await using var command = dbContext.CreateCommand("INSERT INTO users (id) VALUES (@id)");
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("User created.");
    }

    internal async Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        logger.LogInfo("Updating last sync time for user " + id);
        await using var command = dbContext.CreateCommand("UPDATE users SET lastSync = @lastSync WHERE id = @id");
        command.Parameters.AddWithValue("lastSync", lastSyncTime);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("Updated last sync time.");
    }
}

internal record UserInfo(string UserId, DateTime? LastSync);
