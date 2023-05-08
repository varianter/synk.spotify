using Npgsql;

namespace Synk.Spotify;

public sealed class CockroachDbContext : IDisposable, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly NpgsqlDataSource db;

    public CockroachDbContext()
    {
        var host = Environment.GetEnvironmentVariable("COCKROACHDB_HOST")
            ?? throw new("COCKROACHDB_HOST environment variable not set");
        var user = Environment.GetEnvironmentVariable("COCKROACHDB_USER")
            ?? throw new("COCKROACHDB_USER environment variable not set");
        var password = Environment.GetEnvironmentVariable("COCKROACHDB_PASSWORD")
            ?? throw new("COCKROACHDB_PASSWORD environment variable not set");

        connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = 26257,
            SslMode = SslMode.VerifyFull,

            Username = user,
            Password = password,
            Database = "synk",
            ApplicationName = "synk.spotify"
        }.ToString();

        db = NpgsqlDataSource.Create(connectionString);
    }

    public void Dispose()
    {
        db.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return db.DisposeAsync();
    }

    public NpgsqlCommand CreateCommand(string? commandText = null)
    {
        return db.CreateCommand(commandText);
    }

}
