using Newtonsoft.Json;
namespace SendbirdHttpClient.Models.Channel
{
	public class ChannelResource
	{
		public string Name { get; set; }

		[JsonProperty("channel_url")]
		public string ChannelUrl { get; set; }
		
		[JsonProperty("cover_url")]
		public string CoverUrl { get; set; }
		
		[JsonProperty("data")]
		public ChannelData Data { get; set; }

		[JsonProperty("member_count")]
		public int MemberCount { get; set; }
		
		[JsonProperty("joined_member_count")]
		public int JoinedMemberCount { get; set; }
		
		[JsonProperty("freeze")]
		public bool Freeze { get; set; }
		
		[JsonProperty("unread_message_count")]
		public int UnreadMessageCount { get; set; }
		
		[JsonProperty("max_length_message")]
		public int MaxLengthMessage { get; set; }
		
		[JsonProperty("created_at")]
		public long CreatedAt { get; set; }
	}
}
