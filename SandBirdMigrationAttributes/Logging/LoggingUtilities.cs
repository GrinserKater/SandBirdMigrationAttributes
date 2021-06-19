using System;
using System.IO;
using System.Linq;
using TheGrandMigrator.Abstractions;
using TwilioHttpClient.Abstractions;

namespace SandBirdMigrationAttributes.Logging
{
    public static class LoggingUtilities
    {
        private static readonly string SuccessLogFileName = $"{LogFolder}/successfull_entities_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
        private static readonly string FailedLogFileName = $"{LogFolder}/failed_entities_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
        private static readonly string SkippedLogFileName = $"{LogFolder}/skipped_entities_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.log";
        
        private static readonly string DefaultMainlogFileName = $"{LogFolder}/Migration_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";

        private static bool _logToFile;
        private static string _mainLogFileName;

        public const string LogFolder = "Logs";

        static LoggingUtilities()
        {
            Directory.CreateDirectory(LogFolder);
        }

        public static SetupLoggingToFiles(string logFileName = null)
        {
            _mainLogFileName = String.IsNullOrWhiteSpace(logFileName) ? DefaultMainlogFileName : logFileName;
        }
        
        public static void Log(string logMessage, )

        public static void WriteMigrationResultLogFiles(IRichMigrationResult<IResource> result)
        {
            if (result.SuccessCount > 0) File.AppendAllLines(SuccessLogFileName, result.EntitiesSucceeded.Select(e => e.ToString()).ToArray());

            if (result.FailedCount > 0) File.AppendAllLines(FailedLogFileName, result.EntitiesFailed.Select(e => e.ToString()).ToArray());

            if (result.SkippedCount > 0) File.AppendAllLines(SkippedLogFileName, result.EntitiesSkipped.Select(e => e.ToString()).ToArray());
        }
    }
}
