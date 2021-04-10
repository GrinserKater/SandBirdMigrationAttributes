using Newtonsoft.Json;

namespace SendbirdHttpClient.Models
{
	public class Failure
	{
		[JsonProperty("error")]
		public bool Error { get; set; }

		[JsonProperty("message")]
		public string Message { get; set; }

		[JsonProperty("code")]
		public int Code { get; set; }
	}
}
