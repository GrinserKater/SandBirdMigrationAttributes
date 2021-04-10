using Newtonsoft.Json;

namespace SendbirdHttpClient.Models.User
{
	public class UserBlockRequest
	{
		[JsonIgnore]
		public int OriginatorUserId { get; set; }

		[JsonProperty("user_ids")]
		public int[] UserIdsToBlock { get; set; }
	}
}
