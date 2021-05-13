using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TheGrandMigrator.Abstractions;
using CommandManager;
using CommandManager.Enums;
using SandBirdMigrationAttributes.Logging;
using TwilioHttpClient.Abstractions;

namespace SandBirdMigrationAttributes
{
    class Program
    {
        private static string _logFileName = $"{LoggingUtilities.LogFolder}\\Migration_{{0}}_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";

        public static async Task Main(string[] args)
        {
	        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

			var options = Manager.Manage(args);
            if (options.IsEmpty) return;

			if (options.LogToFile)
			{
				Directory.CreateDirectory(LoggingUtilities.LogFolder);
				_logFileName = String.Format(_logFileName, options.MigrationSubject.ToString("G"));
				Trace.Listeners.Add(new TextWriterTraceListener(File.CreateText(_logFileName)));
			}

			try
			{
				ServiceProvider serviceProvider = InversionOfControl.Setup();

				IMigrator grandMigrator = serviceProvider.GetRequiredService<IMigrator>();

				Trace.WriteLine($"Starting migrations of {options.MigrationSubject:G} - {DateTime.Now.ToShortDateString()}.");

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
						Trace.WriteLine($"Unsupported migration entity {options.MigrationSubject:G}.");
						return;
				}
				sw.Stop();

				LoggingUtilities.WriteMigrationResultLogFiles(migrationResult);
				Trace.WriteLine($"Migration finished. Time elapsed: {sw.Elapsed.Seconds}s. Results:");
				Trace.WriteLine(
					$"\tTotal fetched from Twilio: {migrationResult.FetchedCount}; migrated: {migrationResult.SuccessCount}; skipped: {migrationResult.SkippedCount}; failed {migrationResult.FailedCount}.");

				if (migrationResult.FailedCount == 0) return;

                Trace.WriteLine("The following messages were recorded during the migration:");
				Trace.WriteLine($"\t{migrationResult.Message}");
				foreach (string message in migrationResult.ErrorMessages) Trace.WriteLine($"\t{message}");
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"Exception happened: {ex.Message}.");
			}
			finally
			{
				Trace.Flush();
			}
        }
    }
}
