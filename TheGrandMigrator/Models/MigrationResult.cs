using System.Collections.Generic;
using TheGrandMigrator.Abstractions;

namespace TheGrandMigrator.Models
{
    public class MigrationResult<T> : IMigrationResult<T>
    {
        public int FetchedCount { get; private set; }
        public int SuccessCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int FailedCount { get; private set; }
        public List<string> ErrorMessages { get; }
        public string Message { get; set; }

        public void IncreaseFetched()
        {
            FetchedCount++;
        }
        
        public void IncreaseSuccess()
        {
            SuccessCount++;
        }
        
        public void IncreaseSkipped()
        {
            SkippedCount++;
        }
        
        public void IncreaseFailed()
        {
            FailedCount++;
        }

        public MigrationResult()
        {
            ErrorMessages = new List<string>();
        }
    }
}