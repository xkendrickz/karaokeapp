using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using KaraokeApp.Models;
using Newtonsoft.Json.Linq;

namespace KaraokeApp.Services
{
    /// <summary>
    /// Wraps YouTube Data API v3.
    /// Requires a Google API key with the YouTube Data API v3 enabled.
    /// Set the key in Settings → YouTube API Key.
    /// </summary>
    public class YoutubeService
    {
        private static readonly HttpClient _http = new HttpClient();

        private const string SearchUrl =
            "https://www.googleapis.com/youtube/v3/search" +
            "?part=snippet&type=video&maxResults=25" +
            "&q={0}&key={1}";

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Search YouTube and return Song objects (SourceType = "youtube").
        /// </summary>
        public async Task<List<Song>> SearchAsync(string query)
        {
            if (!HasApiKey)
                throw new InvalidOperationException(
                    "No YouTube API key configured. Please add your key in Settings → YouTube API Key.");

            string url = string.Format(SearchUrl,
                Uri.EscapeDataString(query), Uri.EscapeDataString(ApiKey));

            HttpResponseMessage response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            JObject root = JObject.Parse(json);

            var results = new List<Song>();
            foreach (JToken item in root["items"] ?? new JArray())
            {
                string videoId = item["id"]?["videoId"]?.ToString();
                if (string.IsNullOrEmpty(videoId)) continue;

                string title   = item["snippet"]?["title"]?.ToString() ?? "Unknown";
                string channel = item["snippet"]?["channelTitle"]?.ToString() ?? "";
                string thumb   = item["snippet"]?["thumbnails"]?["medium"]?["url"]?.ToString() ?? "";

                results.Add(new Song
                {
                    Title        = title,
                    Artist       = channel,
                    SourceType   = "youtube",
                    YoutubeId    = videoId,
                    YoutubeUrl   = "https://www.youtube.com/watch?v=" + videoId,
                    ThumbnailUrl = thumb
                });
            }
            return results;
        }

        /// <summary>
        /// Returns the embed URL for a YouTube video ID.
        /// autoplay=1 starts immediately; mute=0 keeps audio on.
        /// </summary>
        public static string GetEmbedUrl(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return string.Empty;
            return "https://www.youtube.com/embed/" + videoId +
                   "?autoplay=1&controls=1&rel=0&modestbranding=1";
        }

        /// <summary>
        /// Extract the video ID from a full YouTube URL.
        /// Supports: youtube.com/watch?v=ID and youtu.be/ID
        /// </summary>
        public static string ExtractVideoId(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var uri = new Uri(url);
                if (uri.Host.Contains("youtu.be"))
                    return uri.AbsolutePath.TrimStart('/');

                string query = uri.Query;
                int idx = query.IndexOf("v=", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    string id = query.Substring(idx + 2);
                    int ampIdx = id.IndexOf('&');
                    return ampIdx >= 0 ? id.Substring(0, ampIdx) : id;
                }
            }
            catch { }
            return null;
        }
    }
}
