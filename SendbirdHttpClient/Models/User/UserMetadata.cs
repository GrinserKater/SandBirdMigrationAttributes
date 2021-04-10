using System;

namespace SendbirdHttpClient.Models.User
{
	// All values of Sendbird metadata must be strings.
	public sealed class UserMetadata
	{
		public UserMetadata()
		{
			BlockedByAdminAt = String.Empty;
		}

		public UserMetadata(UserMetadata metadata)
		{
			if(metadata == null) return;

			BlockedByAdminAt = metadata.BlockedByAdminAt;
		}

		public string BlockedByAdminAt { get; set; }
	}
}
