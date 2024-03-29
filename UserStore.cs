namespace Synk.Spotify;

public class UserStore
{
    private readonly CockroachDbContext dbContext;
    private readonly Logger logger = new("UserStore: ");

    public UserStore(CockroachDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<UserInfo?> GetUserInfo(string userId)
    {
        logger.LogInfo("Getting user info for " + userId);
        await using var command = dbContext.CreateCommand("SELECT id, last_sync FROM users WHERE id = @userId");
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

    public async Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        logger.LogInfo("Updating last sync time for user " + id);
        await using var command = dbContext.CreateCommand("UPDATE users SET last_sync = @lastSync WHERE id = @id");
        command.Parameters.AddWithValue("lastSync", lastSyncTime);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
        logger.LogInfo("Updated last sync time.");
    }
}

public record UserInfo(string UserId, DateTime? LastSync);
