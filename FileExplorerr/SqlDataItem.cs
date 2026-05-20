using System.Collections.Generic;

namespace FileExplorerr
{
    /// <summary>
    /// Fila genérica leída de cualquier tabla SQL.
    /// Las columnas y sus valores se almacenan en el diccionario.
    /// </summary>
    public class SqlDataItem
    {
        public Dictionary<string, string> Columnas { get; set; } = new();
    }

    public class SqlWriteResult
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = "";
        public int Insertados { get; set; }
        public int Errores { get; set; }
    }
}