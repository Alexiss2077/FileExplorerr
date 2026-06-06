using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace FileExplorerr.Charts
{
    // ════════════════════════════════════════════════════════════════════════
    //  CHART DATA BUILDER
    //  Transforma un DataTable en la lista que DataChartPanel necesita.
    //  Single Responsibility: solo convierte datos, no dibuja.
    //
    //  Uso:
    //      var data  = ChartDataBuilder.Build(dt, "País", "PIB", ChartMetric.Sum, 12);
    //      var cols  = ChartDataBuilder.GetColumnNames(dt);
    //      var numCols = ChartDataBuilder.GetNumericColumns(dt);
    // ════════════════════════════════════════════════════════════════════════
    public static class ChartDataBuilder
    {
        // ── Tipos de métrica ──────────────────────────────────────────────

        public enum ChartMetric { Count, Sum, Average }

        // ── API principal ─────────────────────────────────────────────────

        /// <summary>
        /// Agrupa los datos por <paramref name="groupColumn"/> y calcula la métrica
        /// elegida sobre <paramref name="valueColumn"/> (ignorado cuando la métrica
        /// es Count). Devuelve los primeros <paramref name="maxItems"/> grupos,
        /// ordenados de mayor a menor.
        /// </summary>
        public static List<(string Label, double Value)> Build(
            DataTable? table,
            string groupColumn,
            string? valueColumn,
            ChartMetric metric = ChartMetric.Count,
            int maxItems = 8)
        {
            var result = new List<(string, double)>();

            if (table is null || table.Rows.Count == 0) return result;
            if (!table.Columns.Contains(groupColumn)) return result;
            if (metric != ChartMetric.Count &&
                !string.IsNullOrEmpty(valueColumn) &&
                !table.Columns.Contains(valueColumn)) return result;

            // Acumular suma y conteo por grupo en un solo recorrido (O(n))
            var sums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow row in table.Rows)
            {
                string key = row[groupColumn]?.ToString()?.Trim() ?? "(vacío)";
                if (string.IsNullOrEmpty(key)) key = "(vacío)";

                if (!sums.ContainsKey(key)) { sums[key] = 0; counts[key] = 0; }
                counts[key]++;

                if (metric != ChartMetric.Count && !string.IsNullOrEmpty(valueColumn))
                {
                    string raw = row[valueColumn!]?.ToString() ?? "0";
                    // Limpiar símbolos de moneda antes de parsear
                    raw = raw.TrimStart('$', '€', '£', '¥', ' ').Replace(",", string.Empty);
                    if (double.TryParse(raw, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v))
                        sums[key] += v;
                }
            }

            // Calcular valor final por grupo
            var pairs = new List<(string Label, double Value)>();
            foreach (var kv in sums)
            {
                double finalValue = metric switch
                {
                    ChartMetric.Count => counts[kv.Key],
                    ChartMetric.Average => counts[kv.Key] > 0
                                            ? kv.Value / counts[kv.Key]
                                            : 0,
                    _ => kv.Value,   // Sum
                };
                pairs.Add((kv.Key, finalValue));
            }

            return pairs
                .OrderByDescending(p => p.Value)
                .Take(maxItems)
                .ToList();
        }

        // ── Helpers de introspección de columnas ──────────────────────────

        /// <summary>Devuelve todos los nombres de columna del DataTable.</summary>
        public static List<string> GetColumnNames(DataTable? table)
        {
            if (table is null) return new();
            return table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        }

        /// <summary>
        /// Devuelve las columnas cuyo contenido sea mayoritariamente numérico
        /// (al menos el 75 % de las primeras 50 filas no vacías).
        /// </summary>
        public static List<string> GetNumericColumns(DataTable? table)
        {
            if (table is null || table.Rows.Count == 0) return new();

            var result = new List<string>();
            int sampleN = Math.Min(50, table.Rows.Count);

            foreach (DataColumn col in table.Columns)
            {
                int numeric = 0, nonEmpty = 0;
                for (int i = 0; i < sampleN; i++)
                {
                    string raw = table.Rows[i][col]?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(raw)) continue;
                    nonEmpty++;
                    string clean = raw.TrimStart('$', '€', '£', '¥', ' ').Replace(",", string.Empty);
                    if (double.TryParse(clean, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out _))
                        numeric++;
                }
                if (nonEmpty > 0 && (double)numeric / nonEmpty >= 0.75)
                    result.Add(col.ColumnName);
            }
            return result;
        }

        /// <summary>
        /// Devuelve las columnas que NO son mayoritariamente numéricas
        /// (candidatas a ser el eje de agrupación).
        /// </summary>
        public static List<string> GetCategoricalColumns(DataTable? table)
        {
            if (table is null) return new();
            var numeric = new HashSet<string>(GetNumericColumns(table), StringComparer.OrdinalIgnoreCase);
            return GetColumnNames(table).Where(c => !numeric.Contains(c)).ToList();
        }

        /// <summary>
        /// Genera un título descriptivo según la métrica seleccionada.
        /// Ejemplo: "Suma de Ventas  por  País"
        /// </summary>
        public static string BuildTitle(
            string groupColumn, string? valueColumn, ChartMetric metric)
        {
            string metricLabel = metric switch
            {
                ChartMetric.Count => "Conteo",
                ChartMetric.Average => $"Promedio de {valueColumn}",
                _ => $"Suma de {valueColumn}",
            };
            return $"{metricLabel}  por  {groupColumn}";
        }
    }
}