using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

// ── Npgsql (PostgreSQL) ─────────────────────────────────────────────────────
using Npgsql;

// ── MySqlConnector (MariaDB / MySQL) ────────────────────────────────────────
using MySqlConnector;

namespace FileExplorerr
{
    /// <summary>
    /// Proporciona acceso unificado a PostgreSQL y MariaDB.
    /// Permite listar tablas, leer datos, exportar CSV y escribir filas.
    /// </summary>
    public static class SqlConnector
    {
        // ════════════════════════════════════════════════════════════════════
        //  LISTAR TABLAS
        // ════════════════════════════════════════════════════════════════════

        public static List<string> ObtenerTablasPostgreSQL(string cadena)
        {
            var lista = new List<string>();
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "SELECT table_name FROM information_schema.tables " +
                    "WHERE table_schema='public' AND table_type='BASE TABLE' " +
                    "ORDER BY table_name;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read()) lista.Add(r.GetString(0));
            }
            catch { }
            return lista;
        }

        public static List<string> ObtenerTablasMariaDB(string cadena)
        {
            var lista = new List<string>();
            try
            {
                using var conn = new MySqlConnection(cadena);
                conn.Open();
                using var cmd = new MySqlCommand(
                    "SELECT table_name FROM information_schema.tables " +
                    "WHERE table_schema=DATABASE() AND table_type='BASE TABLE' " +
                    "ORDER BY table_name;", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read()) lista.Add(r.GetString(0));
            }
            catch { }
            return lista;
        }

        // ════════════════════════════════════════════════════════════════════
        //  LEER DATOS → DataTable
        // ════════════════════════════════════════════════════════════════════

        public static DataTable LeerTablaPostgreSQL(string cadena, string tabla,
            string? filtroWhere = null, int limite = 0)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                conn.Open();

                var sql = new StringBuilder($"SELECT * FROM \"{tabla}\"");
                if (!string.IsNullOrWhiteSpace(filtroWhere))
                    sql.Append($" WHERE {filtroWhere}");
                if (limite > 0) sql.Append($" LIMIT {limite}");
                sql.Append(';');

