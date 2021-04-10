using System.Collections.Generic;

namespace TheGrandMigrator.Abstractions
{
	public interface IMigrationResult
	{
		int FetchedCount { get; }
		int SuccessCount { get; }
		int FailedCount { get; }
		List<string> ErrorMessages { get; }
		string Message { get; set; }
	}
}
