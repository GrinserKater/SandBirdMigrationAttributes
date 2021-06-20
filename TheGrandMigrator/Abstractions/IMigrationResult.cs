using System.Collections.Generic;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrationResult<T>
	{
		int UsersFetchedCount { get; }
		int ChannelsFetchedCount { get; }
		int UsersSuccessCount { get; }
		int ChannelsSuccessCount { get; }
		int UsersSkippedCount { get; }
		int ChannelsSkippedCount { get; }
		int UsersFailedCount { get; }
		int ChannelsFailedCount { get; }
		int TotalFetchedCount { get; }
		int TotalSuccessCount { get; }
		int TotalSkippedCount { get; }
		int TotalFailedCount { get; }
		List<string> ErrorMessages { get; }
		string Message { get; set; }
	}
}