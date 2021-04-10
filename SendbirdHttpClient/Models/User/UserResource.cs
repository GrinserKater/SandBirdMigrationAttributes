using Newtonsoft.Json;

namespace SendbirdHttpClient.Models.User
{
	public class UserResource
	{
		[JsonProperty("user_id")]
		public string UserId { get; set; }

		[JsonProperty("nickname")]
		public string Nickname { get; set; }

		[JsonProperty("unread_message_count")]
		public int UnreadMessageCount { get; set; }

		[JsonProperty("profile_url")]
		public string ProfileImageUrl { get; set; }

		[JsonProperty("access_token")]
		public string AccessToken { get; set; }

		[JsonProperty("session_tokens")]
		public UserSessionToken[] SessionTokens { get; set; }

		[JsonProperty("is_online")]
		public bool IsOnline { get; set; }

		[JsonProperty("is_active")]
		public bool IsActive { get; set; }

		// Unix epoc time.
		[JsonProperty("created_at")]
		public long CreatedAt { get; set; }

		// Unix epoc time.
		[JsonProperty("last_seen_at")]
		public long LastSeenAt { get; set; }

		// Unique string keys for a user for discovering friends.
		[JsonProperty("discovery_keys")]
		public string[] DiscoveryKeys { get; set; }

		[JsonProperty("preferred_languages")]
		public string[] PreferredLanguages { get; set; }

		[JsonProperty("has_ever_logged_in")]
		public bool HasEverLoggedIn { get; set; }

		[JsonProperty("metadata")]
		public UserMetadata Metadata { get; set; }
	}
}
