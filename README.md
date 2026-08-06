# DressCoder

Frontend bilingüe (español/inglés) para convertir skins de **Final Fantasy VII Rebirth** al formato Dresscode.

DressCoder automatiza el flujo de trabajo del patcher oficial:

- Detecta y corrige skins que necesitan el formato V1.005.
- Crea variantes omitiendo partes de la malla.
- Genera `dresscode.json` con nombre, autor, descripción y miniatura.
- Convierte la skin al formato Dresscode.
- Permite abrir directamente la carpeta final para copiarla a `Mods`.

> DressCoder no incluye mods ni archivos del juego. Debes trabajar con skins que ya tengas y obtenerlas de sus autores correspondientes.

## Requisitos

- Windows 10/11.
- Python 3.9 o superior.
- Python disponible como comando `python`.
- NumPy, instalado mediante `requirements.txt`.

## Instalación

Instala Python 3.9 o superior y verifica que esté disponible como `python`:

```powershell
python --version
```

Desde la carpeta del proyecto:

```powershell
python -m pip install -r requirements.txt
```

## Uso

Ejecuta la interfaz con:

```powershell
python frontend.py
```

El patcher oficial no se distribuye dentro de este repositorio. Cuando sea necesario, DressCoder lo descarga automáticamente desde su release oficial de GitHub, verifica su SHA-256 y lo instala localmente en `tools\patcher`.

### Flujo de conversión

1. Selecciona la carpeta de la skin de origen.
2. Selecciona una carpeta raíz de destino.
3. Completa el nombre de la skin.
4. Opcionalmente agrega autor, descripción y una imagen PNG real.
5. Inicia la conversión.
6. Decide si quieres crear variantes.
7. Al terminar, DressCoder ofrece abrir la carpeta:

```text
<destino>\<nombre de skin>\dresscode
```

La aplicación guarda los últimos datos seleccionados en:

```text
%LOCALAPPDATA%\DressCoder\settings.json
```

## Imágenes

El conversor oficial requiere imágenes PNG reales para thumbnails y previews.

- Usa archivos con extensión `.png`.
- El contenido debe ser un PNG válido, no solamente un archivo renombrado.
- Las imágenes JPG, JPEG, WEBP u otros formatos no son compatibles con el conversor actual.

## Variantes

Solo se pueden modificar las partes del modelo principal. Los modelos `_Condition` y otros modelos secundarios deben permanecer idénticos entre la skin base y sus variantes para evitar conflictos durante la conversión.

Las variantes se guardan dentro de:

```text
<nombre de skin>\Variants\<nombre de variante>
```

## Instalar el mod en el juego

Después de una conversión exitosa, copia **el contenido** de:

```text
<destino>\<nombre de skin>\dresscode
```

a la carpeta `Mods` de Dresscode del juego.

Normalmente se encuentra en una ruta similar a:

```text
<Final Fantasy VII Rebirth>\End\Mods
```

Copia los archivos generados por Dresscode, como `.pak`, `.ucas` y `.utoc`. No copies `dresscode.json` ni la carpeta temporal de trabajo.

## Herramientas de línea de comandos

Las herramientas del patcher se descargan durante la ejecución. Después de instalarlo desde DressCoder, estarán disponibles localmente en `tools\patcher`:

```powershell
python tools\patcher\devtools\parts.py "C:\ruta\de\la\skin" --list
python tools\patcher\devtools\parts.py "C:\ruta\de\la\skin" --omit 1,4 --out "C:\ruta\de\la\variante"
python tools\patcher\convert.py "C:\ruta\de\la\skin" --yes
```

Para conocer todas las opciones, consulta el `README.md` que acompaña la instalación local del patcher o su repositorio oficial.

## Créditos y componentes de terceros

DressCoder utiliza el **FFVII Rebirth Mesh Patcher**, creado por **nikolaybutnik**. DressCoder no redistribuye su código: lo descarga desde GitHub en el momento de ejecución:

- Repositorio: <https://github.com/nikolaybutnik/FFVII-Rebirth-Mesh-Patcher>
- Release utilizada: `v1.5.0`
- Licencia del patcher descargado: MIT
- Copyright del patcher: `© 2026 nikolaybutnik`

El release descargado incluye su propio aviso de copyright y su licencia MIT. El patcher se instala solo localmente para ejecutar la conversión y no se incluye en los archivos distribuidos por este repositorio.

El patcher es un proyecto independiente. DressCoder no es oficial, no está afiliado a Square Enix, al equipo de Dresscode ni a los autores de las skins procesadas.

## Licencia de DressCoder

El código propio de DressCoder se distribuye bajo la licencia MIT. Consulta [`LICENSE`](LICENSE).

La licencia del patcher descargado es independiente de la licencia de DressCoder y debe respetarse por separado.
