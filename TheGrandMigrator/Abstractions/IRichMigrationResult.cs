using System.Collections.Generic;

namespace TheGrandMigrator.Abstractions
{
    public interface IRichMigrationResult<T> : IMigrationResult<T>
    {
        List<T> EntitiesFetched { get; }
        List<T> EntitiesSucceeded { get; }
        List<T> EntitiesFailed { get; }
        List<T> EntitiesSkipped { get; }
    }
}