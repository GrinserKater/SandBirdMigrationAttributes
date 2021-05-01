using TwilioHttpClient.Abstractions;

namespace TwilioHttpClient.Models
{
	public class UserChannel : IResource
	{
		public string ChannelSid { get; set; }

		public override string ToString()
		{
			return ChannelSid;
		}
	}
}
