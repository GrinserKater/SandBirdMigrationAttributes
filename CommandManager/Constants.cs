﻿namespace CommandManager
{
    public static class Constants
    {
        public class Limits
        {
            public const int DefaultPageSize = 100;
            public const int MaxAllowedPageSize = 1000;
            public const int DefaultLimit = 50;
        }

        public class CommandLineParameters
        {
            public const string PageSizeArgument = "pagesize";
            public const string LimitArgument = "limit";
            public const string AllArgument = "all";
            public const string LogToFileArgument = "logtofile";
        }
    }
}