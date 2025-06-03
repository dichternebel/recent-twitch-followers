﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RecentFollowers
{
    public class Stream
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("user_login")]
        public string UserLogin { get; set; }

        [JsonPropertyName("user_name")]
        public string UserName { get; set; }

        [JsonPropertyName("game_id")]
        public string GameId { get; set; }

        [JsonPropertyName("game_name")]
        public string GameName { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("viewer_count")]
        public int ViewerCount { get; set; }

        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string ThumbnailUrl { get; set; }

        [JsonPropertyName("tag_ids")]
        public List<string> TagIds { get; set; }

        [JsonPropertyName("is_mature")]
        public bool IsMature { get; set; }
    }

    public class StreamPagination
    {
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; }
    }

    public class StreamObject
    {
        [JsonPropertyName("data")]
        public List<Stream> Streams { get; set; }

        [JsonPropertyName("pagination")]
        public StreamPagination Pagination { get; set; }
    }
}
