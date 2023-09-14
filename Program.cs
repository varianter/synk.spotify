using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synk.Spotify;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((host, services) =>
    {
        services.AddOptions<SpotifyConfiguration>()
            .BindConfiguration("Spotify");
        services.AddOptions<CockroachConfiguration>()
            .BindConfiguration("CockroachDB");

        services.AddScoped<SpotifyApi>();
        services.AddScoped<CockroachDbContext>();
        services.AddScoped<TokenStore>();
        services.AddScoped<UserStore>();
        services.AddScoped<MusicStore>();
        services.AddScoped<PlayedTracksStore>();
        services.AddScoped<TokenRefresher>();
        services.AddScoped<PlayedTracksFetcher>();
    })
    .Build();

host.Run();
