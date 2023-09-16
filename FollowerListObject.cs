using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecentFollowers
{
    public class Follower
    {
        [JsonPropertyName("user_id")]
        public string FromId { get; set; }

        [JsonPropertyName("user_name")]
        public string FollowerName { get; set; }

        [JsonPropertyName("user_login")]
        public string FollowerId { get; set; }

        [JsonPropertyName("followed_at")]
        public DateTime FollowedAt { get; set; }
    }

    public class Pagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    public class FollowerListObject
    {
        [JsonPropertyName("data")]
        public List<Follower> Data { get; set; }

        [JsonPropertyName("pagination")]
        public Pagination Pagination { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }
}
