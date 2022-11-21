using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecentFollowers
{
    public class Follower
    {
        //[JsonPropertyName("followed_at")]
        //public DateTime FollowedAt { get; set; }

        [JsonPropertyName("from_id")]
        public string FromId { get; set; }

        [JsonPropertyName("from_login")]
        public string FollowerId { get; set; }

        [JsonPropertyName("from_name")]
        public string FollowerName { get; set; }

        //[JsonPropertyName("to_id")]
        //public string ToId { get; set; }

        //[JsonPropertyName("to_login")]
        //public string ToLogin { get; set; }

        //[JsonPropertyName("to_name")]
        //public string ToName { get; set; }
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
