# FileExplorerr

Explorador de archivos de escritorio construido con **Windows Forms** y **.NET 8**, con tema oscuro en paleta negro/azul y soporte completo de drag & drop.

---

## Características

### Navegación
- Barra de dirección editable — escribe una ruta y presiona `Enter` para navegar
- Botón **Atrás** con historial de navegación
- Botón **Subir** para ir al directorio padre
- Doble clic en carpetas para entrar, doble clic en archivos para abrirlos

### Gestión de archivos
- **Crear carpeta** — botón en la barra superior o menú contextual, con validación de nombre
- **Renombrar** — desde el menú contextual (clic derecho)
- **Eliminar** — envía a la Papelera de Reciclaje de Windows (recuperable), desde el menú contextual o arrastrando al panel inferior

### Drag & Drop
- Arrastra archivos o carpetas **sobre otra carpeta** del listado para moverlos al instante — la carpeta destino se resalta en azul al pasar encima
- Arrastra archivos o carpetas al **panel de la papelera** (esquina inferior derecha) para eliminarlos — el icono cambia a papelera llena al hacer hover
- Manejo de conflictos de nombres al mover (sobreescribir / saltar / cancelar)
- Protección contra mover una carpeta dentro de sí misma

### Visualizadores integrados
| Tipo | Extensiones | Visor |
|---|---|---|
| Texto | `.txt` `.log` `.ini` `.config` | Editor de solo lectura con fuente monoespaciada |
| JSON | `.json` | Formateado con indentación automática |
| XML | `.xml` | Formateado con indentación automática |
| CSV | `.csv` | Columnas alineadas |
| Imágenes | `.jpg` `.jpeg` `.png` `.gif` `.bmp` | Visor con zoom (`+` / `−` / `1:1`) y atajos de teclado |
| Audio / Video | `.mp3` `.wav` `.mp4` `.avi` `.mkv` … | Abre con la aplicación predeterminada del sistema |

### Listado de archivos
- Columnas: **Nombre · Tipo · Tamaño · Información · Fecha de modificación**
- Clic en cualquier columna para ordenar ascendente/descendente
- Archivos y carpetas ocultos se excluyen automáticamente
- La columna *Información* muestra el conteo de subcarpetas y archivos de cada directorio
- Iconos por categoría: carpeta, archivo genérico, imagen, audio, video, texto

---

## Requisitos

| Componente | Versión mínima |
|---|---|
| Sistema operativo | Windows 10 / 11 |
| .NET | 8.0 |
| SDK | .NET 8 SDK (para compilar) |

---

## Compilar y ejecutar

```bash
# Clonar el repositorio
git clone <url-del-repo>
cd FileExplorerr

# Compilar
dotnet build

# Ejecutar
dotnet run --project FileExplorerr/FileExplorerr.csproj
```

O abre `FileExplorerr.slnx` directamente en **Visual Studio 2022 o 2026 y presiona `F5`.

---

## Estructura del proyecto

```
FileExplorerr/
├── FileExplorerr.csproj       # Proyecto WinForms .NET 8
├── Program.cs                 # Punto de entrada
├── Form1.cs                   # Ventana principal — lógica y UI
├── Form1.Designer.cs          # Declaración de controles
├── FileViewerform.cs          # Visor de archivos de texto
├── ImageViewerform.cs         # Visor de imágenes con zoom
└── Form1.resx                 # Recursos del formulario
FileExplorerr.slnx             # Solución
```


---

## Atajos de teclado

| Atajo | Acción |
|---|---|
| `Enter` (barra de dirección) | Navegar a la ruta escrita |
| `+` / `−` (visor de imágenes) | Zoom in / out |
| `Ctrl + 0` (visor de imágenes) | Restablecer zoom 1:1 |
| `Escape` (visor de imágenes) | Cerrar el visor |

---

