using Newtonsoft.Json;

namespace SendbirdHttpClient.Models.User
{
	public class UserUpsertRequest
	{
		[JsonProperty("user_id")]
		public string UserId { get; set; }

		[JsonProperty("nickname")]
		public string Nickname { get; set; }

		[JsonProperty("profile_url")]
		public string ProfileImageUrl { get; set; }

		[JsonProperty("profile_file")]
		public byte[] ProfileImageFile { get; set; }

		[JsonProperty("issue_access_token")]
		public bool? IssueAccessToken { get; set; }

		[JsonProperty("issue_session_token")]
		public bool? IssueSessionToken { get; set; }

		// Unix epoc time.
		[JsonProperty("session_token_expires_at")]
		public long? SessionTokenExpiresAt { get; set; }

		// Unique string keys for a user for discovering friends.
		[JsonProperty("discovery_keys")]
		public string[] DiscoveryKeys { get; set; }

		[JsonProperty("is_active")]
		public bool? IsActive { get; set; }

		// Unix epoc time.
		[JsonProperty("last_seen_at")]
		public long? LastSeenAt { get; set; }

		[JsonProperty("preferred_languages")]
		public string[] PreferredLanguages { get; set; }

		// If set to true (default on SB's), a user will be kicked off all group channels if deactivated.
		[JsonProperty("leave_all_when_deactivated")]
		public bool? QuitAllChannelsOnDeactivation { get; set; }

		[JsonProperty("metadata")]
		public UserMetadata Metadata { get; set; }
	}
}
