using System;
using CommandManager.Enums;

namespace CommandManager
{
    public class ExecutionOptions
    {
        public MigrationSubject MigrationSubject { get; set; }
        public int PageSize { get; set; }
        public int ResourceLimit { get; set; }
        public bool LogToFile { get; set; }
        public int AccoutId { get; set; }
        public DateTime? LaterThan { get; set; }
        public bool IsEmpty => MigrationSubject == MigrationSubject.Undefined && PageSize == 0;
        public static ExecutionOptions Empty => new ExecutionOptions { MigrationSubject = MigrationSubject.Undefined };
    }
}
