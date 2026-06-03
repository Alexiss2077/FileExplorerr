namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  SQL WRITE RESULT
    //  Returned by IDbConnector.InsertDataTable() to report bulk-insert outcome.
    //  Previously defined inside SqlDataItem.cs alongside the unused SqlDataItem.
    //
    //  NOTE: SqlDataItem (Dictionary<string,string> wrapper) was removed because
    //  no code in the project ever reads or writes it. If it is needed in the
    //  future it can be re-added here.
    // ════════════════════════════════════════════════════════════════════════
    public sealed class SqlWriteResult
    {
        /// <summary>Whether the operation completed without a fatal error.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable outcome message shown to the user.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Number of rows successfully inserted.</summary>
        public int Inserted { get; set; }

        /// <summary>Number of rows that failed to insert.</summary>
        public int Errors { get; set; }
    }
}