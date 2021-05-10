using System;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models.Attributes.Channel;

namespace TwilioHttpClient.Models
{
	public class Channel : IResource
	{
		public string UniqueName { get; set; }
		public string FriendlyName { get; set; }
        public int MembersCount { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }

        public ChannelAttributes Attributes { get; set; }

        public override string ToString()
        {
            return UniqueName;
        }
    }
}
