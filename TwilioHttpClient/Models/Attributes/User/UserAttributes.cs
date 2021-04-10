using System;
using System.Linq;

namespace TwilioHttpClient.Models.Attributes.User
{
	public class UserAttributes
	{
		public int[] BlockedUsers { get; set; }
		public DateTime? BlockedByAdminAt { get; set; }

		public override string ToString()
		{
			var blockedUsers = BlockedUsers?.Select(i => i.ToString());

			string blockedByAdminAtString = $"BlockedByAdminAt: {(BlockedByAdminAt == null ? "null" : BlockedByAdminAt.ToString())}";

			string blockedUsersString = $"BlockedUsers: [{(blockedUsers == null ? "null" : String.Join(", ", blockedUsers))}]";

			return $"{blockedByAdminAtString}; {blockedUsersString}";
		}
	}
}