                using var cmd = new NpgsqlCommand(sql.ToString(), conn);
                cmd.CommandTimeout = 120;
                using var adapter = new NpgsqlDataAdapter(cmd);
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PG] LeerTabla: {ex.Message}");
            }
            return dt;
        }

        public static DataTable LeerTablaMariaDB(string cadena, string tabla,
            string? filtroWhere = null, int limite = 0)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new MySqlConnection(cadena);
                conn.Open();

                var sql = new StringBuilder($"SELECT * FROM `{tabla}`");
                if (!string.IsNullOrWhiteSpace(filtroWhere))
                    sql.Append($" WHERE {filtroWhere}");
                if (limite > 0) sql.Append($" LIMIT {limite}");
                sql.Append(';');

                using var cmd = new MySqlCommand(sql.ToString(), conn);
                cmd.CommandTimeout = 120;
                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MD] LeerTabla: {ex.Message}");
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  EJECUTAR SQL PERSONALIZADO → DataTable
        // ════════════════════════════════════════════════════════════════════

        public static DataTable EjecutarConsultaPostgreSQL(string cadena, string sql)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                conn.Open();
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.CommandTimeout = 120;
                using var adapter = new NpgsqlDataAdapter(cmd);
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                throw new Exception($"[PostgreSQL] {ex.Message}", ex);
            }
            return dt;
        }

        public static DataTable EjecutarConsultaMariaDB(string cadena, string sql)
        {
            var dt = new DataTable();
            try
            {
                using var conn = new MySqlConnection(cadena);
                conn.Open();
                using var cmd = new MySqlCommand(sql, conn);
                cmd.CommandTimeout = 120;
                using var adapter = new MySqlDataAdapter(cmd);
                adapter.Fill(dt);
            }
            catch (Exception ex)
            {
                throw new Exception($"[MariaDB] {ex.Message}", ex);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PROBAR CONEXIÓN
        // ════════════════════════════════════════════════════════════════════

        public static bool ProbarPostgreSQL(string cadena, out string mensaje)
        {
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                conn.Open();
                using var cmd = new NpgsqlCommand("SELECT 1;", conn);
                cmd.ExecuteScalar();
                mensaje = $"Conexión exitosa · Servidor: {conn.Host} · DB: {conn.Database}";
                return true;
            }
            catch (Exception ex) { mensaje = ex.Message; return false; }
        }

        public static bool ProbarMariaDB(string cadena, out string mensaje)
        {
            try
            {
                using var conn = new MySqlConnection(cadena);
                conn.Open();
                using var cmd = new MySqlCommand("SELECT 1;", conn);
                cmd.ExecuteScalar();
                mensaje = $"Conexión exitosa · Servidor: {conn.DataSource} · DB: {conn.Database}";
                return true;
            }
            catch (Exception ex) { mensaje = ex.Message; return false; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  EXPORTAR DataTable → CSV en memoria
        // ════════════════════════════════════════════════════════════════════

        public static string DataTableACsv(DataTable dt)
        {
            var sb = new StringBuilder();
            // Cabecera
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append('"');
                sb.Append(dt.Columns[c].ColumnName.Replace("\"", "\"\""));
                sb.Append('"');
            }
            sb.AppendLine();
            // Filas
            foreach (DataRow row in dt.Rows)
            {
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    string val = row[c]?.ToString() ?? "";
                    sb.Append('"');
                    sb.Append(val.Replace("\"", "\"\""));
                    sb.Append('"');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  INSERTAR DataTable → tabla SQL  (CREATE IF NOT EXISTS + INSERT)
        // ════════════════════════════════════════════════════════════════════

        public static SqlWriteResult InsertarDataTablePostgreSQL(
            string cadena, string tabla, DataTable dt,
            IProgress<int>? progreso = null)
        {
            var result = new SqlWriteResult();
            try
            {
                using var conn = new NpgsqlConnection(cadena);
                conn.Open();

                // CREATE TABLE IF NOT EXISTS con columnas TEXT
                CrearTablaPostgreSQL(conn, tabla, dt);

                int total = dt.Rows.Count, ok = 0, err = 0;
                using var tx = conn.BeginTransaction();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        try
                        {
                            InsertarFilaPostgreSQL(conn, tx, tabla, dt, row);
                            ok++;
                            if (ok % 100 == 0)
                                progreso?.Report((int)(ok * 100.0 / total));
                        }
                        catch { err++; }
                    }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }

                progreso?.Report(100);
                result.Exito = true;
                result.Insertados = ok;
                result.Errores = err;
                result.Mensaje = $"✅ {ok} filas insertadas en '{tabla}'. Errores: {err}.";
            }
            catch (Exception ex)
            {
                result.Exito = false;
                result.Mensaje = $"❌ {ex.Message}";
            }
            return result;
        }

        public static SqlWriteResult InsertarDataTableMariaDB(
            string cadena, string tabla, DataTable dt,
            IProgress<int>? progreso = null)
        {
            var result = new SqlWriteResult();
            try
            {
                using var conn = new MySqlConnection(cadena);
                conn.Open();

                CrearTablaMariaDB(conn, tabla, dt);

                int total = dt.Rows.Count, ok = 0, err = 0;
                using var tx = conn.BeginTransaction();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        try
                        {
                            InsertarFilaMariaDB(conn, tx, tabla, dt, row);
                            ok++;
                            if (ok % 100 == 0)
                                progreso?.Report((int)(ok * 100.0 / total));
                        }
                        catch { err++; }
                    }
                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }

                progreso?.Report(100);
                result.Exito = true;
                result.Insertados = ok;
                result.Errores = err;
                result.Mensaje = $"✅ {ok} filas insertadas en `{tabla}`. Errores: {err}.";
            }
            catch (Exception ex)
            {
                result.Exito = false;
                result.Mensaje = $"❌ {ex.Message}";
            }
            return result;
        }

        // ── Helpers privados ────────────────────────────────────────────────

        private static void CrearTablaPostgreSQL(NpgsqlConnection conn, string tabla, DataTable dt)
        {
            var cols = new StringBuilder();
            foreach (DataColumn col in dt.Columns)
            {
                if (cols.Length > 0) cols.Append(", ");
                string nombre = SanitizarNombre(col.ColumnName);
                string tipo = col.DataType == typeof(long) || col.DataType == typeof(int) ? "BIGINT"
                    : col.DataType == typeof(double) || col.DataType == typeof(float) || col.DataType == typeof(decimal) ? "DOUBLE PRECISION"
                    : "TEXT";
                cols.Append($"\"{nombre}\" {tipo}");
            }
            string sql = $"CREATE TABLE IF NOT EXISTS \"{tabla}\" ({cols});";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        private static void CrearTablaMariaDB(MySqlConnection conn, string tabla, DataTable dt)
        {
            var cols = new StringBuilder();
            foreach (DataColumn col in dt.Columns)
            {
                if (cols.Length > 0) cols.Append(", ");
                string nombre = SanitizarNombre(col.ColumnName);
                string tipo = col.DataType == typeof(long) || col.DataType == typeof(int) ? "BIGINT"
                    : col.DataType == typeof(double) || col.DataType == typeof(float) || col.DataType == typeof(decimal) ? "DOUBLE"
                    : "TEXT";
                cols.Append($"`{nombre}` {tipo}");
            }
            string sql = $"CREATE TABLE IF NOT EXISTS `{tabla}` ({cols}) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        private static void InsertarFilaPostgreSQL(NpgsqlConnection conn, NpgsqlTransaction tx,
            string tabla, DataTable dt, DataRow row)
        {
            var nombres = new List<string>();
            var pars = new List<string>();
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                nombres.Add($"\"{SanitizarNombre(dt.Columns[i].ColumnName)}\"");
                pars.Add($"@p{i}");
            }
            string sql = $"INSERT INTO \"{tabla}\" ({string.Join(",", nombres)}) VALUES ({string.Join(",", pars)});";
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            for (int i = 0; i < dt.Columns.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", row[i] ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static void InsertarFilaMariaDB(MySqlConnection conn, MySqlTransaction tx,
            string tabla, DataTable dt, DataRow row)
        {
            var nombres = new List<string>();
            var pars = new List<string>();
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                nombres.Add($"`{SanitizarNombre(dt.Columns[i].ColumnName)}`");
                pars.Add($"@p{i}");
            }
            string sql = $"INSERT INTO `{tabla}` ({string.Join(",", nombres)}) VALUES ({string.Join(",", pars)});";
            using var cmd = new MySqlCommand(sql, conn, tx);
            for (int i = 0; i < dt.Columns.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", row[i] ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static string SanitizarNombre(string nombre)
        {
            var sb = new StringBuilder();
            foreach (char c in nombre)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            string s = sb.ToString().Trim('_');
            if (s.Length == 0) return "campo";
            if (char.IsDigit(s[0])) s = "_" + s;
            return s.ToLowerInvariant();
        }
    }
}