using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  SQL CONNECTOR — FAÇADE (Phase 3 refactoring)
    //
    //  The three concrete connectors now live in their own files:
    //    - PostgreSqlConnector.cs
    //    - MariaDbConnector.cs
    //    - SqlServerConnector.cs
    //
    //  All three implement IDbConnector (IDbConnector.cs).
    //
    //  This static class is kept ONLY for backward compatibility with any
    //  call-sites that still use the old static methods (e.g. ExportadorOffice,
    //  tests, or tooling). Each method simply delegates to the appropriate
    //  connector instance.
    //
    //  New code should prefer IDbConnector directly.
    // ════════════════════════════════════════════════════════════════════════
    public static class SqlConnector
    {
        // ── Connection helpers ────────────────────────────────────────────

        public static bool ProbarPostgreSQL(string cadena, out string mensaje)
        {
            try
            {
                var connector = new PostgreSqlConnector(cadena);
                mensaje = connector.TestConnectionAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex) { mensaje = ex.Message; return false; }
        }

        public static bool ProbarMariaDB(string cadena, out string mensaje)
        {
            try
            {
                var connector = new MariaDbConnector(cadena);
                mensaje = connector.TestConnectionAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex) { mensaje = ex.Message; return false; }
        }

        public static bool ProbarSqlServer(string cadena, out string mensaje)
        {
            try
            {
                var connector = new SqlServerConnector(cadena);
                mensaje = connector.TestConnectionAsync().GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex) { mensaje = ex.Message; return false; }
        }

        // ── Table listing ─────────────────────────────────────────────────

        public static List<string> ObtenerTablasPostgreSQL(string cadena)
        {
            try
            {
                var connector = new PostgreSqlConnector(cadena);
                return connector.GetTablesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.ObtenerTablasPostgreSQL] {ex.Message}");
                return new List<string>();
            }
        }

        public static List<string> ObtenerTablasMariaDB(string cadena)
        {
            try
            {
                var connector = new MariaDbConnector(cadena);
                return connector.GetTablesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.ObtenerTablasMariaDB] {ex.Message}");
                return new List<string>();
            }
        }

        public static List<string> ObtenerTablasSqlServer(string cadena)
        {
            try
            {
                var connector = new SqlServerConnector(cadena);
                return connector.GetTablesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.ObtenerTablasSqlServer] {ex.Message}");
                return new List<string>();
            }
        }

        // ── Data reading ──────────────────────────────────────────────────

        public static DataTable LeerTablaPostgreSQL(string cadena, string tabla,
            string? filtroWhere = null, int limite = 0)
        {
            string sql = BuildSelectSql($"\"{tabla}\"", filtroWhere, limite, limitKeyword: "LIMIT");
            try
            {
                var connector = new PostgreSqlConnector(cadena);
                return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable
                    ?? new DataTable();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.LeerTablaPostgreSQL] {ex.Message}");
                return new DataTable();
            }
        }

        public static DataTable LeerTablaMariaDB(string cadena, string tabla,
            string? filtroWhere = null, int limite = 0)
        {
            string sql = BuildSelectSql($"`{tabla}`", filtroWhere, limite, limitKeyword: "LIMIT");
            try
            {
                var connector = new MariaDbConnector(cadena);
                return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable
                   ?? new DataTable();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.LeerTablaMariaDB] {ex.Message}");
                return new DataTable();
            }
        }

        public static DataTable LeerTablaSqlServer(string cadena, string tabla,
            string? filtroWhere = null, int limite = 0)
        {
            // SQL Server uses TOP instead of LIMIT.
            string topClause = limite > 0 ? $"TOP {limite} " : string.Empty;
            string wherePart = !string.IsNullOrWhiteSpace(filtroWhere)
                ? $" WHERE {filtroWhere}" : string.Empty;
            string sql = $"SELECT {topClause}* FROM [{tabla}]{wherePart};";

            try
            {
                var connector = new SqlServerConnector(cadena);
                return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable
                    ?? new DataTable();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SqlConnector.LeerTablaSqlServer] {ex.Message}");
                return new DataTable();
            }
        }

        // ── Custom queries ────────────────────────────────────────────────

        public static DataTable EjecutarConsultaPostgreSQL(string cadena, string sql)
        {
            var connector = new PostgreSqlConnector(cadena);
            return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable
                ?? new DataTable();
        }

        public static DataTable EjecutarConsultaMariaDB(string cadena, string sql)
        {
            var connector = new MariaDbConnector(cadena);
            return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable
                ?? new DataTable();
        }

        public static DataTable EjecutarConsultaSqlServer(string cadena, string sql)
        {
            var connector = new SqlServerConnector(cadena);
            return connector.ExecuteAsync(sql).GetAwaiter().GetResult().DataTable

                ?? new DataTable();
        }

        // ── Bulk insert ───────────────────────────────────────────────────

        public static SqlWriteResult InsertarDataTablePostgreSQL(
            string cadena, string tabla, DataTable dt, IProgress<int>? progreso = null)
        {
            var connector = new PostgreSqlConnector(cadena);
            return connector.InsertDataTableAsync(dt, tabla, progreso)
                            .GetAwaiter().GetResult();
        }

        public static SqlWriteResult InsertarDataTableMariaDB(
            string cadena, string tabla, DataTable dt, IProgress<int>? progreso = null)
        {
            var connector = new MariaDbConnector(cadena);
            return connector.InsertDataTableAsync(dt, tabla, progreso)
                            .GetAwaiter().GetResult();
        }

        public static SqlWriteResult InsertarDataTableSqlServer(
            string cadena, string tabla, DataTable dt, IProgress<int>? progreso = null)
        {
            var connector = new SqlServerConnector(cadena);
            return connector.InsertDataTableAsync(dt, tabla, progreso)
                            .GetAwaiter().GetResult();
        }

        // ── CSV export ────────────────────────────────────────────────────

        public static string DataTableACsv(DataTable dt)
        {
            var sb = new StringBuilder();

            for (int c = 0; c < dt.Columns.Count; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(CsvHelper.EscapeField(dt.Columns[c].ColumnName));
                sb.Append('"');
            }
            sb.AppendLine();

            foreach (DataRow row in dt.Rows)
            {
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append('"');
                    sb.Append(CsvHelper.EscapeField(row[c]?.ToString() ?? string.Empty));
                    sb.Append('"');
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Private helper ────────────────────────────────────────────────

        private static string BuildSelectSql(
            string quotedTable,
            string? where,
            int limit,
            string limitKeyword)
        {
            var sb = new StringBuilder($"SELECT * FROM {quotedTable}");
            if (!string.IsNullOrWhiteSpace(where)) sb.Append($" WHERE {where}");
            if (limit > 0) sb.Append($" {limitKeyword} {limit}");
            sb.Append(';');
            return sb.ToString();
        }
    }
}