namespace SendbirdHttpClient.Models.Common
{
	public class MetadataUpsertRequest<T> where T : class
	{
		public T Metadata { get; set; }
	}
}
