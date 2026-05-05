# FileExplorer

> Explorador de archivos de escritorio avanzado, construido con **C# / .NET 8** y **Windows Forms**, con tema oscuro "Arctic Frost", visualizadores integrados para múltiples formatos, reproductor de música y video, y herramientas de edición de imágenes.

---

## Tabla de contenidos

- [Vista general](#vista-general)
- [Características](#características)
- [Requisitos](#requisitos)
- [Instalación y compilación](#instalación-y-compilación)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Módulos y componentes](#módulos-y-componentes)
- [Visualizadores integrados](#visualizadores-integrados)
- [Atajos de teclado](#atajos-de-teclado)
- [Dependencias](#dependencias)
- [Tecnologías](#tecnologías)

---

## Vista general

FileExplorerr es un reemplazo del Explorador de Windows con una interfaz oscura minimalista y funcionalidades que van más allá de la navegación básica. Incluye un reproductor de música con letras y carátulas automáticas, un visor/editor de imágenes con GPS, un reproductor de video con metadatos, un visor de archivos de datos (CSV/JSON/XML) con análisis automático, y un bloc de notas con numeración de líneas.

---

## Características

### Navegación principal
- **Barra de dirección editable** — escribe una ruta y presiona `Enter` para navegar directamente
- **Historial de navegación** — botón `←` para volver a directorios anteriores
- **Subir nivel** — botón `↑` para ir al directorio padre
- **Actualización** — botón `↻` o `F5` para recargar el directorio actual
- **Doble clic** en carpetas para entrar, en archivos para abrirlos con el visualizador apropiado

### Gestión de archivos
- **Nueva carpeta** — validación de nombre y caracteres inválidos
- **Renombrar** — desde el menú contextual (clic derecho)
- **Eliminar** — envía a la Papelera de Reciclaje de Windows vía `SHFileOperation` (recuperable)
- **Mover mediante Drag & Drop** — arrastra sobre otra carpeta para moverla; la carpeta destino se resalta en color azul-teal
- **Manejo de conflictos** — al mover, si el nombre ya existe ofrece sobreescribir, saltar o cancelar
- **Protección recursiva** — no permite mover una carpeta dentro de sí misma

### Papelera de reciclaje integrada
- Panel en la esquina inferior derecha con icono de papelera del sistema
- Arrastra archivos o carpetas sobre él para eliminarlos con confirmación
- El icono cambia al estado "llena" al hacer hover

### Panel lateral derecho
El panel derecho opera en dos modos:

**Modo árbol (normal):** Al navegar a un directorio, muestra su contenido completo en un `TreeView` con propietario de dibujo personalizado. Organiza los archivos por categorías expandibles:
- 📁 Carpetas (con expansión lazy-load para subcarpetas)
- 🖼️ Imágenes
- 🎵 Audio
- 🎬 Video
- 📝 Texto / Código
- 📄 Documentos
- 📦 Otros

**Modo búsqueda:** Escribe en la barra y presiona `Enter` para buscar recursivamente por nombre de archivo o carpeta dentro del directorio actual. Los resultados muestran la ruta relativa y son expandibles. Vaciar el campo y buscar vuelve al modo árbol.

**Colores del TreeView:**
| Color | Elemento |
|---|---|
| Teal (acento) | Headers de grupo |
| Gris claro | Categorías de archivos |
| Amarillo cálido | Carpetas individuales |
| Blanco | Archivos individuales |
| Gris | Mensajes de estado (vacía, sin acceso) |

### Estadísticas en barra de estado
Al cargar cualquier carpeta la barra inferior muestra el desglose completo de contenido:
```
📁 4 carpetas  ·  📄 30 archivos  ·  🖼️ 12  ·  🎵 3  ·  🎬 5  ·  📝 8  ·  📦 2
```
La columna **Info** de cada fila también muestra un resumen por categoría: `3 sub, 12 img, 5 txt`.

### Exportación de índice CSV
El botón **Exportar CSV** recorre el directorio actual de forma recursiva y genera un archivo `.csv` con una fila por archivo encontrado:

```csv
"Ruta Carpeta","Nombre Carpeta","Nombre Archivo","Extensión","Tamaño","Último Acceso"
"C:\Fotos\Vacaciones","Vacaciones","playa.jpg","JPG","2.4 MB","15/01/2025 18:22"
```

- La generación es completamente **asíncrona** — la barra de estado muestra la carpeta en proceso en tiempo real
- El botón se deshabilita durante la generación
- Al finalizar ofrece abrir el archivo directamente

---

## Módulos y componentes

### `Form1.cs` — Ventana principal
Corazón de la aplicación. Gestiona:
- Toda la navegación y el ciclo de carga de directorios
- El `ListView` con `OwnerDraw` para cabeceras personalizadas
- El panel lateral `TreeView` con lazy-loading de subcarpetas
- Drag & Drop completo (entre carpetas y hacia la papelera)
- El menú contextual con renderer personalizado
- Título de barra con colores DWM (`DwmSetWindowAttribute` vía P/Invoke)
- Apertura de cada tipo de archivo en su visualizador correspondiente

### `CsvIndexer.cs` — Generador de índice y estadísticas
Clase estática con dos responsabilidades:
1. **`GenerateAsync`** — Recorre recursivamente un directorio y produce el contenido CSV completo en un hilo de fondo, reportando progreso via `IProgress<string>`
2. **`ClassifyFiles` / `ClassifyByExtensions`** — Clasifica arrays de archivos por tipo (imagen, audio, video, texto, otro) y devuelve un `FileStats` con métodos para generar cadenas de estado y columnas de info

### `FileViewerForm.cs` — Visor de archivos de datos
Abre y visualiza archivos `.csv`, `.json`, `.xml`, `.txt` (con delimitador), `.log` en un `DataGridView` con:
- **Parsers propios** para CSV (respeta comillas y escapes), JSON (arrays, objetos simples y objetos con array anidado), XML (mapea atributos y elementos hijo como columnas) y TXT con detección automática de delimitador
- **Análisis automático** al cargar:
  - Detección de **filas duplicadas** (resaltadas en rojo)
  - Detección y propuesta de corrección de **fechas con formato inconsistente** → normaliza a `yyyy-MM-dd` (resaltadas en azul)
  - Detección de **campos vacíos** (resaltados en amarillo)
  - Popup informativo con el resumen del análisis
- **Formato de celdas numéricas**: detecta columnas con ≥80% de valores numéricos y aplica formato `N2`; si el nombre de la columna contiene palabras clave monetarias (`price`, `costo`, `total`, etc.) agrega prefijo `$`
- **Filtrado** por columna o en todas con búsqueda de texto
- **Ordenamiento** por columna al hacer clic en la cabecera
- **Guardar copia corregida** — aplica todas las correcciones detectadas y elimina duplicados
- **Exportar** a CSV, JSON, TXT (TSV) o XML

### `ImageViewerForm.cs` — Visor y editor de imágenes
Abre más de 30 formatos de imagen (JPEG, PNG, GIF, BMP, TIFF, ICO, WebP, RAW, SVG, EMF, HEIC, etc.) con:
- **Zoom** libre con rueda del ratón o botones (+/−/1:1/Ajustar)
- **Pan** con clic central o arrastre sin herramienta
- **Herramientas de edición:**
  - ✂ **Recorte** — selección rectangular con overlay semitransparente y confirmación
  - ✏ **Dibujo libre** — pincel con color y tamaño configurables
  - ◻ **Borrador** — pinta en blanco
  - **T Texto** — abre `TextToolDialog` para elegir fuente, tamaño, estilo, color y vista previa
  - ◉ **Cuentagotas** — captura el color de un píxel de la imagen
- **Transformaciones:** Rotar ±90°, voltear horizontal/vertical
- **Filtros:** Escala de grises, Sepia, Invertir colores
- **Deshacer** — pila de hasta 20 estados
- **Restaurar original**
- **Panel GPS** — lee coordenadas EXIF de la imagen y muestra un mapa Leaflet (OpenStreetMap) interactivo embebido en un `WebBrowser`
- **Guardar copia** en PNG, JPEG o BMP

### `GpsReader.cs` — Extractor de GPS
Lee coordenadas GPS de imágenes y videos:
- **Imágenes:** Lee EXIF directamente con `System.Drawing` (lat, lon, altitud, referencia N/S/E/W, fecha, cámara, software)
- **Videos MP4/MOV:** Busca átomos QuickTime `©xyz` (iPhone), `loci`, y patrones ISO 6709 en los primeros 50 MB del archivo. También extrae la fecha de `©day` y como fallback lee el timestamp de creación del átomo `mvhd`
- Convierte coordenadas a formato DMS (grados, minutos, segundos)
- Devuelve un `record GpsData` inmutable con todos los metadatos

### `MusicPlayerForm.cs` — Reproductor de música
Reproductor completo usando **NAudio** para audio y **TagLib** para metadatos:
- Carga automáticamente todos los archivos de audio de la misma carpeta del archivo inicial
- **Lista de reproducción** editable — agregar archivos individuales por diálogo o arrastrar, quitar pistas
- **Controles:** Play/Pausa, Anterior, Siguiente, Seek, Volumen
- **Modos:** Aleatorio (shuffle) con orden pre-generado; Repetir lista / Repetir pista / Sin repetir
- **Carátulas:** Lee desde los tags ID3 del archivo; si no hay, consulta la **iTunes Search API** y guarda la imagen descargada de vuelta en el tag del archivo
- **Letras:** Busca en **lrclib.net** y muestra en panel lateral
- **Edición de tags** (`TagEditDialog`): Título, Artista, Álbum, Año, Nº de pista, Género (con combobox de géneros comunes)
- **Guardar / Cargar playlist** en archivos `.txt` con rutas absolutas
- Normalización de nombres de artista (elimina "- Topic", "VEVO") y títulos (elimina "(Official Audio)", "[HD]", etc.)

### `VideoPlayerForm.cs` — Reproductor de video
Usa **Windows Media Player** vía COM (`WMPLib`) embebido en un control `AxHost` personalizado (`WmpControl`):
- Soporta MP4, AVI, MKV, MOV, WMV, WebM, FLV, TS, 3GP y más
- **Lista de reproducción** con Drag & Drop
- **Controles:** Play/Pausa, Stop, Anterior, Siguiente, Seek, Volumen, Mute
- **Velocidad de reproducción:** 0.5x, 0.75x, 1x, 1.25x, 1.5x, 2x
- **Modo bucle** y **Pantalla completa** (oculta todos los paneles, salvo el video)
- **Panel de propiedades:** Nombre, duración, tamaño, formato, resolución, FPS, codec de video, codec de audio, canales/sample rate. Lee metadatos de WMP, con fallback al Shell de Windows (IShellFolder2) y fallback final leyendo átomos `stsd` del MP4 directamente para codecs `avc1`, `hvc1`, `mp4a`, `alac`, etc.
- **Panel GPS** — igual que en el visor de imágenes, con mapa Leaflet

### `CoverSearcher.cs` — Buscador de carátulas
Motor multi-fuente para buscar carátulas de álbumes/canciones:
- Consulta en paralelo: **iTunes Search API**, **Last.fm API**, **Spotify API**
- Algoritmo de similitud avanzado que combina distancia Levenshtein, similitud por palabras y pesos ponderados (artista 35%, título 50%, palabras 15%)
- Preprocesamiento del texto: normalización Unicode, extracción del artista principal (elimina "feat.", "ft."), eliminación de stopwords musicales ("remix", "official", "remastered", etc.), limpieza de paréntesis y corchetes
- Caché en memoria con clave normalizada
- Retorna el resultado con mayor similitud si supera umbral de 0.5

### `NotepadForm.cs` — Bloc de notas
Editor de texto plano con:
- Detección automática de encoding (UTF-8 BOM, UTF-16 LE/BE)
- Fuente monoespaciada Cascadia Code
- **Numeración de líneas** con `OwnerDraw` sincronizada con el scroll
- **Ajuste de líneas** (word wrap) configurable — oculta el panel de numeración cuando está activo
- **Buscar** con resaltado y navegación circular; **Reemplazar uno** / **Reemplazar todos**
- **Ir a línea** (`Ctrl+G`)
- **Zoom** de fuente (`Ctrl++` / `Ctrl+-`)
- Barra de estado con: línea/columna actual, total de líneas, palabras, caracteres y encoding
- Indicador de cambios sin guardar (`●` en el título)
- Protección al cerrar si hay cambios sin guardar

### `Theme.cs` — Sistema de diseño "Arctic Frost"
Clase estática centralizada con:
- **Paleta de colores** completa: fondos en 4 capas (base, surface, elevated, hover), acento teal, semánticos (danger, success, warning) con variantes dim para fondos
- **Tipografía** con 9 variantes (body, bold, small, mono, icon, etc.)
- **Factory methods** para crear controles con estilo consistente: `MakeButton`, `MakeIconButton`, `MakeTextBox`, `MakeLabel`, `MakeDivider`, `StyleGrid`
- Enums `ButtonKind` (Default, Primary, Danger, Success, Ghost) y `LabelKind`

---

## Visualizadores integrados

| Tipo | Extensiones soportadas | Visor |
|---|---|---|
| Imagen | `.jpg` `.jpeg` `.jfif` `.png` `.gif` `.bmp` `.webp` `.tiff` `.ico` `.svg` `.raw` `.cr2` `.nef` `.arw` `.dng` `.heic` y más | `ImageViewerForm` con editor |
| Audio | `.mp3` `.wav` `.wma` `.m4a` `.flac` `.aac` `.ogg` `.opus` `.aiff` | `MusicPlayerForm` |
| Video | `.mp4` `.avi` `.mkv` `.mov` `.wmv` `.flv` `.webm` `.ts` `.3gp` `.divx` | `VideoPlayerForm` |
| CSV | `.csv` | `FileViewerForm` con análisis |
| JSON | `.json` | `FileViewerForm` con análisis |
| XML | `.xml` | `FileViewerForm` con análisis |
| Texto / Log | `.txt` `.log` | Elección entre `FileViewerForm` o `NotepadForm` |
| Otros | Cualquier extensión | Abre con la aplicación predeterminada del sistema |

---

## Atajos de teclado

### Ventana principal
| Atajo | Acción |
|---|---|
| `Enter` en barra de dirección | Navegar a la ruta escrita |
| `Enter` en barra de búsqueda | Buscar en panel lateral |
| `F5` | Actualizar directorio actual |

### Visor de imágenes
| Atajo | Acción |
|---|---|
| `+` / `−` | Zoom in / Zoom out |
| `Ctrl+Z` | Deshacer |
| `Ctrl+S` | Guardar copia |
| `Escape` | Deseleccionar herramienta / Cerrar |

### Reproductor de música
| Atajo | Acción |
|---|---|
| `Space` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 5 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |

### Reproductor de video
| Atajo | Acción |
|---|---|
| `Space` | Play / Pausa |
| `←` / `→` | Retroceder / Avanzar 10 s |
| `↑` / `↓` | Subir / Bajar volumen 5% |
| `M` | Silenciar / Activar audio |
| `F` | Pantalla completa / Ventana |
| `Escape` | Salir de pantalla completa |

### Bloc de notas
| Atajo | Acción |
|---|---|
| `Ctrl+S` | Guardar |
| `Ctrl+Shift+S` | Guardar como |
| `Ctrl+F` | Abrir búsqueda |
| `Ctrl+H` | Abrir reemplazar |
| `Ctrl+G` | Ir a línea |
| `F3` | Siguiente coincidencia |
| `Ctrl++` / `Ctrl+-` | Zoom de fuente |
| `Escape` | Cerrar panel de búsqueda |

---

## Requisitos

- **Sistema operativo:** Windows 10 / 11 (x64 recomendado)
- **Runtime:** .NET 8.0 (Windows)
- **Windows Media Player** instalado (para el reproductor de video)
- Conexión a internet opcional (para carátulas automáticas, letras y mapas GPS)

---

## Instalación y compilación

```bash
# Clonar el repositorio
git clone <repo-url>
cd FileExplorerr

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build -c Release

# Ejecutar
dotnet run --project FileExplorerr/FileExplorerr.csproj
```

O abrir `FileExplorerr.slnx` en **Visual Studio 2022** (v17.8+) y compilar con `Ctrl+Shift+B`.

---

## Estructura del proyecto

```
FileExplorerr/
│
├── FileExplorerr.csproj        # Proyecto WinForms .NET 8 con refs COM y NuGet
├── Program.cs                  # Punto de entrada — STAThread, Application.Run
│
├── Form1.cs                    # Ventana principal: navegación, ListView, drag&drop
├── Form1.Designer.cs           # Declaración de campos de controles
├── Form1.resx                  # Recursos del formulario principal
│
├── Theme.cs                    # Sistema de diseño "Arctic Frost" — colores, fuentes, factory methods
├── CsvIndexer.cs               # Generador de índice CSV asíncrono + clasificador de archivos
│
├── FileViewerform.cs           # Visor/analizador de CSV, JSON, XML, TXT
├── FileViewerform.resx
│
├── ImagevIewerform.cs          # Visor + editor de imágenes con GPS
│                               # → TextToolDialog (diálogo de texto inline)
│
├── MusicPlayerForm.cs          # Reproductor de audio con letras y carátulas
├── MusicPlayerForm.resx
│
├── VideoPlayerForm.cs          # Reproductor de video con WMP COM + metadatos
│                               # → WmpControl (wrapper AxHost)
│
├── NotepadForm.cs              # Bloc de notas con numeración, búsqueda y zoom
├── NotepadForm.resx
│
├── GpsReader.cs                # Extractor de GPS de imágenes (EXIF) y videos (átomos MP4)
├── CoverSearcher.cs            # Buscador multi-fuente de carátulas (iTunes, Last.fm, Spotify)
├── TagEditDialog.cs            # Diálogo de edición de tags ID3
│
└── *.resx                      # Archivos de recursos adicionales

FileExplorerr.slnx              # Solución (.NET SDK solution format)
.gitignore                      # Visual Studio + .NET gitignore
.gitattributes                  # Normalización de finales de línea
```

---

## Dependencias

| Paquete | Versión | Uso |
|---|---|---|
| [NAudio](https://github.com/naudio/NAudio) | 2.2.1 | Reproducción de audio (MP3, WAV, FLAC, AAC, etc.) |
| [taglib-sharp-netstandard2.0](https://github.com/mono/taglib-sharp) | 2.1.0 | Lectura y escritura de tags ID3, Vorbis, APE, etc. |
| WMPLib (COM Reference) | 1.0 | Interfaz con Windows Media Player para video |

**APIs externas (opcionales, requieren internet):**
- iTunes Search API — carátulas
- lrclib.net — letras de canciones
- Last.fm API — carátulas alternativas
- OpenStreetMap / Leaflet — mapas GPS embebidos
- Spotify API — carátulas alternativas

---

## Tecnologías

- **C# 12 / .NET 8** con `nullable enable` e `implicit usings`
- **Windows Forms** con `OwnerDraw` personalizado en `ListView`, `TreeView` y `DataGridView`
- **P/Invoke** — `SHFileOperation` (papelera), `DwmSetWindowAttribute` (título oscuro), `ExtractIcon` (icono de papelera del sistema)
- **COM Interop** — Windows Media Player (`WMPLib`) embebido en `AxHost`
- **async/await** — carga de directorios, exportación CSV, búsqueda de carátulas y letras sin bloquear la UI
- **System.Text.Json** — parsing de respuestas de APIs y archivos JSON
- **System.Drawing** — edición de imágenes, lectura de EXIF, iconos personalizados
- **TagLib** — metadatos de audio (ID3v2, Vorbis, APE)
- **NAudio** — decodificación y reproducción de audio PCM
- **Leaflet.js** + **OpenStreetMap** — mapas GPS en `WebBrowser` embebido