using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  IDB CONNECTOR
    //  Common contract for PostgreSQL, MariaDB and SQL Server connectors.
    //  SqlViewerForm and SqlConnector (façade) work against this interface.
    // ════════════════════════════════════════════════════════════════════════
    internal interface IDbConnector
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>Display name shown in the UI (e.g. "PostgreSQL").</summary>
        string DisplayName { get; }

        // ── Connection ────────────────────────────────────────────────────

        /// <summary>
        /// Opens a connection and returns a human-readable success message,
        /// or throws on failure so the caller can display the error.
        /// </summary>
        /// <returns>E.g. "Conexión exitosa · Servidor: localhost · DB: mydb"</returns>
        Task<string> TestConnectionAsync(CancellationToken ct = default);

        // ── Schema ────────────────────────────────────────────────────────

        /// <summary>Returns the names of all user tables in the connected database.</summary>
        Task<List<string>> GetTablesAsync(CancellationToken ct = default);

        // ── Query ─────────────────────────────────────────────────────────

        /// <summary>
        /// Executes an arbitrary SQL statement.
        /// For SELECT / WITH / SHOW statements returns a populated DataTable and 0 rows affected.
        /// For DDL / DML returns null for the DataTable and the actual rows affected.
        /// </summary>
        Task<(DataTable? DataTable, int RowsAffected)> ExecuteAsync(
            string sql,
            CancellationToken ct = default);

        // ── Bulk insert ───────────────────────────────────────────────────

        /// <summary>
        /// Creates <paramref name="tableName"/> if it does not exist, then
        /// bulk-inserts all rows from <paramref name="data"/>.
        /// Reports insertion progress (0-100) via <paramref name="progress"/>.
        /// </summary>
        Task<SqlWriteResult> InsertDataTableAsync(
            DataTable data,
            string tableName,
            IProgress<int>? progress = null,
            CancellationToken ct = default);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DB CONNECTOR TYPE ENUM
    //  Used by SqlViewerForm to track which backend is active.
    // ════════════════════════════════════════════════════════════════════════
    internal enum DbConnectorType
    {
        None,
        PostgreSql,
        MariaDb,
        SqlServer
    }
}