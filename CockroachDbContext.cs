using Microsoft.Extensions.Options;
using Npgsql;

namespace Synk.Spotify;

public sealed class CockroachDbContext : IDisposable, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly NpgsqlDataSource db;

    public CockroachDbContext(IOptions<CockroachConfiguration> options)
    {
        var configuration = options.Value;
        connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = configuration.Host,
            Port = 26257,
            SslMode = SslMode.VerifyFull,

            Username = configuration.User,
            Password = configuration.Password,
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
