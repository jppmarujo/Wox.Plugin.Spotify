using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SpotifyAPI.Local;
using SpotifyAPI.Local.Enums;
using SpotifyAPI.Local.Models;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;

namespace Wox.Plugin.Spotify
{
    public class SpotifyApi
    {
        private readonly SpotifyLocalAPI _localSpotify;
        private SpotifyWebAPI _spotifyApi;
        private readonly object _lock = new object();


        public SpotifyApi(string pluginDir = null)
        {
            var pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
            CacheFolder = Path.Combine(pluginDirectory, "Cache");

            // Create the cache folder, if it doesn't already exist
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);

            _localSpotify = new SpotifyLocalAPI();
            _localSpotify.OnTrackChange += (o, e) => CurrentTrack = e.NewTrack;
            _localSpotify.OnPlayStateChange += (o, e) => IsPlaying = e.Playing;
            ConnectToSpotify();

            _spotifyApi = new SpotifyWebAPI();
        }
        
        public bool IsPlaying { get; set; }

        public bool IsMuted => _localSpotify.IsSpotifyMuted();

        public Track CurrentTrack { get; private set; }

        private string CacheFolder { get; }

        public bool IsConnected { get; private set; }

        public bool IsWebApiCOnnected => !string.IsNullOrEmpty(_spotifyApi.AccessToken);

        public bool IsRunning => SpotifyLocalAPI.IsSpotifyRunning() && SpotifyLocalAPI.IsSpotifyWebHelperRunning();

        public void Play()
        {
            _localSpotify.Play();
        }

        public void Play(string uri)
        {
            _localSpotify.PlayURL(uri);
        }

        public void Pause()
        {
            _localSpotify.Pause();
        }

        public void Skip()
        {
            _localSpotify.Skip();
        }

        public void ToggleMute()
        {
            if (_localSpotify.IsSpotifyMuted())
            {
                _localSpotify.UnMute();
            }
            else
            {
                _localSpotify.Mute();
            }
        }

        public void RunSpotify()
        {
            if (!IsRunning)
                SpotifyLocalAPI.RunSpotify();
        }

        public async Task ConnectWebApi()
        {
            var webApiFactory = new WebAPIFactory("http://localhost", 8000, "3e271cd3f0634b92a991f60601f9db44", Scope.None, TimeSpan.FromSeconds(40));
            _spotifyApi = await webApiFactory.GetWebApi();
        }

        public IEnumerable<FullArtist> GetArtists(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Artist, 10);

                return searchItems.Artists.Items;
            }
        }

        public IEnumerable<FullAlbum> GetAlbums(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Album, 10);

                var results = _spotifyApi.GetSeveralAlbums(searchItems.Albums.Items.Select(a => a.Id).ToList());

                return results.Albums;
            }
        }

        public IEnumerable<FullTrack> GetTracks(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Track, 10);

                return searchItems.Tracks.Items;
            }
        }

        public Task<string> GetArtworkAsync(SimpleAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullArtist artist) => GetArtworkAsync(artist.Images, artist.Uri);

        public Task<string> GetArtworkAsync(FullTrack track) => GetArtworkAsync(track.Album);

        public Task<string> GetArtworkAsync(Track track)
        {
            var albumArtUrl = track.GetAlbumArtUrl(AlbumArtSize.Size160);

            return GetArtworkAsync(albumArtUrl, track.TrackResource.Uri);
        }

        private Task<string> GetArtworkAsync(List<Image> images, string uri)
        {
            if (!images.Any())
                return null;

            var url = images.Last().Url;

            return GetArtworkAsync(url, uri);
        }

        private async Task<string> GetArtworkAsync(string url, string resourceUri)
        {
            // use the unique spotify ID as the local file name
            var uniqueId = GetUniqueIdForArtwork(resourceUri);

            return await DownloadImageAsync(uniqueId, url);
        }

        private static string GetUniqueIdForArtwork(string uri) => uri.Substring(uri.LastIndexOf(":", StringComparison.Ordinal) + 1);

        public void ConnectToSpotify()
        {
            if (!IsRunning)
            {
                return;
            }
            
            var successful = _localSpotify.Connect();
            if (successful)
            {
                UpdateInfos();
                _localSpotify.ListenForEvents = true;
            }
        }

        private void UpdateInfos()
        {
            var status = _localSpotify.GetStatus();

            if (status?.Track != null) //Update track infos
            {
                CurrentTrack = status.Track;
                IsPlaying = status.Playing;
                IsConnected = true;
            }
        }
        
        private async Task<string> DownloadImageAsync(string uniqueId, string url)
        {
            // local path to the image file, located in the Cache folder
            var path = $@"{CacheFolder}\{uniqueId}.jpg";

            if (File.Exists(path))
            {
                return path;
            }

            using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync(new Uri(url), path);
            }

            return path;
        }
    }
}