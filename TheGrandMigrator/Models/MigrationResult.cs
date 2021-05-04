using System.Collections.Generic;
using TheGrandMigrator.Abstractions;

namespace TheGrandMigrator.Models
{
	public class MigrationResult<T> : IMigrationResult
	{
		public bool IsFailure => ErrorMessages.Count > 0;
		public int FetchedCount => EntitiesFetched.Count; 
		public int SuccessCount => EntitiesSucceeded.Count;
		public int FailedCount => EntitiesFailed.Count;
		public List<T> EntitiesFetched { get; }
		public List<T> EntitiesSucceeded { get; }
		public List<T> EntitiesFailed { get; }
		public List<string> ErrorMessages { get; }
		public string Message { get; set; }

		public MigrationResult()
		{
			EntitiesFetched   = new List<T>();
			EntitiesSucceeded = new List<T>();
			EntitiesFailed    = new List<T>();
			ErrorMessages     = new List<string>();
		}
	}
}
