using TwilioHttpClient.Models.Attributes.User;

namespace TwilioHttpClient.Models
{
	public class User
	{
		public string Id { get; set; }
		public string FriendlyName { get; set; }
		public UserAttributes Attributes { get; set; }
	}
}
