using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TheGrandMigrator.Abstractions;
using CommandManager;
using CommandManager.Enums;
using Common.Extensions;
using TheGrandMigrator.Logging;
using TwilioHttpClient.Abstractions;

namespace SandBirdMigrationAttributes
{
    class Program
    {
        private static string _logFileName = $"{LoggingUtilities.LogFolder}/Migration_{{0}}_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";

        public static async Task Main(string[] args)
        {
			var options = Manager.Manage(args);
            if (options.IsEmpty) return;

			if (options.LogToFile)
			{
				_logFileName = String.Format(_logFileName, options.MigrationSubject.ToString("G"));
				LoggingUtilities.SetupLoggingToFiles(_logFileName);
			}

			try
			{
				ServiceProvider serviceProvider = InversionOfControl.Setup();

				IMigrator grandMigrator = serviceProvider.GetRequiredService<IMigrator>();

				LoggingUtilities.Log($"Starting migrations of {options.MigrationSubject:G} - {DateTime.Now.ToShortDateString()}.");

				var sw = new Stopwatch();
				sw.Start();
                IMigrationResult<IResource> migrationResult;
				switch (options.MigrationSubject)
				{
					case MigrationSubject.User:
						migrationResult = await grandMigrator.MigrateUsersAttributesAsync(options.DateBefore, options.DateAfter, options.ResourceLimit, options.PageSize);
						break;
					case MigrationSubject.Channel:
						migrationResult = String.IsNullOrWhiteSpace(options.ChannelUniqueIdentifier) ?
							await grandMigrator.MigrateChannelsAttributesAsync(options.DateBefore, options.DateAfter, options.ResourceLimit, options.PageSize) :
							await grandMigrator.MigrateSingleChannelAttributesAsync(options.DateBefore, options.DateAfter, options.ChannelUniqueIdentifier);
                        break;
					case MigrationSubject.Account:
						migrationResult = await grandMigrator.MigrateSingleAccountAttributesAsync(options.DateBefore, options.DateAfter, options.AccoutId, options.ResourceLimit, options.PageSize);
						break;
					default:
						LoggingUtilities.Log($"Unsupported migration entity {options.MigrationSubject:G}.");
						return;
				}
				sw.Stop();
				
				LoggingUtilities.Log($"Migration finished. Time elapsed: {sw.Elapsed.AsString()}. Results:");
				LoggingUtilities.LogFinalStatistics(migrationResult);
				
				if (migrationResult.TotalFailedCount == 0 && migrationResult.TotalFetchedCount > 0) return;

				LoggingUtilities.Log("The following messages were recorded during the migration:");
				LoggingUtilities.Log($"\t{migrationResult.Message}");
				foreach (string message in migrationResult.ErrorMessages) LoggingUtilities.Log($"\t{message}");
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"Exception happened: {ex.Message}.");
			}
        }
    }
}
