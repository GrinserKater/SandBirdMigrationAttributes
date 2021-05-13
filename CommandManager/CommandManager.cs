using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CommandManager.Enums;

namespace CommandManager
{
    public class Manager
    {
        public static void ShowUsageLine()
        {
			var usageHint = new StringBuilder("Usage: SandBirdMigrationAttributes ");
			usageHint.AppendLine($"--{MigrationSubject.User:G} | --{MigrationSubject.Channel:G} | --{MigrationSubject.Account:G} <user ID>");
			usageHint.AppendLine("Optional arguments:");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.PageSizeArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.LimitArgument} | --{Constants.CommandLineParameters.AllArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.LogToFileArgument}]");
			usageHint.AppendLine($"\t[--{Constants.CommandLineParameters.BeforeArgument} <date> --{Constants.CommandLineParameters.AfterArgument}] <date>");
			Trace.WriteLine(usageHint);
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
				ShowUsageLine();
				return ExecutionOptions.Empty;
			}

			var options = new ExecutionOptions
			{
				MigrationSubject = (MigrationSubject)Enum.Parse(typeof(MigrationSubject), migrationSubject, true)
			};

			if (options.MigrationSubject == MigrationSubject.Account)
			{
				options.AccoutId = ExtractNextPositionIntegerParameter(arguments, MigrationSubject.Account.ToString(), null, null);
				if (options.AccoutId == 0)
				{
					ShowUsageLine();
					return ExecutionOptions.Empty;
				}
			}

            if (options.MigrationSubject == MigrationSubject.Channel)
                options.ChannelUniqueIdentifier = ExtractNextPositionStringParameter(arguments, MigrationSubject.Channel.ToString());

            options.DateBefore = ExtractNextPositionDateTimeParameter(arguments, Constants.CommandLineParameters.BeforeArgument);
            options.DateAfter = ExtractNextPositionDateTimeParameter(arguments, Constants.CommandLineParameters.AfterArgument);
            if (options.DateBefore.HasValue && options.DateAfter.HasValue && options.DateBefore == options.DateAfter)
            {
                ShowUsageLine();
                return ExecutionOptions.Empty;
            }

            int pageSize = ExtractNextPositionIntegerParameter(arguments, Constants.CommandLineParameters.PageSizeArgument, Constants.Limits.MaxAllowedPageSize,
				Constants.Limits.DefaultPageSize);
			int resourceLimit = ExtractNextPositionIntegerParameter(arguments, Constants.CommandLineParameters.LimitArgument, null, Constants.Limits.DefaultLimit);
			string logToFile = arguments.ElementAtOrDefault(Array.IndexOf(arguments, Constants.CommandLineParameters.LogToFileArgument));

			if (arguments.Contains(Constants.CommandLineParameters.AllArgument)) resourceLimit = 0;

			options.PageSize = pageSize;
			options.ResourceLimit = resourceLimit;
			options.LogToFile = !String.IsNullOrWhiteSpace(logToFile);
            return options;
		}

        private static int ExtractNextPositionIntegerParameter(string[] arguments, string parameterName, int? maxLimit, int? defaultValue)
        {
	        int result = Int32.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1), out int value) ? value : maxLimit ?? defaultValue ?? 0;
            if (maxLimit.HasValue && !defaultValue.HasValue) return result;
            return result > maxLimit ? defaultValue.Value : result;
        }

        private static DateTime? ExtractNextPositionDateTimeParameter(string[] arguments, string parameterName)
        {
	        if(!DateTime.TryParse(arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1), out DateTime value)) return null;
            return value;
        }

        private static string ExtractNextPositionStringParameter(string[] arguments, string parameterName)
        {
            string result = arguments.ElementAtOrDefault(Array.IndexOf(arguments, parameterName.ToLower()) + 1);
			if (String.IsNullOrWhiteSpace(result) || result.StartsWith("--")) return String.Empty;
			return result;
        }
	}
}
