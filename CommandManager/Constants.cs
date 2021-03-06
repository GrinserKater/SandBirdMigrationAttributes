namespace CommandManager
{
    public static class Constants
    {
        public static class Limits
        {
            public const int DefaultPageSize = 100;
            public const int MaxAllowedPageSize = 1000;
            public const int DefaultLimit = 100;
            public const int MaxLimitOfBlockedUsers = 20;
        }

        public static class CommandLineParameters
        {
            public const string PageSizeArgument = "pagesize";
            public const string LimitArgument = "limit";
            public const string AllArgument = "all";
            public const string LogToFileArgument = "logtofile";
            public const string AfterArgument = "after";
            public const string BeforeArgument = "before";
            public const string FromFileArgument = "fromfile";
            public const string ExperimentalArgument = "experimental";
        }
    }
}
