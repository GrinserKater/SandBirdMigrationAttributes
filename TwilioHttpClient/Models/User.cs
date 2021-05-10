using System;
using TwilioHttpClient.Abstractions;
using TwilioHttpClient.Models.Attributes.User;

namespace TwilioHttpClient.Models
{
	public class User : IResource
	{
		public string Id { get; set; }
		public string FriendlyName { get; set; }
		public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
		public string ProfileImageUrl { get; set; }
		public UserAttributes Attributes { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
