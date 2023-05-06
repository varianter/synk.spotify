namespace Synk.Spotify;

public class UserStore
{
    public Task<UserInfo> GetUserInfo(string userId)
    {
        throw new NotImplementedException();
    }

    internal Task UpdateLastSync(string id, DateTime lastSyncTime)
    {
        throw new NotImplementedException();
    }
}

public record UserInfo(string UserId, string AccessToken, string RefreshToken, DateTime ExpiresAt, DateTime? LastSync);
