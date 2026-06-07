

<div align="center">

# 📁 FileExplorerr

**Explorador de archivos de escritorio avanzado para Windows**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows Forms](https://img.shields.io/badge/UI-Windows%20Forms-0078D4?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/)
[![C# 12](https://img.shields.io/badge/C%23-12-239120?style=flat-square&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

Un reemplazo funcional del Explorador de Windows con tema oscuro **Arctic Night**, visualizadores multimedia integrados, análisis inteligente de datos, cliente SQL multi-motor, exportación nativa a Office y PDF, y autenticación OAuth con Google y GitHub.

[Características](#-características) · [Arquitectura](#-arquitectura) · [Instalación](#-instalación) · [Uso](#-uso) · [Tecnologías](#-tecnologías)

</div>

---

## 📋 Tabla de Contenidos

- [Vista General](#-vista-general)
- [Características](#-características)
- [Arquitectura del Sistema](#-arquitectura-del-sistema)
- [Estructura del Proyecto](#-estructura-del-proyecto)
- [Módulos Principales](#-módulos-principales)
- [Funcionalidades en Detalle](#-funcionalidades-en-detalle)
- [Diseño y Patrones](#-diseño-y-patrones)
- [Tecnologías Utilizadas](#-tecnologías-utilizadas)
- [Instalación](#-instalación)
- [Configuración](#-configuración)
- [Uso](#-uso)
- [Atajos de Teclado](#-atajos-de-teclado)
- [Rendimiento](#-rendimiento)
- [Roadmap](#-roadmap)
- [Contribución](#-contribución)

---

## 🌟 Vista General

FileExplorerr es una aplicación de escritorio Windows construida con C# 12 y .NET 8 que reemplaza y extiende al Explorador de Windows nativo. Combina navegación de archivos con un ecosistema completo de herramientas: reproducción multimedia, edición de imágenes con GPS, análisis y visualización de datos estructurados, un cliente SQL multi-motor, y exportación a múltiples formatos de documento.

### ¿Qué lo hace diferente?

| Característica | Explorador de Windows | FileExplorerr |
|---|---|---|
| Reproducción de audio/video | ❌ | ✅ Integrada |
| Edición de imágenes | ❌ | ✅ Con GPS, filtros y herramientas |
| Análisis de datos CSV/JSON/XML | ❌ | ✅ Con detección de anomalías |
| Cliente SQL | ❌ | ✅ PostgreSQL, MariaDB, SQL Server |
| Exportación a Office/PDF | ❌ | ✅ .xlsx, .docx, .pptx, .pdf |
| Compresión/descompresión | ❌ | ✅ ZIP, 7z, TAR, RAR |
| Autenticación OAuth | ❌ | ✅ Google + GitHub |
| Tema oscuro | Limitado | ✅ Arctic Night completo |
| Gráficas de datos | ❌ | ✅ Columnas, barras, pastel |

---

## ✨ Características

### 🗂 Explorador de Archivos
- Navegación con historial completo (atrás / adelante / subir nivel)
- Barra de dirección editable con navegación por teclado (`Enter`)
- **Autocompletado inteligente en la barra de dirección**: al escribir una ruta, aparece un menú flotante con las subcarpetas que coinciden con el texto ingresado. El menú se posiciona automáticamente debajo de la barra, respeta el tema oscuro Arctic Night y se cierra al presionar `Escape`. Navega entre sugerencias con `↓`, acepta con `Enter` o haciendo clic. Al navegar a una carpeta mediante código (historial, botones, favoritos), el menú no se activa para no interrumpir la navegación.
- Actualización con `F5` o botón dedicado
- **Drag & Drop** entre carpetas con resaltado visual del destino
- **Papelera integrada** en la barra de estado para eliminar arrastrando
- Panel lateral derecho con árbol de contenido categorizado y **lazy-loading** de subcarpetas
- Búsqueda recursiva por nombre de archivo o carpeta en el panel lateral
- Barra de estado con desglose por tipo: `📁 carpetas · 📄 archivos · 🖼️ imágenes · 🎵 audios`
- Columna **Info** con resumen de contenido por carpeta
- Exportación de índice CSV asíncrona con progreso en tiempo real
- Nueva carpeta con validación de nombre y caracteres inválidos
- Renombrar y eliminar (a Papelera de Reciclaje) desde el menú contextual
- Menú contextual con tema oscuro personalizado

### 🎵 Reproductor de Música
- Lista de reproducción con carga de toda la carpeta del archivo inicial
- **Shuffle** con orden pre-generado, **3 modos de repetición** (off / lista / pista única)
- Seek, volumen, mute con controles estilo Spotify
- Búsqueda automática de **carátulas de álbum** en iTunes, Last.fm y Spotify
- Guardado de carátula en el tag ID3 del archivo
- Búsqueda de **letras de canciones** via lrclib.net
- Edición de tags ID3 (título, artista, álbum, año, pista, género)
- **Grabación de micrófono** con NAudio y guardado en WAV
- Drag & Drop de archivos a la lista de reproducción
- Exportación e importación de playlists `.txt`

### 🎬 Reproductor de Video
- Motor LibVLC para soporte de todos los formatos comunes
- **Inicialización asíncrona** para no bloquear el hilo UI
- Lista de reproducción con Drag & Drop
- **3 modos de bucle** (off / lista / video único) con indicadores visuales distintos
- Velocidades de reproducción: 0.25× a 3×
- Pantalla completa con `F` o botón dedicado
- Extracción de metadatos (resolución, FPS, codec, audio, duración)
- Panel GPS integrado con mapa Leaflet/OpenStreetMap
- **Grabación de webcam** con OpenCvSharp y guardado en MP4

### 🖼️ Visor y Editor de Imágenes
- Soporte para más de 35 extensiones incluyendo RAW (CR2, NEF, ARW, DNG, etc.)
- Zoom libre (5% – 2000%) con rueda del ratón y pan
- Herramientas de dibujo: recorte, pincel, borrador, texto, cuentagotas
- Selector de fuente con preview en tiempo real para la herramienta de texto
- Transformaciones: rotar ±90°, voltear horizontal/vertical
- Filtros no destructivos: escala de grises, sepia, invertir colores
- **Deshacer** hasta 20 estados, restaurar original
- Exportar como PNG, JPEG o BMP
- **Panel GPS** con extracción de coordenadas EXIF y mapa embebido
- **Escritura de GPS** en EXIF de archivos JPEG/TIFF de forma atómica

### 📊 Visor y Analizador de Datos
- Soporte para CSV (RFC 4180 completo), JSON (arrays y objetos), XML, TXT (delimitador automático)
- **Análisis automático de calidad** de datos:
  - Detección de filas duplicadas
  - Normalización de fechas a `yyyy-MM-dd`
  - Validación y corrección de números de teléfono (10 dígitos)
  - Validación de emails
  - Detección de campos vacíos
  - Filas con columnas desajustadas (CSV)
- Celdas coloreadas por tipo de problema
- Filtrado por columna o texto libre en tiempo real
- Ordenamiento por cualquier columna
- **Gráficas interactivas**: columnas, barras horizontales, pastel — con selección de columna de agrupación, métrica (conteo, suma, promedio) y columna de valor
- Guardar copia corregida con todas las sugerencias aplicadas
- Exportación directa a una base de datos SQL abierta
- Exportación a CSV, JSON, TXT, XML, Excel, Word, PowerPoint, PDF
- Envío por email con adjunto

### 🗄️ Cliente SQL
- Conexión a **PostgreSQL**, **MariaDB** y **SQL Server** desde la misma interfaz
- Diálogo de conexión con soporte de Autenticación de Windows (SQL Server)
- Editor SQL con syntax-friendly y atajos de teclado (`F5`, `Ctrl+Enter`)
- Lista de tablas con doble clic para previsualizar
- Resultados en `DataGridView` con numeración de filas
- **Importación de archivos** CSV, JSON, XML, TXT directamente a una tabla
- Exportación de resultados a CSV, JSON, TXT, XML, Excel, Word, PowerPoint, PDF
- **Gráfica flotante** del resultado de la consulta con controles de tipo y métrica
- Indicador de tiempo de ejecución y filas afectadas

### 📦 Compresión y Descompresión
- **ZIP**: compresión y extracción completa (BCL .NET, sin dependencias externas)
- **7z, TAR, TAR.GZ, TAR.BZ2**: via SharpCompress
- **RAR**: solo extracción via SharpCompress (RAR 4.x y 5.x)
- Modos de extracción: "Extraer aquí" (aplana carpeta raíz única) y "Extraer en..."
- Protección **Zip Slip** (validación de path traversal en todas las entradas)
- Ventana de progreso con cancelación, temporizador y nombre del archivo actual
- Manejo de conflictos de nombre configurable (sobreescribir / omitir)
- Factory pattern extensible para nuevos formatos

### 📤 Exportación Nativa a Office y PDF
- **Excel (.xlsx)**: ClosedXML — sin límite práctico, fila de información, encabezado con filtros y panel congelado, anchos automáticos, colores alternos
- **Word (.docx)**: DocumentFormat.OpenXml — hasta 8,000 celdas, orientación automática landscape para tablas anchas
- **PowerPoint (.pptx)**: DocumentFormat.OpenXml — hasta 500 filas, diapositiva de portada + diapositivas de datos paginadas, tema Arctic Night
- **PDF (.pdf)**: QuestPDF — paginación automática, orientación automática, hasta 500,000 celdas
- Todos los exportadores: progreso animado, cancelación cooperativa, archivo parcial eliminado en fallo

### 🔐 Autenticación OAuth
- Login con **Google** (OpenID Connect, acceso/email/perfil)
- Login con **GitHub** (user:email, read:user)
- Servidor HTTP local en `localhost:5200` para el callback OAuth
- Sesión persistida en `AppData/FileExplorerr/session.json` (30 días)
- Avatar descargado y cacheado en `AppData/FileExplorerr/avatars/`
- Panel de cuenta flotante (estilo VS Code) con información de sesión, proveedor y opciones de cerrar/cambiar sesión
- Modo invitado disponible sin autenticación

### 📝 Bloc de Notas Avanzado
- Detección automática de encoding (UTF-8 BOM, UTF-16 LE/BE)
- Numeración de líneas con `OwnerDraw` sincronizada con el scroll
- Búsqueda con resaltado y navegación circular
- Reemplazar uno / todos (async para archivos grandes)
- Ir a línea (`Ctrl+G`)
- Zoom de fuente (6pt – 40pt) con `Ctrl++` / `Ctrl+-`
- Ajuste de línea togglable
- Protección al cerrar con cambios pendientes
- Guardado asíncrono sin bloquear UI

### 📧 Envío por Email
- Envío de archivos por SMTP con adjunto
- Soporte Gmail con Contraseña de Aplicación
- Configuración SMTP persistida en AppData (host, puerto, credenciales)
- Validación de dirección de destino

---

## 🏗️ Arquitectura del Sistema

```
┌──────────────────────────────────────────────────────────────┐
│                         UI Layer                              │
│   Forms · Dialogs · Components · Theme (Arctic Night)         │
│   Form1 · FileViewerForm · ImageViewerForm · MusicPlayerForm  │
│   VideoPlayerForm · NotepadForm · SqlViewerForm               │
├──────────────────────────────────────────────────────────────┤
│                      Services Layer                           │
│   FileOpener · FileOperationService · FileTypeHelper          │
│   ExportadorOffice · CompressionService                       │
├────────────────────┬─────────────────────────────────────────┤
│    Export Layer    │         Compression Layer                │
│  IOfficeExporter   │  IArchiver · ArchiverFactory             │
│  ExportOptions     │  ZipArchiver · SharpCompressArchiver     │
│  ExportResult      │  RarArchiver · ArchiveOptions            │
│  ExcelExporter     │  ArchiveResult                           │
│  WordExporter      │                                          │
│  PowerPointExporter│                                          │
│  PdfExporter       │                                          │
│  OfficeExporterFact│                                          │
├────────────────────┴─────────────────────────────────────────┤
│                       Data Layer                              │
│   DataParsers · DataQualityAnalyzer · DataSerializer          │
│   QualityReport · CsvParseResult · ChartDataBuilder           │
│   DataChartPanel                                              │
├──────────────────────┬───────────────────────────────────────┤
│      Core Layer      │          Media Layer                   │
│  FileExtensions      │  CoverSearcher · CoverSearchService    │
│  FileClassifier      │  CoverSearchResult                     │
│  FileStats           │  GpsReader · GpsWriter · GpsData       │
│  CsvIndexer          │  LyricsService                         │
│  AppHelpers          │                                        │
│  (FileSize, CsvHelper│                                        │
│   BrowserHelper,     │                                        │
│   SmtpConfig,        │                                        │
│   TimeSpanFormat)    │                                        │
├──────────────────────┴───────────────────────────────────────┤
│                      Database Layer                           │
│  IDbConnector · PostgreSqlConnector · MariaDbConnector        │
│  SqlServerConnector · SqlConnector (façade) · SqlWriteResult  │
├──────────────────────────────────────────────────────────────┤
│                        Auth Layer                             │
│  UserProfile · SessionManager · OAuthConfig                   │
│  LoginForm · AccountButton · AccountPanel                     │
└──────────────────────────────────────────────────────────────┘
```

### Flujo de Datos Principal

```
Usuario abre archivo
        │
        ▼
FileOpener.Open()
        │
   ┌────┴────────────────────────────────────────┐
   │                                             │
   ▼                                             ▼
Directorio                                    Archivo
NavigateToPath()                         (por extensión)
LoadDirectory()                               │
   │                              ┌────────────┼────────────┐
   ▼                              ▼            ▼            ▼
ListView                   ImageViewerForm  MusicPlayer  FileViewerForm
+ TreeView                 GpsReader        LyricsService DataParsers
+ StatusBar                GpsWriter        CoverSearch   DataQualityAnalyzer
                                                          DataChartPanel
```

---

## 📂 Estructura del Proyecto

```
FileExplorerr/
│
├── FileExplorerr.csproj          # Proyecto WinForms .NET 8 — dependencias NuGet
├── FileExplorerr.slnx            # Solución Visual Studio
├── Program.cs                    # Punto de entrada STAThread — OAuth, QuestPDF, arranque
├── appsettings.example.json      # Plantilla de configuración OAuth (no subir appsettings.json)
│
├── Auth/                         # Autenticación OAuth y gestión de sesión
│   ├── AccountButton.cs          # Botón de cuenta en la barra superior con avatar y menú
│   ├── AccountPanel.cs           # Panel flotante de cuenta (estilo Visual Studio)
│   ├── LoginForm.cs              # Formulario de login Google/GitHub con flujo OAuth completo
│   ├── OAuthConfig.cs            # Lector de credenciales desde appsettings.json
│   └── UserProfile.cs            # Modelo de usuario + SessionManager (persistencia de sesión)
│
├── Charts/                       # Visualización de datos
│   ├── ChartDataBuilder.cs       # Transformador DataTable → datos de gráfica (SRP)
│   └── DataChartPanel.cs         # Control GDI+ para columnas, barras horizontales y pastel
│
├── Core/                         # Lógica de dominio transversal
│   ├── AppHelpers.cs             # FileExtensions · FileSize · TimeSpanFormat · CsvHelper
│   │                             # BrowserHelper · SmtpConfig
│   ├── CsvIndexer.cs             # Generador de índice CSV recursivo y asíncrono
│   ├── FileClassifier.cs         # Clasificación de archivos por extensión → FileStats
│   ├── FileStats.cs              # Contadores con métodos de formateo (ToStatusString, ToInfoColumn)
│   └── QualityReport.cs          # DTO central del análisis de calidad de datos
│
├── Data/                         # Parsers, análisis y serialización de datos estructurados
│   ├── DataParsers.cs            # Parsers stateless: CSV · TXT · JSON · XML → DataTable
│   ├── DataQualityAnalizer.cs    # Análisis: duplicados · fechas · teléfonos · emails · vacíos
│   └── DatSerializer.cs          # Serialización DataTable → CSV · TSV · JSON · XML
│
├── DataBase/                     # Abstracción y conectores de bases de datos
│   ├── IDbConnector.cs           # Interfaz Strategy + enum DbConnectorType
│   ├── PostgreSqlConnector.cs    # Implementación Npgsql — async, transaccional
│   ├── MariaDbConnector.cs       # Implementación MySqlConnector — async, transaccional
│   ├── SqlServerConnector.cs     # Implementación Microsoft.Data.SqlClient — async, transaccional
│   ├── SqlConnector.cs           # Façade estático de compatibilidad legacy
│   └── SqlWriteResult.cs         # DTO de resultado de inserción masiva
│
├── Media/                        # Servicios multimedia y metadatos
│   ├── CoverSearcher.cs          # Búsqueda multi-fuente: iTunes · Last.fm · Spotify
│   │                             # Similitud Levenshtein + palabras, caché en memoria
│   ├── CoverSearchResult.cs      # DTO de resultado de búsqueda de carátulas
│   ├── CoverSearchService.cs     # Façade de alto nivel — FetchCoverBytesAsync, FetchFromITunesAsync
│   ├── GpsData.cs                # Record inmutable con coordenadas, altitud, cámara, fecha
│   ├── GpsReader.cs              # Extracción GPS de imágenes (EXIF) y videos (átomos QuickTime)
│   ├── GpsWriter.cs              # Escritura GPS en EXIF de JPEG/TIFF (atómica)
│   └── LyricsService.cs          # Búsqueda de letras via lrclib.net — retorna LyricsResult
│
├── Services/                     # Servicios de aplicación
│   │
│   ├── Export/                   # Exportadores nativos de documentos
│   │   ├── IOfficeExporter.cs    # Contrato Strategy — ExportAsync, nunca lanza excepciones
│   │   ├── ExportOptions.cs      # DTO inmutable con builder fluido y paleta Arctic Night
│   │   ├── ExportResult.cs       # DTO: Ok / Fail / Cancelled con metadatos de resultado
│   │   ├── OfficeExporterFactory.cs # Registro y resolución de exportadores por extensión
│   │   ├── ExcelExporter.cs      # .xlsx — ClosedXML, filtros, panel congelado, anchos automáticos
│   │   ├── WordExporter.cs       # .docx — DocumentFormat.OpenXml, orientación automática
│   │   ├── PowerPointExporter.cs # .pptx — DocumentFormat.OpenXml, portada + datos paginados
│   │   └── PdfExporter.cs        # .pdf  — QuestPDF, paginación automática, animación de progreso
│   │
│   ├── Compression/              # Compresión y descompresión de archivos
│   │   ├── IArchiver.cs          # Contrato Strategy — CompressAsync · ExtractAsync
│   │   ├── ArchiveOptions.cs     # DTO inmutable con builder fluido para opciones
│   │   ├── ArchiveResult.cs      # DTO: CompressOk · ExtractOk · Fail · Cancelled
│   │   ├── ArchiverFactory.cs    # Registro y resolución de archivers por extensión
│   │   ├── ZipArchiver.cs        # ZIP — System.IO.Compression, Zip Slip prevention
│   │   ├── SharpCompressArchiver.cs # 7z · TAR · TAR.GZ · TAR.BZ2 — SharpCompress
│   │   └── RarArchiver.cs        # RAR — solo extracción via SharpCompress
│   │
│   ├── ExportadorOffice.cs       # Façade público — ExportarConDialogo, ExportarExcel, etc.
│   ├── FileOpener.cs             # Enrutador de apertura de archivos al visor correcto
│   ├── FileOperationService.cs   # Crear carpeta · renombrar · eliminar · mover (DnD)
│   ├── FileTypeHelper.cs         # Etiquetas legibles por tipo + columna Info de carpetas
│   └── CompressionService.cs     # Façade público — Compress() y Extract() con diálogos
│
└── UI/
    ├── Components/               # Componentes reutilizables de UI
    │   ├── FileIconFactory.cs    # Iconos GDI+ 32×32 programáticos + resolución de ImageList
    │   ├── MinimalMenuRenderer.cs # Renderer ContextMenu Arctic Night + LvComparer
    │   └── Theme.cs              # Sistema de diseño "Arctic Night" — colores, fuentes,
    │                             # factory methods para Button, TextBox, Label, DataGridView
    │
    ├── Dialogs/                  # Cuadros de diálogo
    │   ├── CompressionProgressForm.cs # Progreso de compresión/extracción con cancelación
    │   ├── ConexionDialog.cs     # Conexión a BD (PostgreSQL · MariaDB · SQL Server)
    │   ├── EmailForm.cs          # Envío de archivo por SMTP con configuración embebida
    │   ├── ExportProgressForm.cs # Progreso de exportación Office/PDF con cancelación
    │   ├── ExtractOptionsDialog.cs # Opciones de extracción: "aquí" · subcarpeta · elegir
    │   ├── GpsEditDialog.cs      # Agregar/editar coordenadas GPS en imágenes
    │   ├── InputDialog.cs        # Diálogo genérico de entrada de texto
    │   ├── NombreTablaDialog.cs  # Nombre de tabla para importación a BD
    │   ├── TagEditDialog.cs      # Edición de tags ID3 con ComboBox de géneros
    │   └── TextToolDialog.cs     # Selector de fuente/estilo/color con preview en tiempo real
    │
    └── Forms/                    # Formularios principales
        ├── Form1.cs              # Ventana principal — navegación, ListView, TreeView, DnD
        ├── Form1.Designer.cs     # Diseñador de Form1
        ├── FilePropertiesForm.cs # Propiedades detalladas con carga asíncrona y toast de copiado
        ├── FileViewerform.cs     # Visor de datos — análisis, gráficas, filtros, exportación
        ├── ImagevIewerform.cs    # Visor/editor de imágenes — tools, filtros, GPS, SVG
        ├── MusicPlayerForm.cs    # Reproductor de música — estilo Spotify, grabación, letras
        ├── NotepadForm.cs        # Bloc de notas — line numbers, búsqueda async, zoom
        ├── SqlViewerForm.cs      # Cliente SQL — multi-motor, gráficas flotantes, importación
        └── VideoPlayerForm.cs    # Reproductor de video — LibVLC async, webcam, GPS
```

---

## 🔧 Módulos Principales

### `Core/AppHelpers.cs` — Helpers Centralizados

Fuente única de verdad para utilidades compartidas. Elimina implementaciones duplicadas que antes existían en múltiples formularios:

| Clase | Responsabilidad |
|---|---|
| `FileExtensions` | Sets de extensiones por categoría (Image, Audio, Video, Text, Document, Archive) |
| `FileSize` | Formateo de bytes a formato legible (B, KB, MB, GB, TB) |
| `TimeSpanFormat` | Formateo de duraciones (`1:23:45` o `3:07`) |
| `CsvHelper` | Split RFC 4180, escape de campos, split de líneas |
| `BrowserHelper` | Registro de emulación IE-Edge para WebBrowser embebido |
| `SmtpConfig` | Carga y persistencia de configuración SMTP en AppData |

### `Data/DataParsers.cs` — Parsers de Datos

Parsers estáticos y sin estado. Retornan `DataTable` (o `CsvParseResult` para CSV con metadatos de mismatches):

- **CSV**: respeta comillas, escapes de `""`, detecta filas con columnas desajustadas
- **TXT**: detecta automáticamente el delimitador (tab > pipe > semicolon > comma)
- **JSON**: maneja root array, root object con array anidado, y root object de escalares
- **XML**: extrae atributos (`@attr`) y elementos hijo como columnas; fallback a tabla plana

### `Data/DataQualityAnalyzer.cs` — Análisis de Calidad

Detecta seis tipos de problemas en un solo recorrido O(n):

1. **Duplicados**: hashing de filas completas
2. **Fechas**: detección de `dd/mm/yyyy`, `mm/dd/yyyy`, `yyyy.mm.dd` y normalización a ISO
3. **Campos vacíos**: null o whitespace
4. **Teléfonos**: heurística de nombre de columna + 60% de valores que parecen teléfonos
5. **Emails**: validación estructural con `@`, dominio y TLD
6. **Mismatches CSV**: pasados directamente desde el parser

### `Services/Export/` — Exportadores Nativos

Todos implementan `IOfficeExporter`. Reglas invariantes:
- Nunca lanzan excepciones — retornan `ExportResult.Fail()`
- Reportan progreso 0–100 via `IProgress<int>`
- Respetan `CancellationToken` y eliminan el archivo parcial si fallan

| Exportador | Límites | Características especiales |
|---|---|---|
| `ExcelExporter` | 1,048,575 filas | Filtros automáticos, panel congelado, anchos por muestreo |
| `WordExporter` | 8,000 celdas, 20 cols | Landscape automático para tablas anchas |
| `PowerPointExporter` | 500 filas, 20 cols, 18/diap | Portada con metadatos + diapositivas de datos paginadas |
| `PdfExporter` | 500,000 celdas | Timer de animación para QuestPDF síncrono |

### `Services/Compression/` — Archivers

Todos implementan `IArchiver`. Reglas invariantes:
- Nunca lanzan excepciones — retornan `ArchiveResult.Fail()`
- Validan path traversal (Zip Slip) en **cada entrada** antes de escribir
- Soportan `FlattenSingleRootFolder` (comportamiento "Extraer aquí" de WinRAR)

### `Media/CoverSearcher.cs` — Búsqueda de Carátulas

Algoritmo de similitud multi-fuente con pesos ponderados:

```
score = artista × 0.35 + título × 0.50 + palabras × 0.15

donde:
  artista, título = Levenshtein normalizado con potencia 0.8
  palabras        = coincidencia de palabras > 2 caracteres
```

Normalización: elimina diacríticos, stopwords musicales (feat., remix, official, etc.), contenido entre paréntesis/corchetes.

### `Auth/UserProfile.cs` + `SessionManager` — Autenticación

- Sesión guardada sin el `access_token` completo por seguridad (solo metadatos)
- Avatar cacheado 24 horas en `AppData/FileExplorerr/avatars/`
- Sesión válida 30 días; expirada, se limpia automáticamente

---

## 🎨 Funcionalidades en Detalle

### Autocompletado de la Barra de Dirección

El autocompletado es un `ListBox` flotante propio (no el nativo de Windows) para mantener el tema Arctic Night de forma consistente. Su comportamiento es:

- **Activación**: se dispara con el evento `TextChanged` solo cuando la ruta escrita contiene al menos una barra invertida `\`, indicando que el usuario está tecleando una ruta de sistema.
- **Filtrado en tiempo real**: obtiene el directorio padre de lo escrito con `Path.GetDirectoryName()` y lista las subcarpetas cuyo nombre empiece con el fragmento parcial ingresado, usando comparación sin distinción de mayúsculas/minúsculas.
- **Posicionamiento**: el menú se ancla directamente debajo de la barra de dirección usando coordenadas de pantalla, con un ancho equivalente al de la barra más el ícono de carpeta.
- **Navegación con teclado**: `↓` desde la barra mueve el foco al `ListBox`; `Enter` dentro del menú acepta la sugerencia y devuelve el foco a la barra; `Escape` cierra el menú sin navegar.
- **Clic con ratón**: seleccionar un ítem en el menú lo copia en la barra y cierra el desplegable.
- **Protección contra bucles**: cuando la aplicación cambia el texto de la barra de forma programática (historial, botones de navegación, favoritos de la barra lateral), desuscribe `TextChanged` antes de asignar el valor y lo vuelve a suscribir justo después, evitando que el menú aparezca durante navegaciones automáticas.
- **Cierre automático**: el menú desaparece si no hay coincidencias, si la ruta está vacía o si no contiene una barra invertida.

### Panel GPS (Imágenes y Video)

**Lectura de GPS:**
- Imágenes: EXIF via `System.Drawing.PropertyItem` (tags 0x0001–0x001D)
- Videos MP4/MOV: átomos QuickTime `©xyz`, `loci`, fallback a scan ISO 6709 en los primeros 50 MB
- Muestra coordenadas DMS formateadas, altitud, cámara y fecha

**Escritura de GPS** (solo JPEG/TIFF):
- Crea archivo temporal `.tmp_gps`, escribe EXIF, elimina original, renombra temporal
- Garantiza atomicidad: si falla, el original no se corrompe

**Mapa interactivo:**
- HTML embebido con Leaflet.js + OpenStreetMap en `WebBrowser`
- Emulación IE-Edge via registro de Windows para renderizado moderno

### Gráficas de Datos (`DataChartPanel`)

Control GDI+ personalizado que implementa tres tipos de gráfica:

- **Columnas**: barras verticales con gradiente, etiquetas rotadas 45°, grid horizontal
- **Barras**: barras horizontales con etiquetas truncadas, grid vertical
- **Pastel**: sectores con porcentaje embebido, leyenda lateral

Todos los tipos usan la paleta Arctic Night (10 colores) y adaptan sus márgenes automáticamente al ancho de las etiquetas.

### Árbol del Panel Lateral (`infoTree`)

- **Lazy-loading**: las subcarpetas se pueblan con un nodo `__dummy__` que se expande bajo demanda en `BeforeExpand`
- **Búsqueda recursiva asíncrona** con cancelación (al iniciar nueva búsqueda se cancela la anterior)
- **Clic simple en carpeta**: expande/colapsa sin navegar el explorador principal
- **Doble clic en archivo**: abre el visor correspondiente
- **OwnerDraw personalizado**: colores por tipo de nodo (carpeta ámbar, archivo blanco, grupo teal, dim gris)

### Drag & Drop Extendido

- **Entre carpetas**: resaltado visual del destino, manejo de conflictos de nombre interactivo
- **A la papelera**: cambio de ícono a papelera llena (shell32.dll, índice 32) al hover
- **A la playlist de video**: acepta archivos de video con `DragEnter`/`DragDrop`
- **A la playlist de música**: acepta archivos de audio

---

## 🧩 Diseño y Patrones

### Strategy Pattern

```csharp
// Exportación
IOfficeExporter exporter = OfficeExporterFactory.Resolve(".xlsx");
ExportResult result = await exporter.ExportAsync(data, options, progress);

// Compresión
IArchiver archiver = ArchiverFactory.Resolve(".zip");
ArchiveResult result = await archiver.CompressAsync(options, progress);

// Base de datos
IDbConnector connector = new PostgreSqlConnector(connectionString);
var (dt, rows) = await connector.ExecuteAsync(sql);
```

### Factory Pattern

`OfficeExporterFactory` y `ArchiverFactory` son registros de instancias únicas por extensión. Agregar un nuevo formato requiere una sola línea:

```csharp
OfficeExporterFactory.Register(new CsvExporter()); // ejemplo futuro
ArchiverFactory.Register(new SevenZipArchiver());   // ejemplo futuro
```

### Façade Pattern

`SqlConnector` (façade estático), `ExportadorOffice` y `CompressionService` exponen APIs simples orientadas a formularios, delegando internamente a la implementación concreta:

```csharp
// Form1 solo llama:
CompressionService.Compress(selectedPaths, this, () => LoadDirectory(currentPath));

// Internamente: ArchiverFactory → ZipArchiver → ArchiveOptions → ArchiveResult
```

### Single Responsibility

Cada clase tiene una responsabilidad bien definida:
- `DataParsers`: solo parsea, no analiza calidad
- `DataQualityAnalyzer`: solo analiza, no parsea
- `DataSerializer`: solo serializa, no parsea ni analiza
- `ChartDataBuilder`: solo convierte DataTable en datos de gráfica
- `DataChartPanel`: solo dibuja, no genera datos

### Fluent Builder (Immutable DTOs)

```csharp
var opts = ExportOptions.For(outputPath, "Mi título")
                        .WithMaxRows(5_000)
                        .WithTimestamp(true)
                        .WithCancellation(cts.Token)
                        .Build();

var archiveOpts = ArchiveOptions
    .ForExtraction(archivePath, destination)
    .WithOverwrite(false)
    .WithFlattenSingleRoot()
    .WithCancellation(cts.Token)
    .Build();
```

### Result Object (No-Throw Pattern)

Todos los exportadores y archivers retornan un objeto de resultado en lugar de lanzar excepciones:

```csharp
ExportResult result = await exporter.ExportAsync(data, options, progress);
if (result.Success)           { /* abrir archivo */ }
else if (result.WasTruncated) { /* mostrar advertencia */ }
else                          { MessageBox.Show(result.ErrorMessage); }
```

### Async/Await con Cooperative Cancellation

Las operaciones pesadas (carga de directorios, exportación, análisis de calidad, búsqueda en árbol) se ejecutan en el thread pool y soportan cancelación cooperativa:

```csharp
_loadCts.Cancel();          // cancela la carga anterior
_loadCts = new CancellationTokenSource();
var token = _loadCts.Token;
// ...
if (token.IsCancellationRequested) return;
```

---

## 🛠️ Tecnologías Utilizadas

### Plataforma y Lenguaje

| Tecnología | Versión | Uso |
|---|---|---|
| C# | 12 | Lenguaje principal |
| .NET | 8.0 (Windows) | Runtime y BCL |
| Windows Forms | .NET 8 | Framework de UI |

### Multimedia

| Paquete | Versión | Uso |
|---|---|---|
| `LibVLCSharp` | 3.9.7.1 | Motor de reproducción de video |
| `LibVLCSharp.WinForms` | 3.9.3 | Control `VideoView` |
| `VideoLAN.LibVLC.Windows` | 3.0.21 | Binarios nativos VLC |
| `OpenCvSharp4` | 4.13.0+ | Captura y grabación de webcam |
| `OpenCvSharp4.Extensions` | 4.13.0+ | Conversión `Mat` → `Bitmap` |
| `OpenCvSharp4.runtime.win` | 4.13.0+ | Binarios nativos OpenCV |
| `NAudio` | 2.2.1 | Reproducción de audio PCM, grabación de micrófono |
| `taglib-sharp-netstandard2.0` | 2.1.0 | Lectura/escritura de tags ID3, Vorbis, APE, M4A |

### Exportación de Documentos

| Paquete | Versión | Uso |
|---|---|---|
| `ClosedXML` | 0.102.2 | Generación de Excel (.xlsx) |
| `DocumentFormat.OpenXml` | 2.20.0 | Generación de Word (.docx) y PowerPoint (.pptx) |
| `QuestPDF` | 2024.12.0 | Generación de PDF — licencia Community MIT |

### Compresión

| Paquete | Versión | Uso |
|---|---|---|
| `SharpCompress` | 0.49.1 | 7z, TAR, TAR.GZ, TAR.BZ2, RAR (extracción) |
| `System.IO.Compression` | BCL | ZIP (sin dependencias externas) |

### Bases de Datos

| Paquete | Versión | Uso |
|---|---|---|
| `Npgsql` | 9.0.2 | Cliente PostgreSQL async (ADO.NET) |
| `MySqlConnector` | 2.3.7 | Cliente MariaDB/MySQL async (ADO.NET) |
| `Microsoft.Data.SqlClient` | 5.2.2 | Cliente SQL Server async (ADO.NET) |

### APIs Externas (Opcionales)

| API | Uso | Autenticación |
|---|---|---|
| iTunes Search API | Carátulas de álbumes | Sin clave |
| lrclib.net | Letras de canciones | Sin clave |
| Last.fm API | Carátulas alternativas | Sin clave (solo búsqueda) |
| Spotify API | Carátulas alternativas | Best-effort sin auth |
| OpenStreetMap / Leaflet.js | Mapas GPS embebidos | Sin clave |
| Google OAuth 2.0 | Autenticación de usuario | Client ID + Secret |
| GitHub OAuth | Autenticación de usuario | Client ID + Secret |

### P/Invoke y APIs de Windows

| API | Uso |
|---|---|
| `SHFileOperation` | Envío a Papelera de Reciclaje |
| `DwmSetWindowAttribute` | Título de barra oscuro, color de acento |
| `ExtractIcon` | Ícono de papelera desde shell32.dll |

---

## 📥 Instalación

### Requisitos

- **Sistema operativo**: Windows 10 / 11 (x64)
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) o superior
- **IDE** (para compilar): Visual Studio 2022 v17.8+ o VS Code con C# Dev Kit
- Conexión a internet opcional (carátulas, letras, mapas GPS)

### Clonar el Repositorio

```bash
git clone https://github.com/tu-usuario/FileExplorerr.git
cd FileExplorerr
```

### Restaurar Dependencias

```bash
dotnet restore
```

### Compilar

```bash
# Debug
dotnet build

# Release
dotnet build -c Release
```

### Ejecutar

```bash
dotnet run --project FileExplorerr/FileExplorerr.csproj
```

O desde Visual Studio: abrir `FileExplorerr.slnx` y presionar `F5`.

---

## ⚙️ Configuración

### Configuración OAuth (Opcional)

Para habilitar el login con Google y GitHub, copia el archivo de ejemplo y rellena tus credenciales:

```bash
cp FileExplorerr/appsettings.example.json FileExplorerr/appsettings.json
```

Edita `appsettings.json`:

```json
{
  "OAuth": {
    "Google": {
      "ClientId": "TU_GOOGLE_CLIENT_ID",
      "ClientSecret": "TU_GOOGLE_CLIENT_SECRET"
    },
    "GitHub": {
      "ClientId": "TU_GITHUB_CLIENT_ID",
      "ClientSecret": "TU_GITHUB_CLIENT_SECRET"
    }
  }
}
```

> **⚠️ Importante**: `appsettings.json` está en `.gitignore` y nunca debe subirse al repositorio.

#### Obtener credenciales Google

1. Ve a [Google Cloud Console](https://console.cloud.google.com/)
2. Crea un proyecto nuevo o selecciona uno existente
3. Habilita la **Google OAuth 2.0 API**
4. Crea credenciales tipo "Aplicación de escritorio"
5. Agrega `http://localhost:5200/callback` como URI de redirección autorizado

#### Obtener credenciales GitHub

1. Ve a [GitHub Developer Settings](https://github.com/settings/developers)
2. Crea una nueva **OAuth App**
3. Establece `http://localhost:5200/callback` como Authorization callback URL

### Modo Sin Autenticación

Si no configuras OAuth, puedes usar la aplicación completa con la opción **"Continuar como invitado"** en la pantalla de login.

### Configuración SMTP (Opcional)

Para el envío de archivos por email, configura tus credenciales desde dentro de la aplicación: al abrir `EmailForm`, usa el botón **"⚙ Configurar SMTP"**. La configuración se guarda en `AppData\Roaming\FileExplorerr\smtp.cfg`.

Para Gmail, necesitas una [Contraseña de Aplicación](https://support.google.com/accounts/answer/185833) de 16 caracteres.

---

## 🚀 Uso

### Primera Ejecución

1. La aplicación muestra la pantalla de login
2. Elige autenticarte con Google, GitHub, o continuar como invitado
3. La ventana principal se abre en tu carpeta de usuario
4. En siguientes ejecuciones, la sesión se restaura automáticamente (válida 30 días)

### Navegación

```
📁 Carpeta de usuario
├── Usa la barra de dirección para navegar directamente
├── Escribe una ruta parcial para ver sugerencias de autocompletado
├── Haz doble clic en carpetas para entrar
├── Botones ← → ↑ para historial y subir nivel
├── F5 para actualizar
└── Panel lateral derecho para árbol categorizado
```

### Apertura de Archivos

| Tipo de archivo | Acción | Resultado |
|---|---|---|
| Imagen (JPG, PNG, RAW...) | Doble clic | Abre `ImageViewerForm` |
| Audio (MP3, FLAC...) | Doble clic | Abre `MusicPlayerForm` con toda la carpeta |
| Video (MP4, MKV...) | Doble clic | Abre `VideoPlayerForm` |
| CSV, JSON, XML | Doble clic | Abre `FileViewerForm` con análisis automático |
| TXT, LOG | Doble clic | Pregunta: visor de tabla o bloc de notas |
| Código fuente (CS, PY...) | Doble clic | Abre `NotepadForm` |
| ZIP, RAR, 7z | Doble clic → menú contextual | Opciones de extracción |
| Cualquier otro | Doble clic | Aplicación predeterminada del sistema |

### Menú Contextual

Clic derecho sobre cualquier elemento del explorador:

```
Abrir                          → abre con el visor correspondiente
Nueva carpeta
Renombrar
Eliminar                       → envía a Papelera de Reciclaje
Propiedades                    → panel detallado con metadatos
Actualizar (F5)
📦 Comprimir selección...      → crea ZIP, 7z, TAR, etc.
📂 Extraer aquí                → solo para archivos de compresión
📁 Extraer en...               → solo para archivos de compresión
```

### Visor de Datos — Flujo de Trabajo

```
1. Abre un archivo CSV/JSON/XML
2. El análisis de calidad se ejecuta automáticamente
3. Un popup muestra los problemas detectados
4. Las celdas con problemas aparecen coloreadas:
   🔴 Duplicados  🟡 Vacíos  🔵 Fechas  🟣 Teléfonos  🟠 Emails
5. Aplica filtros o busca en los datos
6. Visualiza en la pestaña "Gráfica" con los controles deseados
7. Exporta al formato deseado con los botones del panel inferior
8. O guarda una "copia corregida" con todas las sugerencias aplicadas
```

---

## ⌨️ Atajos de Teclado

### Explorador Principal

| Atajo | Acción |
|---|---|
| `F5` | Actualizar directorio |
| `Enter` en barra de dirección | Navegar a la ruta escrita |
| `↓` en barra de dirección | Abrir menú de autocompletado y mover foco a sugerencias |
| `Enter` en menú de autocompletado | Aceptar sugerencia seleccionada |
| `Escape` en menú de autocompletado | Cerrar menú y volver a la barra |
| `Enter` en búsqueda del panel | Buscar en árbol lateral |

### Visor de Imágenes

| Atajo | Acción |
|---|---|
| `+` / `−` | Zoom in / Zoom out |
| `Rueda del ratón` | Zoom in / Zoom out |
| `Ctrl+Z` | Deshacer |
| `Ctrl+S` | Guardar copia |
| `Escape` | Deseleccionar herramienta / Cerrar |

### Reproductor de Música

| Atajo | Acción |
|---|---|
| `Espacio` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 5 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |

### Reproductor de Video

| Atajo | Acción |
|---|---|
| `Espacio` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 10 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |
| `M` | Silenciar / Activar audio |
| `F` | Pantalla completa / Ventana |
| `Escape` | Salir de pantalla completa |

### Bloc de Notas

| Atajo | Acción |
|---|---|
| `Ctrl+S` | Guardar |
| `Ctrl+Shift+S` | Guardar como |
| `Ctrl+F` | Buscar |
| `Ctrl+H` | Reemplazar |
| `Ctrl+G` | Ir a línea |
| `F3` | Siguiente coincidencia |
| `Ctrl++` / `Ctrl+-` | Zoom de fuente |
| `Escape` | Cerrar panel de búsqueda |

### Visor SQL

| Atajo | Acción |
|---|---|
| `F5` | Ejecutar consulta |
| `Ctrl+Enter` | Ejecutar consulta |

---

## ⚡ Rendimiento

### Carga de Directorios

- Las operaciones de disco (`GetDirectories`, `GetFiles`) se ejecutan en el thread pool con `Task.Run`
- La información de carpetas (`FolderInfoColumn`) se calcula con un semáforo de concurrencia limitada (máx. 8 tareas paralelas)
- La carga se puede cancelar al navegar a otra carpeta: cada llamada a `LoadDirectory` cancela la anterior con `CancellationTokenSource`
- La UI se actualiza con `BeginUpdate()`/`EndUpdate()` para evitar flickering

### Análisis de Datos

- Los parsers son stateless y se ejecutan en background con `Task.Run`
- El analizador de calidad hace un único recorrido O(n) sobre las filas
- La detección de duplicados usa `Dictionary<string, int>` con hashing directo
- Las gráficas se redibujan con `DoubleBuffered = true` y `ResizeRedraw = true`

### Búsqueda en Árbol Lateral

- La búsqueda recursiva usa `CancellationTokenSource` enlazado: si el usuario escribe antes de que termine, la búsqueda anterior se cancela
- El `TreeView` usa `BeginUpdate()`/`EndUpdate()` para poblar sin flickering

### Exportación

- `ExcelExporter` cancela cooperativamente cada 3,000 filas
- `PdfExporter` usa un timer de animación paralelo porque QuestPDF es síncrono
- Todos los exportadores eliminan el archivo parcial si fallan o se cancelan

---

## 🤝 Contribución

### Cómo Contribuir

1. **Fork** del repositorio
2. Crea una rama para tu funcionalidad:
   ```bash
   git checkout -b feature/nueva-funcionalidad
   ```
3. Realiza tus cambios siguiendo las convenciones del proyecto
4. Asegúrate de que el proyecto compile sin errores
5. Haz **commit** con mensajes descriptivos:
   ```bash
   git commit -m "feat(visor): agregar soporte para formato AVIF"
   ```
6. Sube tu rama:
   ```bash
   git push origin feature/nueva-funcionalidad
   ```
7. Abre un **Pull Request** describiendo los cambios

### Convenciones del Proyecto

- **Nombres**: PascalCase para clases y métodos, camelCase para variables locales
- **Async**: todos los métodos que accedan a disco o red deben ser `async Task<>`
- **Sin excepciones en exportadores/archivers**: usar el patrón Result Object
- **Single Responsibility**: una clase, una responsabilidad
- **Sin dependencias circulares** entre capas (UI → Services → Core, no al revés)
- Los nuevos exportadores deben implementar `IOfficeExporter` y registrarse en `RegisterNativeExporters()`
- Los nuevos archivers deben implementar `IArchiver` y registrarse en `RegisterBuiltInArchivers()`

### Reportar Bugs

Abre un issue con:
- Descripción del problema
- Pasos para reproducirlo
- Versión de Windows y .NET
- Captura de pantalla si aplica

---

## 📄 Licencia

Este proyecto está bajo la licencia MIT. Consulta el archivo [LICENSE](LICENSE) para más detalles.

Las dependencias tienen sus propias licencias:
- **QuestPDF**: Community MIT (libre para ingresos < $1M USD/año)
- **LibVLC**: LGPL 2.1
- **SharpCompress**: MIT
- **ClosedXML**: MIT
- **DocumentFormat.OpenXml**: MIT
- **NAudio**: MIT
- **Npgsql, MySqlConnector, Microsoft.Data.SqlClient**: Apache 2.0 / MIT

---

<div align="center">


</div>
