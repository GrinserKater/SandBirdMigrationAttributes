using System.Collections.Generic;

namespace TwilioHttpClient.Models.Attributes.Channel
{
	public class Listing
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public Dictionary<string, string> FormattedPrice { get; set; }
		public Dictionary<string, string> Location { get; set; }
		public string MainPicture { get; set; }
		public int State { get; set; }
	}
}
