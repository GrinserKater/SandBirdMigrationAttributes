using System;
using System.Diagnostics;
using System.Linq;
using CommandManager.Enums;

namespace CommandManager
{
    public class Manager
    {
        private static readonly string UsageHint =
            $"\tUsage: SandBirdMigrationAttributes --{MigrationSubject.User:G} | --{MigrationSubject.Channel:G} [--{Constants.CommandLineParameters.PageSizeArgument}] [--{Constants.CommandLineParameters.LimitArgument} | --{Constants.CommandLineParameters.AllArgument}] [--{Constants.CommandLineParameters.LogToFileArgument}]";

        public static void ShowUsageLine()
        {
            Trace.WriteLine(UsageHint);
        }

        public static ExecutionOptions Manage(string[] args)
        {
			if (args == null || args.Length == 0)
			{
				ShowUsageLine();
				return  ExecutionOptions.Empty;
			}

			string[] arguments = args.Select(a => a.Trim('-').ToLower()).ToArray();
			var migrationSubjects = Enum.GetNames(typeof(MigrationSubject));

			var migrationSubject = arguments.FirstOrDefault(a => migrationSubjects.Any(ms => ms.ToLower() == a.ToLower()));

			if (String.IsNullOrWhiteSpace(migrationSubject))
			{
				Manager.ShowUsageLine();
				return ExecutionOptions.Empty;
			}

			var options = new ExecutionOptions
			{
				MigrationSubject = (MigrationSubject)Enum.Parse(typeof(MigrationSubject), migrationSubject, true)
			};

			int pageSize =
				Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.PageSizeArgument) + 1), out int size) &&
				size <= Constants.Limits.MaxAllowedPageSize ? size : Constants.Limits.DefaultPageSize;

			int resourceLimit = Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.LimitArgument) + 1), out int limit) ?
				limit : Constants.Limits.DefaultLimit;

			if (arguments.Contains(Constants.CommandLineParameters.AllArgument)) resourceLimit = 0;

			string logToFile = arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.LogToFileArgument));

			options.PageSize = pageSize;
			options.ResourceLimit = resourceLimit;
			options.LogToFile = !String.IsNullOrWhiteSpace(logToFile);

			return options;
		}
	}
}
