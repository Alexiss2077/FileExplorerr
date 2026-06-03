using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  MARIADB CONNECTOR
    //  Implements IDbConnector for MariaDB / MySQL via MySqlConnector.
    //  All logic previously lived inside the static SqlConnector class.
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class MariaDbConnector : IDbConnector
    {
        private readonly string _connectionString;

        public MariaDbConnector(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty.",
                    nameof(connectionString));
            _connectionString = connectionString;
        }

        // ── IDbConnector ──────────────────────────────────────────────────

        public string DisplayName => "MariaDB";

        public async Task<string> TestConnectionAsync(CancellationToken ct = default)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new MySqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync(ct);
            return $"Conexión exitosa · Servidor: {conn.DataSource} · DB: {conn.Database}";
        }

        public async Task<List<string>> GetTablesAsync(CancellationToken ct = default)
        {
            var tables = new List<string>();
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new MySqlCommand("SHOW TABLES;", conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tables.Add(reader.GetString(0));

            return tables;
        }

        public async Task<(DataTable? DataTable, int RowsAffected)> ExecuteAsync(
            string sql,
            CancellationToken ct = default)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new MySqlCommand(sql, conn) { CommandTimeout = 60 };

            if (IsSelectStatement(sql))
            {
                var dt = new DataTable();
                var adapter = new MySqlDataAdapter(cmd);
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
                await using var conn = new MySqlConnection(_connectionString);
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
                                $"[MariaDbConnector] Row insert error: {ex.Message}");
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
                result.Message = $"✅ {ok} filas insertadas en `{tableName}`. Errores: {errors}.";
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
                    $"[MariaDbConnector.InsertDataTableAsync] {ex.Message}");
            }
            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static async Task EnsureTableExistsAsync(
            MySqlConnection conn,
            string tableName,
            DataTable data,
            CancellationToken ct)
        {
            var cols = string.Join(", ",
                data.Columns.Cast<DataColumn>()
                    .Select(c => $"`{SanitiseColumn(c.ColumnName)}` {MyType(c.DataType)}"));

            string ddl =
                $"CREATE TABLE IF NOT EXISTS `{tableName}` ({cols}) " +
                "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            await using var cmd = new MySqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static async Task InsertRowAsync(
            MySqlConnection conn,
            MySqlTransaction tx,
            string tableName,
            DataTable data,
            DataRow row,
            CancellationToken ct)
        {
            var colNames = data.Columns.Cast<DataColumn>()
                .Select(c => $"`{SanitiseColumn(c.ColumnName)}`");
            var paramNames = Enumerable.Range(1, data.Columns.Count)
                .Select(i => $"@p{i}");

            string sql =
                $"INSERT INTO `{tableName}` ({string.Join(",", colNames)}) " +
                $"VALUES ({string.Join(",", paramNames)});";

            await using var cmd = new MySqlCommand(sql, conn, tx);
            for (int i = 0; i < data.Columns.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i + 1}", row[i] ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static string MyType(Type t)
        {
            if (t == typeof(long) || t == typeof(int)) return "BIGINT";
            if (t == typeof(double) || t == typeof(float) ||
                t == typeof(decimal)) return "DOUBLE";
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