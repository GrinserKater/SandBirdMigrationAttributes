using Newtonsoft.Json;

namespace SendbirdHttpClient.Models.User
{
	public class UserSessionToken
	{
		[JsonProperty("session_token")]
		public string Token { get; set; }

		// Unix epoc time.
		[JsonProperty("expires_at")]
		public long ExpiresAt { get; set; }
	}
}
