using Npgsql;

namespace Synk.Spotify;

public class CockroachDbContext
{
    private readonly string connectionString;
    public CockroachDbContext()
    {
        connectionString = Environment.GetEnvironmentVariable("COCKROACHDB_CONNECTION_STRING")
            ?? throw new("COCKROACHDB_CONNECTION_STRING environment variable not set");
    }

    public NpgsqlConnection GetConnection() => new(connectionString);
}
