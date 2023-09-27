using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Synk.Spotify;

namespace synk.spotify
{
    public class FillTrackArtists
    {
        private readonly ILogger _logger;
        private readonly MusicStore musicStore;
        private readonly SpotifyApi spotifyApi;
        private readonly TokenStore tokenStore;
        private readonly TokenRefresher tokenRefresher;

        public FillTrackArtists(ILoggerFactory loggerFactory, MusicStore musicStore, SpotifyApi spotifyApi, TokenStore tokenStore, TokenRefresher tokenRefresher)
        {
            _logger = loggerFactory.CreateLogger<FillTrackArtists>();
            this.musicStore = musicStore;
            this.spotifyApi = spotifyApi;
            this.tokenStore = tokenStore;
            this.tokenRefresher = tokenRefresher;
        }

        [Function("FillTrackArtists")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var trackIds = await musicStore.GetTrackIdsWithoutArtists();
            _logger.LogInformation("Found {tracks} tracks without artists.", trackIds.Count());
            var token = (await tokenStore.GetTokens()).Skip(1).FirstOrDefault() ?? throw new Exception("No token found.");
            spotifyApi.SetAccessToken(token.AccessToken);
            foreach (var trackId in trackIds)
            {
                Track spotifyTrack;
                try
                {
                    spotifyTrack = await spotifyApi.GetTrackAsync(trackId);
                }
                catch (SpotifyUnauthorizedException)
                {
                    _logger.LogInformation("Token expired. Refreshing.");
                    token = await tokenRefresher.RefreshTokenAsync(token);
                    await tokenStore.UpdateToken(token ?? throw new Exception("Failed to refresh token."));
                    spotifyApi.SetAccessToken(token.AccessToken);
                    spotifyTrack = await spotifyApi.GetTrackAsync(trackId);
                }
                foreach (var artist in spotifyTrack.artists)
                {
                    if (await musicStore.IsArtistMissing(artist.id))
                    {
                        await musicStore.CreateArtist(artist);
                    }
                }
                _logger.LogInformation("Found artists for track {track}.", spotifyTrack.name);
                await musicStore.LinkTrackToArtists(spotifyTrack);
                _logger.LogInformation("Added links artists for track {track}.", spotifyTrack.name);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions!");

            return response;
        }
    }
}
