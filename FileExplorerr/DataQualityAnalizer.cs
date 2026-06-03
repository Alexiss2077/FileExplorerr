using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  DATA QUALITY ANALYZER
    //  Inspects a DataTable for common data-quality issues and returns a
    //  QualityReport DTO.
    //
    //  Phase 5B: extracted from FileViewerForm.cs.
    //  Original methods removed from FileViewerForm:
    //    AnalyzeTable()           -> DataQualityAnalyzer.Analyze()
    //    IsPhoneColumn()          -> DataQualityAnalyzer.IsPhoneColumn()
    //    LooksLikePhone()         -> DataQualityAnalyzer.LooksLikePhone()
    //    ValidateAndFixPhone()    -> DataQualityAnalyzer.ValidateAndFixPhone()
    //    IsValidEmail()           -> DataQualityAnalyzer.IsValidEmail()
    //    DetectAndFixDate()       -> DataQualityAnalyzer.DetectAndFixDate()
    //    TryDate()                -> private TryDate()
    //
    //  Static fields migrated from FileViewerForm:
    //    DatePatterns, EmailRegex, PhoneKeywords, EmailKeywords
    //
    //  NOTE: CurrencyKeywords was NOT migrated — it remains in FileViewerForm
    //  because ApplyDisplayTable() (which stays in the form) also reads it.
    // ════════════════════════════════════════════════════════════════════════
    internal static class DataQualityAnalyzer
    {
        // ── Regex patterns — identical to original fields on FileViewerForm ─

        private static readonly Regex[] DatePatterns =
        {
            new(@"\b(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})\b"),
            new(@"\b(\d{2,4})[/\-\.](\d{1,2})[/\-\.](\d{1,2})\b"),
            new(@"\b(\d{1,2})\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(\d{2,4})\b",
                RegexOptions.IgnoreCase),
        };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

        private static readonly string[] PhoneKeywords =
        {
            "phone", "telefono", "tel\u00E9fono", "tel", "celular", "mobile", "cell",
            "fono", "movil", "m\u00F3vil", "whatsapp", "contacto", "numero", "n\u00FAmero"
        };

        private static readonly string[] EmailKeywords =
        {
            "email", "correo", "mail", "e-mail", "correo electronico",
            "correo electr\u00F3nico", "electronic", "address"
        };

        // ════════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINT
        //  Originally: private void AnalyzeTable() on FileViewerForm.
        //  Returns a fully-populated QualityReport instead of mutating form
        //  fields.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Analyses <paramref name="table"/> for data-quality issues and
        /// returns a <see cref="QualityReport"/> describing them.
        /// </summary>
        /// <param name="table">The DataTable to analyse.</param>
        /// <param name="columnMismatchRows">
        ///   Rows already identified as column-mismatched by the CSV parser.
        ///   Passed through into the returned report unchanged.
        /// </param>
        /// <param name="columnMismatchDetails">
        ///   Per-row mismatch details from the CSV parser.
        ///   Passed through into the returned report unchanged.
        /// </param>
        public static QualityReport Analyze(
            DataTable table,
            List<int>? columnMismatchRows = null,
            List<(int Row, int ExpectedCols, int ActualCols)>? columnMismatchDetails = null)
        {
            var duplicateRows = new List<int>();
            var dateIssues = new List<(int, int, string, string)>();
            var emptyFields = new List<(int, int)>();
            var phoneIssues = new List<(int, int, string, string)>();
            var emailIssues = new List<(int, int, string)>();

            // ── Identify phone and email columns ──────────────────────────
            var phoneColumns = new HashSet<int>();
            var emailColumns = new HashSet<int>();

            for (int c = 0; c < table.Columns.Count; c++)
            {
                if (IsPhoneColumn(table, c))
                    phoneColumns.Add(c);

                string colName = table.Columns[c].ColumnName.ToLower();
                if (EmailKeywords.Any(k => colName.Contains(k)))
                    emailColumns.Add(c);
            }

            // ── Duplicate detection ───────────────────────────────────────
            var seen = new Dictionary<string, int>();
            for (int r = 0; r < table.Rows.Count; r++)
            {
                string key = string.Join(
                    "\u2502",
                    table.Rows[r].ItemArray.Select(x => x?.ToString() ?? string.Empty));

                if (seen.TryGetValue(key, out int orig))
                {
                    if (!duplicateRows.Contains(orig)) duplicateRows.Add(orig);
                    duplicateRows.Add(r);
                }
                else
                {
                    seen[key] = r;
                }
            }

            // ── Cell-level analysis ───────────────────────────────────────
            for (int r = 0; r < table.Rows.Count; r++)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    string val = table.Rows[r][c]?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(val))
                    {
                        emptyFields.Add((r, c));
                        continue;
                    }

                    if (phoneColumns.Contains(c))
                    {
                        string? fix = ValidateAndFixPhone(val);
                        if (fix != null) phoneIssues.Add((r, c, val, fix));
                    }

                    if (emailColumns.Contains(c))
                    {
                        if (!IsValidEmail(val)) emailIssues.Add((r, c, val));
                    }

                    // Only check dates on non-phone columns to avoid false positives.
                    if (!phoneColumns.Contains(c))
                    {
                        string? fix = DetectAndFixDate(val);
                        if (fix != null && fix != val)
                            dateIssues.Add((r, c, val, fix));
                    }
                }
            }

            return new QualityReport
            {
                DuplicateRows = duplicateRows,
                DateIssues = dateIssues,
                EmptyFields = emptyFields,
                PhoneIssues = phoneIssues,
                EmailIssues = emailIssues,
                ColumnMismatchRows = columnMismatchRows ?? new List<int>(),
                ColumnMismatchDetails = columnMismatchDetails ?? new List<(int, int, int)>()
            };
        }

        // ════════════════════════════════════════════════════════════════════
        //  PHONE DETECTION
        //  Originally: IsPhoneColumn() + LooksLikePhone() on FileViewerForm.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true when column <paramref name="colIndex"/> is likely to
        /// contain phone numbers, based on the column name and at least 60% of
        /// non-empty values matching <see cref="LooksLikePhone"/>.
        /// </summary>
        public static bool IsPhoneColumn(DataTable dt, int colIndex)
        {
            string colName = dt.Columns[colIndex].ColumnName.ToLower();
            if (PhoneKeywords.Any(k => colName.Contains(k))) return true;

            var nonEmpty = dt.Rows.Cast<DataRow>()
                             .Select(r => r[colIndex]?.ToString()?.Trim() ?? string.Empty)
                             .Where(v => !string.IsNullOrWhiteSpace(v))
                             .ToList();

            if (nonEmpty.Count == 0) return false;

            int phoneCount = nonEmpty.Count(LooksLikePhone);
            return (double)phoneCount / nonEmpty.Count >= 0.6;
        }

        /// <summary>
        /// Returns true when <paramref name="value"/> resembles a phone number:
        /// contains only digits, spaces, +, -, (, ), . and ext; has 7–15 digits;
        /// and does not look like a decimal number.
        /// </summary>
        public static bool LooksLikePhone(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!Regex.IsMatch(value, @"^[\d\s\+\-\(\)\.ext]{7,20}$",
                    RegexOptions.IgnoreCase)) return false;

            string digitsOnly = new string(value.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 7 || digitsOnly.Length > 15) return false;

            // Reject decimal numbers that happen to look like phone numbers.
            var dotMatch = Regex.Match(value, @"\.(\d+)");
            if (dotMatch.Success && dotMatch.Groups[1].Value.TrimEnd('0').Length > 0)
                return false;

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        //  PHONE NORMALISATION
        //  Originally: ValidateAndFixPhone(string raw) on FileViewerForm.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Attempts to normalise a phone number to 10 digits.
        /// Returns a suggested fixed value (may start with "⚠" for issues)
        /// or null when the value is already in the expected 10-digit format.
        /// </summary>
        public static string? ValidateAndFixPhone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string digitsOnly = new string(raw.Where(char.IsDigit).ToArray());

            if (digitsOnly.Length == 0)
                return "\u26A0(sin d\u00EDgitos)";

            if (digitsOnly.Length == 10)
            {
                // Already correct number of digits; report only if formatting differs.
                return raw.Trim() != digitsOnly ? digitsOnly : null;
            }

            if (digitsOnly.Length > 10 && digitsOnly.Length <= 15)
                return digitsOnly.Substring(digitsOnly.Length - 10);

            if (digitsOnly.Length >= 7)
                return $"\u26A0{digitsOnly}({digitsOnly.Length}d)";

            return $"\u26A0{digitsOnly}(inv\u00E1lido)";
        }

        // ════════════════════════════════════════════════════════════════════
        //  EMAIL VALIDATION
        //  Originally: IsValidEmail(string email) on FileViewerForm.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true when <paramref name="email"/> looks like a valid
        /// email address (has one @, a non-empty local part, a domain with a
        /// dot, and a TLD of at least 2 characters).
        /// </summary>
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            email = email.Trim();
            if (!email.Contains('@')) return false;

            string[] parts = email.Split('@');
            if (parts.Length != 2 ||
                string.IsNullOrWhiteSpace(parts[0]) ||
                string.IsNullOrWhiteSpace(parts[1])) return false;

            if (!parts[1].Contains('.') ||
                parts[1].StartsWith('.') ||
                parts[1].EndsWith('.')) return false;

            string tld = parts[1].Substring(parts[1].LastIndexOf('.') + 1);
            return tld.Length >= 2;
        }

        // ════════════════════════════════════════════════════════════════════
        //  DATE NORMALISATION
        //  Originally: DetectAndFixDate() + TryDate() on FileViewerForm.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// If <paramref name="val"/> contains a recognisable date that is not
        /// already in ISO yyyy-MM-dd format, returns the normalised ISO string.
        /// Returns null when the value is already ISO or not a date.
        /// </summary>
        public static string? DetectAndFixDate(string val)
        {
            // Already ISO — nothing to do.
            if (Regex.IsMatch(val, @"^\d{4}-\d{2}-\d{2}$")) return null;

            // Pattern: d/m/y or m/d/y with / - .
            var m1 = Regex.Match(val, @"^(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})$");
            if (m1.Success)
            {
                int a = int.Parse(m1.Groups[1].Value);
                int b = int.Parse(m1.Groups[2].Value);
                int y = int.Parse(m1.Groups[3].Value);
                if (y < 100) y += 2000;

                if (a > 12 && b <= 12) return TryDate(y, b, a);
                if (b > 12 && a <= 12) return TryDate(y, a, b);
                return TryDate(y, b, a);
            }

            // Pattern: yyyy/m/d
            var m2 = Regex.Match(val, @"^(\d{4})[/\.](\d{1,2})[/\.](\d{1,2})$");
            if (m2.Success)
                return TryDate(
                    int.Parse(m2.Groups[1].Value),
                    int.Parse(m2.Groups[2].Value),
                    int.Parse(m2.Groups[3].Value));

            return null;
        }

        /// <summary>
        /// Attempts to create a <c>yyyy-MM-dd</c> string from the supplied
        /// year, month and day. Returns null when the combination is invalid.
        /// </summary>
        private static string? TryDate(int y, int m, int d)
        {
            try { return new DateTime(y, m, d).ToString("yyyy-MM-dd"); }
            catch { return null; }
        }
    }
}