using Newtonsoft.Json;

namespace SendbirdHttpClient.Models.Channel
{
	public class ChannelUpsertRequest
	{
		[JsonProperty("user_ids")]
		public int[] UserIds { get; set; }
		[JsonProperty("channel_url")]
		public string ChannelUrl { get; set; }
		[JsonProperty("name")]
		public string Name { get; set; }
		
		[JsonProperty("cover_url")] 
		public string CoverUrl { get; set; }

		[JsonProperty("data")]
		public ChannelData Data { get; set; }

		[JsonProperty("freeze")]
		public bool Freeze { get; set; }

		[JsonProperty("created_by")]
		public int CreatedBy { get; set; }
	}
}
