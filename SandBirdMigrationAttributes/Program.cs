using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TheGrandMigrator.Abstractions;
using CommandManager;
using CommandManager.Enums;

namespace SandBirdMigrationAttributes
{
    class Program
    {
		private static string _logFileName = $"Migration_{{0}}_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";

		public static async Task Main(string[] args)
        {
	        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

			var options = Manager.Manage(args);

			if (options.IsEmpty) return;

			if (options.LogToFile)
			{
				_logFileName = String.Format(_logFileName, options.MigrationSubject.ToString("G"));
				Trace.Listeners.Add(new TextWriterTraceListener(System.IO.File.CreateText(_logFileName)));
			}

			try
			{
				ServiceProvider serviceProvider = InversionOfControl.Setup();

				IMigrator grandMigrator = serviceProvider.GetRequiredService<IMigrator>();

				Trace.WriteLine($"Starting migrations of {options.MigrationSubject:G} - {DateTime.Now.ToShortDateString()}.");

				var sw = new Stopwatch();
				sw.Start();

				IMigrationResult migrationResult;

				switch (options.MigrationSubject)
				{
					case MigrationSubject.User:
						migrationResult = await grandMigrator.MigrateUsersAttributesAsync(options.ResourceLimit, options.PageSize);
						break;
					case MigrationSubject.Channel:
						migrationResult = await grandMigrator.MigrateChannelsAttributesAsync(options.ResourceLimit, options.PageSize);
						break;
					default:
						Trace.WriteLine($"Unsupported migration entity {options.MigrationSubject:G}.");
						return;
				}

				sw.Stop();

				Trace.WriteLine($"Migration finished. Time elapsed: {sw.ElapsedMilliseconds}. Results:");
				Trace.WriteLine(
					$"\tTotal fetched from Twilio: {migrationResult.FetchedCount}; migrated: {migrationResult.SuccessCount} entities; failed {migrationResult.FailedCount}.");

				if (migrationResult.FailedCount == 0) return;

				Trace.WriteLine(migrationResult.Message);
				foreach (string message in migrationResult.ErrorMessages) Trace.WriteLine($"{message}");
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
