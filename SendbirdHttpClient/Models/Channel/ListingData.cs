namespace SendbirdHttpClient.Models.Channel
{
	public class ListingData
	{
		public int Id { get; set; }
		public string Title { get; set; }
		public int State { get; set; }
		public FormattedInfoData FormattedPrice { get; set; }
		public FormattedInfoData Location { get; set; }
	}
}
