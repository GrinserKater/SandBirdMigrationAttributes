using System;

namespace TwilioHttpClient.Models.Attributes.Channel
{
	public class DeletedBy
	{
		public int UserId { get; set; }
		public DateTime Date { get; set; }
	}
}