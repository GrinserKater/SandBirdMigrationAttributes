using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SandBirdMigrationAttributes.Logging.Enums;
using TheGrandMigrator.Abstractions;
using TwilioHttpClient.Abstractions;

namespace TheGrandMigrator.Logging
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

        public static void SetupLoggingToFiles(string mainLogFileName = null)
        {
            _logToFile = true;
            _mainLogFileName = String.IsNullOrWhiteSpace(mainLogFileName) ? DefaultMainlogFileName : mainLogFileName;
            Directory.CreateDirectory(LogFolder);
        }

        public static void Log(string logMessage, string entityUniqueIdentifier = null, EntityProcessingResult processingResult = EntityProcessingResult.Undefined)
        {
            if (String.IsNullOrWhiteSpace(logMessage)) return;
            
            Trace.WriteLine(logMessage);
            if (!_logToFile) return;
            
            WriteLogFileLine(_mainLogFileName, logMessage);

            if (String.IsNullOrWhiteSpace(entityUniqueIdentifier) || processingResult == EntityProcessingResult.Undefined) return;
            LogEntityProcessingResultToFile(entityUniqueIdentifier, processingResult);
        }
        
        public static void LogEntityProcessingResultToFile(string entityUniqueIdentifier, EntityProcessingResult processingResult)
        {
            if (!_logToFile) return;

            if (String.IsNullOrWhiteSpace(entityUniqueIdentifier)) return;
            switch (processingResult)
            {
                case EntityProcessingResult.Skipped:
                    WriteLogFileLine(SkippedLogFileName, entityUniqueIdentifier);
                    break;
                case EntityProcessingResult.Failure:
                    WriteLogFileLine(FailedLogFileName, entityUniqueIdentifier);
                    break;
                case EntityProcessingResult.Success:
                    WriteLogFileLine(SuccessLogFileName, entityUniqueIdentifier);
                    break;
            }
        }

        public static void WriteMigrationResultLogFiles(IRichMigrationResult<IResource> result)
        {
            if (result.SuccessCount > 0) File.AppendAllLines(SuccessLogFileName, result.EntitiesSucceeded.Select(e => e.ToString()).ToArray());

            if (result.FailedCount > 0) File.AppendAllLines(FailedLogFileName, result.EntitiesFailed.Select(e => e.ToString()).ToArray());

            if (result.SkippedCount > 0) File.AppendAllLines(SkippedLogFileName, result.EntitiesSkipped.Select(e => e.ToString()).ToArray());
        }

        public static void LogFinalStatistics(IMigrationResult<IResource> migrationResult)
        {
            StringBuilder finalStats = new StringBuilder();
            finalStats.AppendLine("Final results of the migration:");
            finalStats.AppendLine($"\ttotal fetched from Twilio: {migrationResult.TotalFetchedCount}:");
            finalStats.AppendLine($"\t\tusers fetched from Twilio: {migrationResult.UsersFetchedCount};");
            finalStats.AppendLine($"\t\tchannels fetched from Twilio: {migrationResult.ChannelsFetchedCount};");
            finalStats.AppendLine($"\ttotal succeeded: {migrationResult.TotalSuccessCount}:");
            finalStats.AppendLine($"\t\tusers succeeded: {migrationResult.UsersSuccessCount};");
            finalStats.AppendLine($"\t\tchannels succeeded: {migrationResult.ChannelsSuccessCount};");
            finalStats.AppendLine($"\ttotal skipped: {migrationResult.TotalSkippedCount}:");
            finalStats.AppendLine($"\t\tusers skipped: {migrationResult.UsersSkippedCount};");
            finalStats.AppendLine($"\t\tchannels skipped: {migrationResult.ChannelsSkippedCount};");
            finalStats.AppendLine($"\ttotal failed: {migrationResult.TotalFailedCount}:");
            finalStats.AppendLine($"\t\tusers failed: {migrationResult.UsersFailedCount};");
            finalStats.AppendLine($"\t\tchannels failed: {migrationResult.ChannelsFailedCount}.");
            Log(finalStats.ToString());
        }

        private static void WriteLogFileLine(string logFileName, string record)
        {
            using StreamWriter sw = File.AppendText(logFileName);
            sw.WriteLine(record);
        }
    }
}
