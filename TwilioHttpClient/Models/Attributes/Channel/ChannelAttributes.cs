namespace TwilioHttpClient.Models.Attributes.Channel
{
	public class ChannelAttributes
	{
		public int ListingId { get; set; }
		public int SellerId { get; set; }
		public int BuyerId { get; set; }
		public bool IsBlocked { get; set; }
		public bool IsListingBlocked { get; set; }
		public DeletedBy[] ChannelDeletedBy { get; set; }
		public Listing Listing { get; set; }
	}
}
