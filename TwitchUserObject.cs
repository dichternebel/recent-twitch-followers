using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecentFollowers
{
    public class TwitchUser
    {
        [JsonPropertyName("broadcaster_type")]
        public string BroadcasterType { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("login")]
        public string Login { get; set; }

        [JsonPropertyName("offline_image_url")]
        public string OfflineImageUrl { get; set; }

        [JsonPropertyName("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("view_count")]
        public int ViewCount { get; set; }
    }

    public class TwitchUserObject
    {
        [JsonPropertyName("data")]
        public List<TwitchUser> Data { get; set; }
    }
}
