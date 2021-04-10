using TwilioHttpClient.Models.Attributes.Channel;

namespace TwilioHttpClient.Models
{
	public class Channel
	{
		public string UniqueName { get; set; }
		public string FriendlyName { get; set; }

		public ChannelAttributes Attributes { get; set; }
	}
}
