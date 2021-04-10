namespace SendbirdHttpClient.Constants
{
	public static class Api
	{
		public const string BaseUrl          = "https://api-{0}.sendbird.com/v3/";
		public const string TokenHeader      = "Api-Token";
		public const string GroupChannelType = "group_channels";

		public static class Endpoints
		{
			public const string Users    = "users";
			public const string Channels = "channels";
			public const string Block    = "block";
			public const string Metadata = "metadata";
			public const string Freeze   = "freeze";
		}
	}
}
