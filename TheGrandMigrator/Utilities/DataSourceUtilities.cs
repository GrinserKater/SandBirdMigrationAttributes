using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace TheGrandMigrator.Utilities
{
    public static class DataSourceUtilities
    {
        private const int DefaultBatchSize = 100;
        
        public static IEnumerable<IEnumerable<string>> ReadBatchFromFile(string fileName, int? batchSize = null)
        {
            if (String.IsNullOrWhiteSpace(fileName)) yield break;
            int currentBatchSize = batchSize ?? DefaultBatchSize;

            List<string> batch = new List<string>(currentBatchSize);
            foreach (string line in File.ReadLines(fileName))
            {
                batch.Add(line);
                if (batch.Count == currentBatchSize)
                {
                    yield return batch;
                    batch.Clear();
                }
            }

            yield return batch;
        }
        
        public static IEnumerable<string> ReadFromFile(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName)) yield break;
            
            foreach (string line in File.ReadLines(fileName))
            {
                yield return line;
            }
        }

        public static string[] ReadAllFromFile(string fileName, out int lineCount)
        {
            lineCount = 0;
            if (String.IsNullOrWhiteSpace(fileName)) return new string[0];
            var lines = File.ReadAllLines(fileName);
            lineCount = lines.Length;
            return lines;
        }
    }
}