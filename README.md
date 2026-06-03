# FileExplorerr

> Explorador de archivos de escritorio avanzado para Windows, construido con **C# 12 / .NET 8** y **Windows Forms**. Tema oscuro "Arctic Night", visualizadores integrados para imágenes, audio y video, visor de datos con análisis de calidad, bloc de notas, y conexión directa a bases de datos SQL.

---

## Tabla de contenidos

- [Vista general](#vista-general)
- [Características](#características)
- [Arquitectura del proyecto](#arquitectura-del-proyecto)
- [Estructura de carpetas](#estructura-de-carpetas)
- [Módulos principales](#módulos-principales)
- [Visualizadores integrados](#visualizadores-integrados)
- [Atajos de teclado](#atajos-de-teclado)
- [Dependencias NuGet](#dependencias-nuget)
- [Requisitos](#requisitos)
- [Instalación y compilación](#instalación-y-compilación)
- [Tecnologías](#tecnologías)

---

## Vista general

FileExplorerr es un reemplazo funcional del Explorador de Windows con una interfaz oscura minimalista. Integra un reproductor de música con búsqueda automática de carátulas y letras, un visor/editor de imágenes con soporte GPS, un reproductor de video con LibVLC, un visor de datos estructurados (CSV/JSON/XML) con análisis automático de calidad, un bloc de notas avanzado, y un cliente SQL para PostgreSQL, MariaDB y SQL Server.

---

## Características

### Explorador de archivos
- Barra de dirección editable con historial de navegación (atrás / adelante / subir nivel)
- Actualización con `F5` o botón dedicado
- Doble clic en carpetas para navegar, en archivos para abrirlos con el visor correspondiente
- Nueva carpeta con validación de nombre y caracteres inválidos
- Renombrar y eliminar (a Papelera de Reciclaje) desde el menú contextual
- **Drag & Drop** entre carpetas con resaltado visual del destino y manejo de conflictos
- **Papelera integrada** en la barra de estado: arrastra archivos directamente sobre el icono
- Panel lateral derecho con árbol de contenido categorizado (carpetas, imágenes, audio, video, texto, documentos, otros) con lazy-loading de subcarpetas
- Búsqueda recursiva en el panel lateral por nombre de archivo o carpeta
- Barra de estado con desglose de contenido por tipo: `📁 4 carpetas · 📄 30 archivos · 🖼️ 12 · 🎵 3`
- Columna **Info** con resumen por categoría en cada fila de carpeta
- **Exportación de índice CSV** asíncrona con progreso en tiempo real

### Gestión de archivos
- Menú contextual con renderer personalizado (tema oscuro)
- Propiedades detalladas de archivos y carpetas: tamaño, fechas, atributos, propietario NTFS, metadatos específicos por tipo (imagen, audio, video, texto)
- Ordenamiento por cualquier columna con indicador visual

---

## Arquitectura del proyecto

El proyecto sigue una arquitectura en capas con separación clara de responsabilidades:

```
┌─────────────────────────────────────────────┐
│                  UI Layer                    │
│   Forms · Dialogs · Components · Theme       │
├─────────────────────────────────────────────┤
│               Services Layer                 │
│   FileOpener · FileOperationService ·        │
│   FileTypeHelper · ExportadorOffice          │
├─────────────────────────────────────────────┤
│                 Data Layer                   │
│   DataParsers · DataQualityAnalyzer ·        │
│   DataSerializer · QualityReport             │
├──────────────────┬──────────────────────────┤
│   Core Layer     │      Media Layer          │
│  FileExtensions  │  CoverSearcher/Service   │
│  FileClassifier  │  GpsReader · GpsWriter   │
│  FileStats       │  LyricsService           │
│  CsvIndexer      │  GpsData                 │
│  AppHelpers      │                          │
├──────────────────┴──────────────────────────┤
│               Database Layer                 │
│  IDbConnector · PostgreSqlConnector ·        │
│  MariaDbConnector · SqlServerConnector ·     │
│  SqlConnector (façade) · SqlWriteResult      │
└─────────────────────────────────────────────┘
```

**Principios de diseño aplicados:**
- **Single Responsibility**: cada clase tiene una responsabilidad bien definida (p. ej. `DataParsers` solo parsea, `DataQualityAnalyzer` solo analiza)
- **Interface Segregation**: `IDbConnector` define el contrato común para los tres motores de base de datos
- **DRY**: helpers centralizados en `AppHelpers.cs` eliminan implementaciones duplicadas (`FileSize`, `CsvHelper`, `BrowserHelper`, `SmtpConfig`)
- **Façade Pattern**: `SqlConnector` mantiene compatibilidad con código legado sin exponer los conectores concretos

---

## Estructura de carpetas

```
FileExplorerr/
│
├── FileExplorerr.csproj          # Proyecto WinForms .NET 8
├── Program.cs                    # Punto de entrada — STAThread, manejo global de excepciones
├── export_office.py              # Script Python para exportación a Excel/Word/PowerPoint/PDF
│
├── Core/                         # Lógica de dominio transversal
│   ├── AppHelpers.cs             # FileExtensions, FileSize, CsvHelper, BrowserHelper, SmtpConfig
│   ├── CsvIndexer.cs             # Generador de índice CSV asíncrono
│   ├── FileClassifier.cs         # Clasificación de archivos por extensión → FileStats
│   ├── FileStats.cs              # Contadores con métodos de formateo para barra de estado
│   └── QualityReport.cs          # DTO con resultados del análisis de calidad de datos
│
├── Data/                         # Parsers y análisis de archivos de datos
│   ├── DataParsers.cs            # Parsers para CSV, TXT, JSON y XML → DataTable
│   ├── DataQualityAnalizer.cs    # Detección de duplicados, fechas, emails, teléfonos
│   └── DatSerializer.cs          # Serialización DataTable → CSV, TSV, JSON, XML
│
├── DataBase/                     # Conectores y abstracción de bases de datos
│   ├── IDbConnector.cs           # Interfaz común + enum DbConnectorType
│   ├── PostgreSqlConnector.cs    # Implementación PostgreSQL via Npgsql
│   ├── MariaDbConnector.cs       # Implementación MariaDB via MySqlConnector
│   ├── SqlServerConnector.cs     # Implementación SQL Server via Microsoft.Data.SqlClient
│   ├── SqlConnector.cs           # Façade de compatibilidad (métodos estáticos legacy)
│   └── SqlWriteResult.cs         # DTO de resultado de inserción masiva
│
├── Media/                        # Servicios multimedia y metadatos
│   ├── CoverSearcher.cs          # Búsqueda multi-fuente de carátulas (iTunes, Last.fm, Spotify)
│   ├── CoverSearchResult.cs      # DTO de resultado de búsqueda de carátulas
│   ├── CoverSearchService.cs     # Façade de alto nivel para descarga de carátulas
│   ├── GpsData.cs                # Record inmutable con coordenadas GPS y metadatos
│   ├── GpsReader.cs              # Extractor GPS de imágenes (EXIF) y videos (átomos MP4)
│   ├── GpsWriter.cs              # Escritura de coordenadas GPS en EXIF de JPEG/TIFF
│   └── LyricsService.cs          # Búsqueda de letras via lrclib.net
│
├── Services/                     # Servicios de la capa de aplicación
│   ├── ExportadorOffice.cs       # Motor de exportación a Excel/Word/PowerPoint/PDF via Python
│   ├── FileOpener.cs             # Enrutador de apertura de archivos al visor correcto
│   ├── FileOperationService.cs   # Crear carpeta, renombrar, eliminar, mover (DnD)
│   └── FileTypeHelper.cs         # Etiquetas legibles por tipo + columna Info de carpetas
│
└── UI/
    ├── Components/
    │   ├── FileIconFactory.cs    # Iconos programáticos 32×32 + resolución de clave ImageList
    │   ├── MinimalMenuRenderer.cs # Renderer de menú contextual + LvComparer para ListView
    │   └── Theme.cs              # Sistema de diseño "Arctic Night" — colores, fuentes, factory methods
    │
    ├── Dialogs/
    │   ├── ConexionDialog.cs     # Formulario de conexión a BD (PostgreSQL/MariaDB/SQL Server)
    │   ├── EmailForm.cs          # Formulario de envío de archivo por SMTP
    │   ├── ExportProgressForm.cs # Ventana de progreso para exportación Office/PDF
    │   ├── GpsEditDialog.cs      # Diálogo para agregar/editar coordenadas GPS
    │   ├── InputDialog.cs        # Diálogo genérico de entrada de texto de una línea
    │   ├── NombreTablaDialog.cs  # Diálogo para nombre de tabla en importación a BD
    │   ├── TagEditDialog.cs      # Edición de tags ID3 (título, artista, álbum, año, pista, género)
    │   └── TextToolDialog.cs     # Selector de fuente/estilo/color para texto en imágenes
    │
    └── Forms/
        ├── Form1.cs              # Ventana principal del explorador
        ├── Form1.Designer.cs
        ├── FilePropertiesForm.cs # Propiedades detalladas de archivo/carpeta
        ├── FileViewerform.cs     # Visor de datos CSV/JSON/XML/TXT con análisis de calidad
        ├── ImagevIewerform.cs    # Visor y editor de imágenes con GPS
        ├── MusicPlayerForm.cs    # Reproductor de música con letras y carátulas
        ├── NotepadForm.cs        # Bloc de notas con numeración de líneas y búsqueda
        ├── SqlViewerForm.cs      # Cliente SQL para PostgreSQL, MariaDB y SQL Server
        └── VideoPlayerForm.cs    # Reproductor de video con LibVLC y webcam
```

---

## Módulos principales

### `Form1.cs` — Ventana principal
Gestiona la navegación, el ciclo de carga de directorios, el `ListView` con `OwnerDraw` para cabeceras personalizadas, el `TreeView` lateral con lazy-loading, Drag & Drop completo (entre carpetas y hacia la papelera), el menú contextual y el título de barra oscuro via `DwmSetWindowAttribute`. Delega las operaciones de archivos a `FileOperationService` y la apertura de archivos a `FileOpener`.

### `Core/AppHelpers.cs` — Helpers centralizados
Fuente única de verdad para las extensiones categorizadas (`FileExtensions`), formateo de tamaños (`FileSize`), formateo de duración (`TimeSpanFormat`), parsing de CSV (`CsvHelper`), emulación de navegador IE-Edge (`BrowserHelper`) y configuración SMTP persistida en AppData (`SmtpConfig`).

### `Core/CsvIndexer.cs` — Generador de índice
Recorre recursivamente un directorio en un hilo de fondo y produce un CSV con una fila por archivo. Reporta progreso via `IProgress<string>`. Delega la clasificación a `FileClassifier`.

### `Data/DataParsers.cs` — Parsers de datos
Parsers estáticos y sin estado para CSV (respeta RFC 4180 con comillas y escapes), TXT (detección automática de delimitador), JSON (arrays, objetos simples y objetos con array anidado) y XML (atributos + elementos hijo como columnas, con fallback a tabla plana). El parser CSV devuelve `CsvParseResult` con información de filas con columnas desajustadas.

### `Data/DataQualityAnalizer.cs` — Análisis de calidad
Detecta filas duplicadas, fechas con formato inconsistente (y propone normalización a `yyyy-MM-dd`), campos vacíos, números de teléfono malformados (con sugerencia de corrección a 10 dígitos) y emails inválidos. Retorna un `QualityReport` DTO sin mutar estado externo.

### `DataBase/IDbConnector.cs` + conectores — Capa de base de datos
Interfaz común con `TestConnectionAsync`, `GetTablesAsync`, `ExecuteAsync` e `InsertDataTableAsync`. Los tres conectores concretos (`PostgreSqlConnector`, `MariaDbConnector`, `SqlServerConnector`) implementan inserción masiva con transacción, manejo de cancelación y reporte de progreso. `SqlConnector` actúa como façade de compatibilidad.

### `Media/CoverSearcher.cs` — Búsqueda de carátulas
Consulta en paralelo iTunes Search API, Last.fm API y Spotify API. Aplica un algoritmo de similitud que combina distancia Levenshtein y coincidencia de palabras con pesos ponderados (artista 35%, título 50%, palabras 15%). Normaliza el texto eliminando diacríticos, stopwords musicales y contenido entre paréntesis. Caché en memoria por clave normalizada.

### `Media/GpsReader.cs` + `GpsWriter.cs` — GPS
`GpsReader` extrae coordenadas de imágenes via EXIF (`System.Drawing`) y de videos MP4/MOV via átomos QuickTime (`©xyz`, `loci`) con fallback a scan de patrón ISO 6709 en los primeros 50 MB. `GpsWriter` escribe coordenadas GPS en EXIF de archivos JPEG y TIFF de forma atómica (archivo temporal + rename).

### `Services/ExportadorOffice.cs` — Exportación Office/PDF
Pipeline asíncrono: escribe un CSV temporal → invoca `export_office.py` → lee progreso por stdout. El script Python usa `openpyxl`, `python-docx`, `python-pptx` y `reportlab`. Soporta cancelación y muestra `ExportProgressForm` con barra animada.

### `UI/Forms/FileViewerform.cs` — Visor de datos
Carga archivos de datos, aplica parsers y análisis de calidad, y muestra los resultados en un `DataGridView` con celdas coloreadas por tipo de problema. Permite filtrar, ordenar, guardar una copia corregida y exportar a CSV/JSON/TXT/XML/Excel/Word/PowerPoint/PDF. Incluye exportación directa a una base de datos SQL abierta.

### `UI/Forms/MusicPlayerForm.cs` — Reproductor de música
Carga todos los archivos de audio de la carpeta del archivo inicial. Soporta shuffle (orden pre-generado), tres modos de repetición (off / lista / pista), seek, volumen y mute. Busca carátulas via `CoverSearchService` y las guarda en el tag ID3. Busca letras via `LyricsService`. Incluye grabación de micrófono con `NAudio.WaveInEvent` y guarda en WAV.

### `UI/Forms/VideoPlayerForm.cs` — Reproductor de video
Inicialización asíncrona de LibVLC para no bloquear el hilo UI. Soporta lista de reproducción con Drag & Drop, tres modos de bucle (off / lista / uno), velocidades de 0.25× a 3×, pantalla completa, metadatos via `Media.Parse()` y grabación de webcam con OpenCvSharp.

### `UI/Forms/NotepadForm.cs` — Bloc de notas
Detección automática de encoding (UTF-8 BOM, UTF-16 LE/BE). Numeración de líneas con `OwnerDraw` sincronizada con el scroll. Búsqueda con resaltado y navegación circular, reemplazar uno / todos (async para archivos grandes), ir a línea, zoom de fuente y protección al cerrar con cambios pendientes.

### `UI/Components/Theme.cs` — Sistema de diseño
Paleta "Arctic Night" con 7 fondos en capas, acento violeta, colores semánticos (teal, coral, ámbar, azul cielo, rosa) con variantes dim para fondos. Factory methods para `Button`, `TextBox`, `Label`, `DataGridView` y divisores con estilos consistentes.

---

## Visualizadores integrados

| Tipo | Extensiones | Visor |
|------|-------------|-------|
| Imagen | `.jpg` `.jpeg` `.jfif` `.png` `.gif` `.bmp` `.webp` `.tiff` `.ico` `.svg` `.emf` `.wmf` `.raw` `.cr2` `.cr3` `.nef` `.nrw` `.arw` `.dng` `.heic` y más | `ImageViewerForm` |
| Audio | `.mp3` `.wav` `.wma` `.m4a` `.flac` `.aac` `.ogg` `.opus` `.aiff` | `MusicPlayerForm` |
| Video | `.mp4` `.avi` `.mkv` `.mov` `.wmv` `.flv` `.webm` `.ts` `.3gp` `.mpg` `.mpeg` `.vob` `.divx` | `VideoPlayerForm` |
| CSV | `.csv` | `FileViewerForm` con análisis de calidad |
| JSON | `.json` | `FileViewerForm` con análisis de calidad |
| XML | `.xml` | `FileViewerForm` con análisis de calidad |
| Log / Texto | `.txt` `.log` | Elección entre `FileViewerForm` o `NotepadForm` |
| Código | `.cs` `.py` `.js` `.ts` `.html` `.css` `.md` `.yaml` `.yml` | `NotepadForm` |
| Otros | Cualquier extensión | Aplicación predeterminada del sistema |

### Capacidades del visor de imágenes
- Zoom libre con rueda del ratón (5% – 2000%), pan, ajustar a ventana, 1:1
- Herramientas: recorte rectangular, dibujo libre, borrador, texto (con selector de fuente), cuentagotas
- Transformaciones: rotar ±90°, voltear horizontal/vertical
- Filtros: escala de grises, sepia, invertir colores
- Deshacer (hasta 20 estados), restaurar original
- Panel GPS con mapa Leaflet/OpenStreetMap embebido y opción de escritura de coordenadas

---

## Atajos de teclado

### Explorador principal
| Atajo | Acción |
|-------|--------|
| `F5` | Actualizar directorio |
| `Enter` en barra de dirección | Navegar a la ruta |
| `Enter` en barra de búsqueda | Buscar en panel lateral |

### Visor de imágenes
| Atajo | Acción |
|-------|--------|
| `+` / `−` | Zoom in / Zoom out |
| `Ctrl+Z` | Deshacer |
| `Ctrl+S` | Guardar copia |
| `Escape` | Deseleccionar herramienta / Cerrar |

### Reproductor de música
| Atajo | Acción |
|-------|--------|
| `Espacio` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 5 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |

### Reproductor de video
| Atajo | Acción |
|-------|--------|
| `Espacio` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 10 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |
| `M` | Silenciar / Activar audio |
| `F` | Pantalla completa / Ventana |
| `Escape` | Salir de pantalla completa |

### Bloc de notas
| Atajo | Acción |
|-------|--------|
| `Ctrl+S` | Guardar |
| `Ctrl+Shift+S` | Guardar como |
| `Ctrl+F` | Abrir búsqueda |
| `Ctrl+H` | Abrir reemplazar |
| `Ctrl+G` | Ir a línea |
| `F3` | Siguiente coincidencia |
| `Ctrl++` / `Ctrl+−` | Zoom de fuente |
| `Escape` | Cerrar panel de búsqueda |

### Visor SQL
| Atajo | Acción |
|-------|--------|
| `F5` o `Ctrl+Enter` | Ejecutar consulta |

---

## Dependencias NuGet

| Paquete | Versión | Uso |
|---------|---------|-----|
| `LibVLCSharp` | 3.9.7.1 | Motor de video multiplataforma |
| `LibVLCSharp.WinForms` | 3.9.3 | Control `VideoView` para Windows Forms |
| `VideoLAN.LibVLC.Windows` | 3.0.21 | Binarios nativos de VLC para Windows |
| `OpenCvSharp4` | 4.13.0.20260526 | Captura y grabación de webcam |
| `OpenCvSharp4.Extensions` | 4.13.0.20260526 | Conversión `Mat` → `Bitmap` |
| `OpenCvSharp4.runtime.win` | 4.13.0.20260302 | Binarios nativos de OpenCV para Windows |
| `NAudio` | 2.2.1 | Reproducción de audio y grabación de micrófono |
| `taglib-sharp-netstandard2.0` | 2.1.0 | Lectura y escritura de tags ID3, Vorbis, APE |
| `Npgsql` | 9.0.2 | Conector ADO.NET para PostgreSQL |
| `MySqlConnector` | 2.3.7 | Conector ADO.NET para MariaDB / MySQL |
| `Microsoft.Data.SqlClient` | 5.2.2 | Conector ADO.NET para SQL Server |

### Dependencias Python (exportación Office/PDF)
```bash
pip install openpyxl python-docx python-pptx reportlab
```

### APIs externas (opcionales, requieren internet)
- **iTunes Search API** — carátulas de álbumes
- **lrclib.net** — letras de canciones en texto plano
- **Last.fm API** — carátulas alternativas
- **Spotify API** — carátulas alternativas (sin autenticación, best-effort)
- **OpenStreetMap / Leaflet.js** — mapas GPS embebidos en `WebBrowser`

---

## Requisitos

- **Sistema operativo:** Windows 10 / 11 (x64)
- **Runtime:** .NET 8.0 (Windows)
- **Python 3.10+** en el PATH (para exportación a Excel/Word/PowerPoint/PDF)
- Conexión a internet opcional (carátulas, letras, mapas GPS)

---

## Instalación y compilación

```bash
# 1. Clonar el repositorio
git clone <repo-url>
cd FileExplorerr

# 2. Restaurar dependencias NuGet
dotnet restore

# 3. Compilar en modo Release
dotnet build -c Release

# 4. Ejecutar
dotnet run --project FileExplorerr/FileExplorerr.csproj
```

O abrir `FileExplorerr.slnx` en **Visual Studio 2022 v17.8+** y compilar con `Ctrl+Shift+B`.

Para habilitar la exportación Office/PDF, instalar las dependencias Python:
```bash
pip install openpyxl python-docx python-pptx reportlab
```

El archivo `export_office.py` debe estar en el mismo directorio que el ejecutable (se copia automáticamente en el build gracias a la regla `CopyToOutputDirectory` en el `.csproj`).

---

## Tecnologías

| Tecnología | Uso |
|------------|-----|
| C# 12 / .NET 8 (Windows) | Lenguaje y runtime principal |
| Windows Forms | Framework de UI con `OwnerDraw` personalizado |
| P/Invoke | `SHFileOperation` (papelera), `DwmSetWindowAttribute` (título oscuro), `ExtractIcon` (icono de papelera) |
| LibVLC / LibVLCSharp | Decodificación y reproducción de video |
| NAudio | Decodificación y reproducción de audio PCM, grabación de micrófono |
| TagLib Sharp | Metadatos de audio (ID3v2, Vorbis, APE, M4A) |
| OpenCvSharp4 | Captura de webcam y grabación de video |
| Npgsql | Cliente PostgreSQL async |
| MySqlConnector | Cliente MariaDB/MySQL async |
| Microsoft.Data.SqlClient | Cliente SQL Server async |
| System.Text.Json | Parsing de JSON y respuestas de APIs |
| System.Drawing | Edición de imágenes, lectura de EXIF, iconos programáticos |
| Leaflet.js + OpenStreetMap | Mapas GPS en `WebBrowser` embebido |
| Python 3 + openpyxl/python-docx/python-pptx/reportlab | Generación de archivos Office y PDF |
| async/await | Carga de directorios, exportación, búsqueda de carátulas y letras sin bloquear la UI |
