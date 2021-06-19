using System.Collections.Generic;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrationResult<T>
	{
		int FetchedCount { get; }
		int SuccessCount { get; }
		int SkippedCount { get; }
		int FailedCount { get; }
		List<string> ErrorMessages { get; }
		string Message { get; set; }
	}
}