using System.Collections.Generic;

namespace SendbirdHttpClient.Models.Channel
{
	public class ChannelData
	{
		public bool IsListingBlocked { get; set; }
		public IEnumerable<DeletedByData> ChannelDeletedBy { get; set; }
		public ListingData Listing { get; set; }

	}
}
