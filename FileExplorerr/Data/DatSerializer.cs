using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  DATA SERIALIZER
    //  Converts a DataTable to CSV, TSV, JSON or XML strings.
    //
    //  Phase 5B: extracted from FileViewerForm.cs.
    //  Original methods removed from FileViewerForm:
    //    TableToCsv()        -> DataSerializer.ToCsv()
    //    TableToTsv()        -> DataSerializer.ToTsv()
    //    TableToJson()       -> DataSerializer.ToJson()
    //    TableToXml()        -> DataSerializer.ToXml()
    //    SerializeTable()    -> DataSerializer.Serialize()
    //
    //  All methods are static and stateless.
    //  Call-site in FileViewerForm: replace SerializeTable(dt, ext)
    //  with DataSerializer.Serialize(dt, ext).
    // ════════════════════════════════════════════════════════════════════════
    internal static class DataSerializer
    {
        // ════════════════════════════════════════════════════════════════════
        //  DISPATCH
        //  Originally: private static string SerializeTable(DataTable, string)
        //  on FileViewerForm.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the serialized string for <paramref name="dt"/> in the
        /// format implied by <paramref name="ext"/> (.csv, .json, .xml, or
        /// any other extension → TSV).
        /// Call-sites: replace <c>SerializeTable(dt, ext)</c>
        ///             with     <c>DataSerializer.Serialize(dt, ext)</c>.
        /// </summary>
        public static string Serialize(DataTable dt, string ext) =>
            ext switch
            {
                ".csv" => ToCsv(dt),
                ".json" => ToJson(dt),
                ".xml" => ToXml(dt),
                _ => ToTsv(dt)
            };

        // ════════════════════════════════════════════════════════════════════
        //  CSV
        //  Originally: private static string TableToCsv(DataTable) on
        //  FileViewerForm.  Uses CsvHelper.EscapeField (AppHelpers.cs).
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Serialises <paramref name="dt"/> to RFC 4180 CSV.
        /// All values are quoted; double-quotes inside values are escaped.
        /// </summary>
        public static string ToCsv(DataTable dt)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",",
                dt.Columns.Cast<System.Data.DataColumn>()
                  .Select(c => $"\"{CsvHelper.EscapeField(c.ColumnName)}\"")));

            // Data
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join(",",
                    row.ItemArray.Select(
                        x => $"\"{CsvHelper.EscapeField(x?.ToString() ?? string.Empty)}\"")));

            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  TSV (tab-separated)
        //  Originally: private static string TableToTsv(DataTable) on
        //  FileViewerForm.  Used as the default fallback for .txt exports.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Serialises <paramref name="dt"/> to tab-separated values.
        /// Values are not quoted; tab characters within values are preserved
        /// as-is (same behaviour as the original FileViewerForm implementation).
        /// </summary>
        public static string ToTsv(DataTable dt)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Join("\t",
                dt.Columns.Cast<System.Data.DataColumn>()
                  .Select(c => c.ColumnName)));

            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join("\t",
                    row.ItemArray.Select(x => x?.ToString() ?? string.Empty)));

            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  JSON
        //  Originally: private static string TableToJson(DataTable) on
        //  FileViewerForm.  Numeric cells are emitted as JSON numbers;
        //  empty cells become JSON null.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Serialises <paramref name="dt"/> to a JSON array of objects.
        /// Numeric cell values are written as JSON numbers; empty/null cells
        /// become JSON null; all other values become JSON strings.
        /// </summary>
        public static string ToJson(DataTable dt)
        {
            var rows = new List<Dictionary<string, object?>>();

            foreach (DataRow row in dt.Rows)
            {
                var d = new Dictionary<string, object?>();
                foreach (System.Data.DataColumn col in dt.Columns)
                {
                    string? val = row[col]?.ToString();
                    if (double.TryParse(val,
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double num))
                        d[col.ColumnName] = num;
                    else if (string.IsNullOrEmpty(val))
                        d[col.ColumnName] = null;
                    else
                        d[col.ColumnName] = val;
                }
                rows.Add(d);
            }

            return JsonSerializer.Serialize(rows, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  XML
        //  Originally: private static string TableToXml(DataTable) on
        //  FileViewerForm.  Delegates to DataTable.WriteXml().
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Serialises <paramref name="dt"/> to XML using
        /// <see cref="DataTable.WriteXml(TextWriter)"/>.
        /// The DataTable's <c>TableName</c> is set to "Records" before
        /// serialisation and is restored afterwards to avoid side-effects.
        /// </summary>
        public static string ToXml(DataTable dt)
        {
            string originalName = dt.TableName;
            dt.TableName = "Records";
            try
            {
                using var sw = new StringWriter();
                dt.WriteXml(sw);
                return sw.ToString();
            }
            finally
            {
                dt.TableName = originalName;
            }
        }
    }
}