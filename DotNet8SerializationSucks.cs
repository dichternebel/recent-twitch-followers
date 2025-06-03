using System.Text.Json.Serialization;

namespace RecentFollowers
{
    [JsonSerializable(typeof(Follower))]
    [JsonSerializable(typeof(FollowerListObject))]
    [JsonSerializable(typeof(FollowerPagination))]
    [JsonSerializable(typeof(RefreshTokenResponse))]
    [JsonSerializable(typeof(Stream))]
    [JsonSerializable(typeof(StreamObject))]
    [JsonSerializable(typeof(StreamPagination))]
    [JsonSerializable(typeof(TwitchUser))]
    [JsonSerializable(typeof(TwitchUserObject))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public partial class JsonContext : JsonSerializerContext { }
}