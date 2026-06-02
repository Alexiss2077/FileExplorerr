using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  POSTGRESQL CONNECTOR
    //  Implements IDbConnector for PostgreSQL via Npgsql.
    //  All logic previously lived inside the static SqlConnector class.
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class PostgreSqlConnector : IDbConnector
    {
        private readonly string _connectionString;

        public PostgreSqlConnector(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.",
                    nameof(connectionString));
            _connectionString = connectionString;
        }

        // ── IDbConnector ──────────────────────────────────────────────────

        public string DisplayName => "PostgreSQL";

        public async Task<string> TestConnectionAsync(CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync(ct);
            return $"Conexión exitosa · Servidor: {conn.Host} · DB: {conn.Database}";
        }

        public async Task<List<string>> GetTablesAsync(CancellationToken ct = default)
        {
            var tables = new List<string>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql =
                "SELECT tablename FROM pg_tables " +
                "WHERE schemaname = 'public' ORDER BY tablename;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tables.Add(reader.GetString(0));

            return tables;
        }

        public async Task<(DataTable? DataTable, int RowsAffected)> ExecuteAsync(
            string sql,
            CancellationToken ct = default)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 60 };

            if (IsSelectStatement(sql))
            {
                var dt = new DataTable();
                var adapter = new NpgsqlDataAdapter(cmd);
                await Task.Run(() => adapter.Fill(dt), ct);
                return (dt, 0);
            }

            int rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
            return (null, rowsAffected);
        }

        public async Task<SqlWriteResult> InsertDataTableAsync(
            DataTable data,
            string tableName,
            IProgress<int>? progress = null,
            CancellationToken ct = default)
        {
            var result = new SqlWriteResult();
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                await EnsureTableExistsAsync(conn, tableName, data, ct);

                int total = data.Rows.Count;
                int ok = 0;
                int errors = 0;

                await using var tx = await conn.BeginTransactionAsync(ct);
                try
                {
                    foreach (DataRow row in data.Rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            await InsertRowAsync(conn, tx, tableName, data, row, ct);
                            ok++;
                            if (ok % 100 == 0 && total > 0)
                                progress?.Report((int)(ok * 100.0 / total));
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            System.Diagnostics.Debug.WriteLine(
                                $"[PostgreSqlConnector] Row insert error: {ex.Message}");
                        }
                    }
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    throw;
                }

                progress?.Report(100);
                result.Success = true;
                result.Inserted = ok;
                result.Errors = errors;
                result.Message = $"✅ {ok} filas insertadas en \"{tableName}\". Errores: {errors}.";
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Message = "❌ Operación cancelada.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"❌ {ex.Message}";
                System.Diagnostics.Debug.WriteLine(
                    $"[PostgreSqlConnector.InsertDataTableAsync] {ex.Message}");
            }
            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static async Task EnsureTableExistsAsync(
            NpgsqlConnection conn,
            string tableName,
            DataTable data,
            CancellationToken ct)
        {
            var cols = string.Join(", ",
                data.Columns.Cast<DataColumn>()
                    .Select(c => $"\"{SanitiseColumn(c.ColumnName)}\" {PgType(c.DataType)}"));

            string ddl = $"CREATE TABLE IF NOT EXISTS \"{tableName}\" ({cols});";
            await using var cmd = new NpgsqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static async Task InsertRowAsync(
            NpgsqlConnection conn,
            NpgsqlTransaction tx,
            string tableName,
            DataTable data,
            DataRow row,
            CancellationToken ct)
        {
            var colNames = data.Columns.Cast<DataColumn>()
                .Select(c => $"\"{SanitiseColumn(c.ColumnName)}\"");
            var paramNames = Enumerable.Range(1, data.Columns.Count)
                .Select(i => $"@p{i}");

            string sql =
                $"INSERT INTO \"{tableName}\" ({string.Join(",", colNames)}) " +
                $"VALUES ({string.Join(",", paramNames)});";

            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            for (int i = 0; i < data.Columns.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i + 1}", row[i] ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static string PgType(Type t)
        {
            if (t == typeof(long) || t == typeof(int)) return "BIGINT";
            if (t == typeof(double) || t == typeof(float) ||
                t == typeof(decimal)) return "DOUBLE PRECISION";
            return "TEXT";
        }

        private static string SanitiseColumn(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "col";
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            string s = sb.ToString().TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            return string.IsNullOrEmpty(s) ? "col" : s.ToLowerInvariant();
        }

        private static bool IsSelectStatement(string sql)
        {
            string trimmed = sql.TrimStart();
            return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("SHOW", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("DESCRIBE", StringComparison.OrdinalIgnoreCase);
        }
    }
}