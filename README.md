# FileExplorerr

Explorador de archivos de escritorio construido con **Windows Forms** y **.NET 8**, con tema oscuro en paleta negro/azul, soporte completo de drag & drop, panel lateral interactivo y exportación de índice CSV.

---


## Características

### Navegación
- Barra de dirección editable — escribe una ruta y presiona `Enter`
- Botón **◄ Atrás** con historial de navegación
- Botón **▲ Subir** para ir al directorio padre
- Doble clic en carpetas para entrar, doble clic en archivos para abrirlos

### Gestión de archivos
- **📁 Nueva carpeta** — con validación de nombre
- **Renombrar** — desde el menú contextual (clic derecho)
- **Eliminar** — envía a la Papelera de Reciclaje de Windows (recuperable)

### Refresh
- Botón **⟳** en la barra superior para recargar el directorio actual
- Atajo de teclado `F5`
- Disponible también desde el menú contextual

### Drag & Drop
- Arrastra archivos o carpetas **sobre otra carpeta** para moverlos — la carpeta destino se resalta en azul
- Arrastra al **panel de papelera** (esquina inferior derecha) para eliminar — el icono cambia al hacer hover
- Manejo de conflictos de nombres (sobreescribir / saltar / cancelar)
- Protección contra mover una carpeta dentro de sí misma

### Estadísticas en barra de estado
Al entrar a cualquier carpeta la barra inferior muestra el desglose completo:
```
📁 4 carpetas  ·  📄 30 archivos  ·  🖼️ 12  ·  🎵 3  ·  🎬 5  ·  📝 8  ·  📦 2
```
La columna **Contenido / Info** de cada carpeta también muestra un resumen: `3 sub, 12 img, 5 txt`

---

## Panel lateral derecho

El panel de la derecha tiene dos modos:

### Modo normal — vista del directorio actual
Al navegar a una carpeta el panel muestra su contenido organizado en un **TreeView expandible**:

```
📁 Carpetas  (3)
  📁 Fotos
  📁 Música
  📁 Proyectos

🖼️ Imágenes  (6)
  * logo.png
  * banner.jpg

🎵 Audio  (5)
  * cancion1.mp3
  * cancion2.flac

📝 Texto/Código  (4)
  * notas.txt
  * config.json
```

Cada carpeta tiene el botón **[+]** para expandirla y ver sus subcarpetas y archivos sin necesidad de navegar.

### Modo búsqueda
Escribe en la barra de búsqueda y presiona `Enter` o el botón **Buscar**:

- Busca en todo el directorio actual de forma recursiva
- Muestra **carpetas** y **archivos** que coincidan con el texto
- Cada carpeta encontrada es expandible para ver su contenido completo
- Al vaciar el campo y buscar de nuevo vuelve al modo normal

**Ejemplo:** buscas `"música"` y aparecen todas las carpetas con ese nombre. Expandes una y ves todos sus archivos organizados por categoría sin salir del panel.

### Colores del TreeView
| Color | Tipo |
|---|---|
| 🔵 Azul | Headers de grupo (`📁 Carpetas`, `🖼️ Imágenes`…) |
| 🔵 Azul claro | Categorías de archivos |
| 🟡 Amarillo | Carpetas individuales |
| ⚪ Blanco | Archivos |
| 🔘 Gris | Mensajes de estado (vacía, sin acceso…) |

**Doble clic** en una carpeta del panel navega a ella. **Doble clic** en un archivo lo abre.

---

## Exportación de índice CSV

El botón **📊 Exportar CSV** recorre el directorio actual y todos sus subdirectorios y genera un `.csv` con una fila por carpeta:

```csv
"Ruta Completa","Nombre Carpeta","Carpetas","Archivos Total","Último Acceso"
"C:\Users\juan\Fotos","Fotos",3,120,"15/02/2026 10:30"
"C:\Users\juan\Fotos\Vacaciones","Vacaciones",0,47,"10/01/2026 18:22"
"C:\Users\juan\Música","Música",2,88,"20/02/2026 09:15"
```

- La generación es **asíncrona** — la barra de estado muestra la carpeta en proceso
- El botón se bloquea durante la generación para evitar doble clic
- Al terminar pregunta si deseas abrir el archivo

---

## Visualizadores integrados

| Tipo | Extensiones | Visor |
|---|---|---|
| Texto | `.txt` `.log` `.ini` `.config` | Solo lectura, fuente monoespaciada |
| JSON | `.json` | Indentación automática |
| XML | `.xml` | Indentación automática |
| CSV | `.csv` | Columnas alineadas |
| Imágenes | `.jpg` `.jpeg` `.png` `.gif` `.bmp` | Zoom `+` / `−` / `1:1` |
| Audio / Video | `.mp3` `.wav` `.mp4` `.avi` `.mkv` … | Abre con la app predeterminada |


---


## Estructura del proyecto

```
FileExplorerr/
├── FileExplorerr.csproj       # Proyecto WinForms .NET 8
├── Program.cs                 # Punto de entrada
├── Form1.cs                   # Ventana principal — lógica y UI
├── Form1.Designer.cs          # Declaración de controles
├── CsvIndexer.cs              # Generador de índice CSV + estadísticas
├── FileViewerform.cs          # Visor de archivos de texto
├── ImageViewerform.cs         # Visor de imágenes con zoom
└── Form1.resx                 # Recursos del formulario
FileExplorerr.slnx             # Solución
```

---

## Atajos de teclado

| Atajo | Acción |
|---|---|
| `Enter` en barra de dirección | Navegar a la ruta escrita |
| `Enter` en barra de búsqueda | Buscar en el panel lateral |
| `F5` | Actualizar directorio |
| `+` / `−` | Zoom in / out (visor de imágenes) |
| `Ctrl + 0` | Restablecer zoom 1:1 (visor de imágenes) |
| `Escape` | Cerrar visor de imágenes |

---

## Tecnologías

- **C# / .NET 8**
- **Windows Forms**
- P/Invoke para integración con la Papelera de Reciclaje de Windows (`SHFileOperation`)
- `async/await` para carga de directorios y exportación CSV sin bloquear la UI
- `TreeView` con `OwnerDrawAll` para coloreo personalizado de nodos
