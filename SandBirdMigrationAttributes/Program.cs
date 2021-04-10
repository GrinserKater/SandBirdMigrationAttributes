using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SendbirdHttpClient.Extensions;
using TwilioHttpClient.Extensions;
using TheGrandMigrator;
using TheGrandMigrator.Abstractions;

namespace SandBirdMigrationAttributes
{
    class Program
    {
		private const int DefaultPageSize    = 100;
		private const int MaxAllowedPageSize = 1000;
		private const int DefaultLimit       = 50;

		private const string PageSizeArgument        = "pagesize";
		private const string LimitArgument           = "limit";
		private const string AllArgument             = "all";
		private const string LogToFileArgument       = "logtofile";
		private const string MigrationSubjectUser    = "users";
		private const string MigrationSubjectChannel = "channels";

		private static readonly string UsageHint =
			$"\tSandBirdMigrationAttributes --{MigrationSubjectUser} | --{MigrationSubjectChannel} [--{PageSizeArgument}] [--{LimitArgument} | --{AllArgument}] [--{LogToFileArgument}]";
		
		private static string _logFileName = $"Migration_{{0}}_log_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";
		public static async Task Main(string[] args)
        {
	        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

			if (args.Length == 0)
			{
				Trace.WriteLine("No arguments were provided. Usage:");
				Trace.WriteLine(UsageHint);
				return;
			}

	        string[] arguments = args.Select(a => a.Trim('-').ToLower()).ToArray();

			string migrationSubject = arguments.ElementAtOrDefault(Array.IndexOf(arguments, MigrationSubjectUser));
			if(String.IsNullOrWhiteSpace(migrationSubject))
				migrationSubject = arguments.ElementAtOrDefault(Array.IndexOf(arguments, MigrationSubjectChannel));

			if (String.IsNullOrWhiteSpace(migrationSubject))
			{
				Trace.WriteLine("No migration subject was provided. Usage:");
				Trace.WriteLine(UsageHint);
				return;
			}

			int pageSize =
				Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, PageSizeArgument) + 1), out int size) && size <= MaxAllowedPageSize ?
				size :
				DefaultPageSize;

			int resourceLimit = Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, LimitArgument) + 1), out int limit) ? limit : DefaultLimit;

			if(arguments.Contains(AllArgument)) resourceLimit = 0;

			string logToFile = arguments.ElementAtOrDefault(Array.IndexOf(arguments, LogToFileArgument));

			if (!String.IsNullOrWhiteSpace(logToFile))
			{
				_logFileName = String.Format(_logFileName, migrationSubject);
				Trace.Listeners.Add(new TextWriterTraceListener(System.IO.File.CreateText(_logFileName)));
			}

			try
			{
				ServiceProvider serviceProvider = new ServiceCollection()
					.AddSendbirdHttpClient()
					.AddTwilioClient()
					.AddSingleton<IMigrator, Migrator>()
					.BuildServiceProvider();

				IMigrator grandMigrator = serviceProvider.GetRequiredService<IMigrator>();

				Trace.WriteLine($"Starting migrations of {migrationSubject} - {DateTime.Now.ToShortDateString()}.");

				var sw = new Stopwatch();

				sw.Start();

				IMigrationResult migrationResult;

				switch (migrationSubject)
				{
					case MigrationSubjectUser:
						migrationResult = await grandMigrator.MigrateUsersAttributesAsync(resourceLimit, pageSize);
						break;
					case MigrationSubjectChannel:
						migrationResult = await grandMigrator.MigrateChannelsAttributesAsync(resourceLimit, pageSize);
						break;
					default:
						Trace.WriteLine($"Unsupported migration entity {migrationSubject}.");
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
