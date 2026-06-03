using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Xml;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  CSV PARSE RESULT
    //  Wrapper returned by DataParsers.ParseCsv().
    //  The CSV parser is the only parser that also produces structural
    //  quality metadata (column-mismatch rows) as a side-effect of parsing.
    //  By returning a dedicated result object the parser stays stateless while
    //  delivering all the information FileViewerForm needs.
    // ════════════════════════════════════════════════════════════════════════
    internal sealed class CsvParseResult
    {
        /// <summary>The parsed data.</summary>
        public DataTable Table { get; init; } = new();

        /// <summary>
        /// Zero-based row indices (data rows, not header) whose column count
        /// differs from <see cref="ExpectedColumnCount"/>.
        /// </summary>
        public List<int> MismatchRows { get; init; } = new();

        /// <summary>
        /// Detailed mismatch info: (DataRowIndex, ExpectedCols, ActualCols).
        /// </summary>
        public List<(int Row, int ExpectedCols, int ActualCols)>
            MismatchDetails
        { get; init; } = new();

        /// <summary>Column count inferred from the header row.</summary>
        public int ExpectedColumnCount { get; init; }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DATA PARSERS
    //  Static, stateless parsers for CSV, TXT, JSON and XML.
    //  Each method receives raw file content as a string and returns a
    //  DataTable (or a CsvParseResult for CSV).
    //
    //  Phase 5B: extracted from FileViewerForm.cs.
    //  Original methods removed from FileViewerForm:
    //    ParseCsvWithMismatch   -> DataParsers.ParseCsv
    //    ParseTxt               -> DataParsers.ParseTxt
    //    SingleColumnTable      -> DataParsers.SingleColumnTable  (internal)
    //    ParseJson              -> DataParsers.ParseJson
    //    ParseXml               -> DataParsers.ParseXml
    //    FlattenXml             -> private helper here
    // ════════════════════════════════════════════════════════════════════════
    internal static class DataParsers
    {
        // ════════════════════════════════════════════════════════════════════
        //  CSV
        //  Originally: ParseCsvWithMismatch(string content) on FileViewerForm.
        //  Detects rows whose column count differs from the header and records
        //  them in CsvParseResult rather than mutating form fields.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses CSV content into a <see cref="CsvParseResult"/>.
        /// Respects RFC 4180 quoting via <see cref="CsvHelper.SplitLine"/>.
        /// Rows with a different column count from the header are recorded in
        /// <see cref="CsvParseResult.MismatchRows"/> but are still added to
        /// the DataTable (cells beyond the column count are silently dropped;
        /// missing cells are filled with empty string).
        /// </summary>
        public static CsvParseResult ParseCsv(string content)
        {
            var dt = new DataTable();
            var mismatchRows = new List<int>();
            var mismatchDetails = new List<(int, int, int)>();

            var lines = CsvHelper.SplitLines(content);
            if (lines.Count == 0)
                return new CsvParseResult { Table = dt };

            // Header
            var headers = CsvHelper.SplitLine(lines[0]);
            int expectedColCount = headers.Count;

            foreach (string h in headers)
            {
                string name = h.Trim('"', ' ');
                dt.Columns.Add(name.Length > 0 ? name : $"Col{dt.Columns.Count + 1}");
            }

            // Data rows
            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var cells = CsvHelper.SplitLine(lines[i]);

                if (cells.Count != expectedColCount)
                {
                    int dataRowIdx = i - 1;
                    mismatchRows.Add(dataRowIdx);
                    mismatchDetails.Add((dataRowIdx, expectedColCount, cells.Count));
                }

                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Count ? cells[c].Trim('"') : string.Empty;
                dt.Rows.Add(row);
            }

            return new CsvParseResult
            {
                Table = dt,
                MismatchRows = mismatchRows,
                MismatchDetails = mismatchDetails,
                ExpectedColumnCount = expectedColCount
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  TXT
        //  Originally: ParseTxt(string content) on FileViewerForm.
        //  Detects delimiter from the first line (tab > pipe > semicolon >
        //  comma).  Falls back to SingleColumnTable when no delimiter is found.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses delimited plain-text content into a DataTable.
        /// Supported delimiters: tab, pipe, semicolon, comma.
        /// Falls back to a single "Línea" column when no delimiter is detected.
        /// </summary>
        public static DataTable ParseTxt(string content)
        {
            var lines = CsvHelper.SplitLines(content);
            if (lines.Count == 0) return SingleColumnTable(content);

            string first = lines[0];
            char delim;
            if (first.Contains('\t')) delim = '\t';
            else if (first.Contains('|')) delim = '|';
            else if (first.Contains(';')) delim = ';';
            else if (first.Contains(',') && lines.Count > 1) delim = ',';
            else return SingleColumnTable(content);

            var dt = new DataTable();
            string[] headers = first.Split(delim);
            foreach (string h in headers)
                dt.Columns.Add(h.Trim().Length > 0 ? h.Trim() : $"Col{dt.Columns.Count + 1}");

            for (int i = 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] cells = lines[i].Split(delim);
                var row = dt.NewRow();
                for (int c = 0; c < dt.Columns.Count; c++)
                    row[c] = c < cells.Length ? cells[c].Trim() : string.Empty;
                dt.Rows.Add(row);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  JSON
        //  Originally: ParseJson(string content) on FileViewerForm.
        //  Handles three shapes:
        //    1. Root array of objects          → columns from all object keys
        //    2. Root object with a nested array → unwrap the first array
        //    3. Root object of scalars          → two-column key/value table
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses JSON content into a DataTable.
        /// Falls back to a single "Línea" column when parsing fails.
        /// </summary>
        public static DataTable ParseJson(string content)
        {
            var dt = new DataTable();
            try
            {
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // 1. Root array —————————————————————————————————————————
                    // Pass 1: collect all column names
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.ValueKind != JsonValueKind.Object) continue;
                        foreach (var prop in elem.EnumerateObject())
                            if (!dt.Columns.Contains(prop.Name))
                                dt.Columns.Add(prop.Name);
                    }
                    // Pass 2: fill rows
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        if (elem.ValueKind != JsonValueKind.Object) continue;
                        var row = dt.NewRow();
                        foreach (var prop in elem.EnumerateObject())
                        {
                            if (!dt.Columns.Contains(prop.Name)) continue;
                            row[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.Null => string.Empty,
                                JsonValueKind.Object => prop.Value.GetRawText(),
                                JsonValueKind.Array => prop.Value.GetRawText(),
                                _ => prop.Value.ToString()
                            };
                        }
                        dt.Rows.Add(row);
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    bool foundArray = false;

                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Array) continue;

                        // 2. Root object wrapping an array —————————————————
                        // Pass 1: collect columns
                        foreach (var elem in prop.Value.EnumerateArray())
                        {
                            if (elem.ValueKind != JsonValueKind.Object) continue;
                            foreach (var p in elem.EnumerateObject())
                                if (!dt.Columns.Contains(p.Name))
                                    dt.Columns.Add(p.Name);
                        }
                        // Pass 2: fill rows
                        foreach (var elem in prop.Value.EnumerateArray())
                        {
                            if (elem.ValueKind != JsonValueKind.Object) continue;
                            var row = dt.NewRow();
                            foreach (var p in elem.EnumerateObject())
                            {
                                if (!dt.Columns.Contains(p.Name)) continue;
                                row[p.Name] = p.Value.ValueKind == JsonValueKind.Null
                                    ? string.Empty
                                    : p.Value.ToString();
                            }
                            dt.Rows.Add(row);
                        }
                        foundArray = true;
                        break;
                    }

                    if (!foundArray)
                    {
                        // 3. Root object of scalars → key/value table ——————
                        dt.Columns.Add("Clave");
                        dt.Columns.Add("Valor");
                        foreach (var prop in doc.RootElement.EnumerateObject())
                            dt.Rows.Add(prop.Name,
                                prop.Value.ValueKind == JsonValueKind.Null
                                    ? string.Empty
                                    : prop.Value.GetRawText());
                    }
                }
            }
            catch
            {
                dt = SingleColumnTable(content);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  XML
        //  Originally: ParseXml(string content) + FlattenXml() on FileViewerForm.
        //  Treats the first child element name as the record type.
        //  Falls back to a two-column Node/Value flat table when the document
        //  has no uniform record structure.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses XML content into a DataTable.
        /// Attributes are mapped to columns prefixed with "@".
        /// Falls back to a two-column Node/Value flat table when the structure
        /// is non-uniform, or to a single "Línea" column when parsing fails.
        /// </summary>
        public static DataTable ParseXml(string content)
        {
            var dt = new DataTable();
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(content);

                XmlNodeList? records = null;
                if (doc.DocumentElement != null)
                {
                    var firstChild = doc.DocumentElement.FirstChild;
                    if (firstChild != null)
                        records = doc.DocumentElement.SelectNodes(firstChild.Name);
                }

                if (records == null || records.Count == 0)
                {
                    dt.Columns.Add("Nodo");
                    dt.Columns.Add("Valor");
                    FlattenXml(doc.DocumentElement, dt, string.Empty);
                    return dt;
                }

                // Collect all column names (attributes + child elements)
                var colSet = new LinkedList<string>();
                foreach (XmlNode rec in records)
                {
                    foreach (XmlAttribute attr in rec.Attributes!)
                    {
                        string key = "@" + attr.Name;
                        if (!colSet.Contains(key)) colSet.AddLast(key);
                    }
                    foreach (XmlNode child in rec.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element && !colSet.Contains(child.Name))
                            colSet.AddLast(child.Name);
                }
                foreach (string col in colSet)
                    dt.Columns.Add(col);

                // Fill rows
                foreach (XmlNode rec in records)
                {
                    var row = dt.NewRow();
                    foreach (XmlAttribute attr in rec.Attributes!)
                    {
                        string key = "@" + attr.Name;
                        if (dt.Columns.Contains(key))
                            row[key] = attr.Value;
                    }
                    foreach (XmlNode child in rec.ChildNodes)
                        if (child.NodeType == XmlNodeType.Element &&
                            dt.Columns.Contains(child.Name))
                            row[child.Name] = child.InnerText;
                    dt.Rows.Add(row);
                }
            }
            catch
            {
                dt = SingleColumnTable(content);
            }
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SINGLE-COLUMN FALLBACK
        //  Originally: SingleColumnTable(string content) on FileViewerForm.
        //  Internal visibility so DataQualityAnalyzer can call it if needed,
        //  though in practice it is only called from within this class.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a DataTable with a single column "Línea", one row per
        /// non-empty line in <paramref name="content"/>.
        /// Used as the final fallback when no structure is detectable.
        /// </summary>
        internal static DataTable SingleColumnTable(string content)
        {
            var dt = new DataTable();
            dt.Columns.Add("L\u00EDnea");
            foreach (string line in CsvHelper.SplitLines(content))
                if (!string.IsNullOrWhiteSpace(line))
                    dt.Rows.Add(line);
            return dt;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Recursively flattens an XML subtree into two columns: Node path
        /// and Value.  Used by <see cref="ParseXml"/> when the document has
        /// no uniform record structure.
        /// Originally: FlattenXml(XmlNode?, DataTable, string) on FileViewerForm.
        /// </summary>
        private static void FlattenXml(XmlNode? node, DataTable dt, string prefix)
        {
            if (node == null) return;
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;

                string name = string.IsNullOrEmpty(prefix)
                    ? child.Name
                    : prefix + "/" + child.Name;

                string val = child.HasChildNodes &&
                             child.FirstChild!.NodeType == XmlNodeType.Text
                    ? child.InnerText
                    : string.Empty;

                dt.Rows.Add(name, val);

                bool isLeaf = child.FirstChild?.NodeType == XmlNodeType.Text &&
                              child.ChildNodes.Count == 1;
                if (child.HasChildNodes && !isLeaf)
                    FlattenXml(child, dt, name);
            }
        }
    }
}