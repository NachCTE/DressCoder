# Investigación: Dresscode - Costume Changer (FF7 Rebirth)

> Fuentes primarias consultadas:
> - Nexus Mods: https://www.nexusmods.com/finalfantasy7rebirth/mods/1062 (Dresscode)
> - Nexus Mods: https://www.nexusmods.com/finalfantasy7rebirth/mods/1061 (Reunion Mod Loader — FF7RML, dependencia obligatoria)
> - Guía oficial para modders (Google Doc enlazado desde la página de Dresscode):
>   "Creating Dresscode compatible Mods" por YIIS
> - Ejemplos de mods compatibles (p.ej. mod 1056 "Aerith SEED Cadet Dresscode") para entender qué distribuye un autor de mod Dresscode.
> - Repos de código/engine mencionados: `narknon/UnrealEngine-CEEnd` (motor custom) y `narknon/FF7R2UProj` (proyecto actualizado) — no accesibles públicamente para exploración de archivos vía fetch anónimo (requieren clon/autenticación), pero su rol se documenta según lo descrito en la guía oficial.

## 1. Qué es Dresscode

Dresscode es un **plugin/mod en Blueprint de Unreal Engine** que corre encima de **Reunion Mod Loader (FF7RML)**, un framework de carga de mods en runtime para FF7 Rebirth. Dresscode permite cambiar dinámicamente, en tiempo real dentro del juego, el **Skeletal Mesh** que usa el blueprint de un personaje o de un arma, sin necesidad de reiniciar el juego (salvo casos puntuales documentados como bugs conocidos).

Dresscode **no reemplaza archivos del juego**. En cambio:
- Escanea el `AssetRegistry` en busca de "mods" registrados (plugins empaquetados como DLC) que contengan un tipo específico de `DataAsset`.
- Cuando el jugador selecciona un outfit/arma desde el menú de Dresscode, simplemente **reemplaza la referencia de Skeletal Mesh** (y datos asociados) del blueprint activo del personaje/arma por el mesh indicado en ese DataAsset.

Esto es fundamentalmente distinto al modelo clásico de "replacer" (.pak que sobreescribe el asset original en su ruta original, sufijo `_P`). Con Dresscode, los assets custom se ubican en **cualquier ruta dentro del plugin del mod**, y se "anuncian" al framework a través de un `DataAsset` de metadatos.

## 2. Requisitos técnicos para crear un mod Dresscode (según la guía oficial)

Para crear un mod compatible, el autor necesita:
1. Un **motor Unreal Engine custom** (`UnrealEngine-CEEnd`, fork específico para FF7R modding).
2. Los **archivos de proyecto actualizados de Rebirth** (`FF7R2UProj`), que exponen el Content Browser del juego dentro del editor.
3. Crear un **Mod Plugin** desde la herramienta **Alpakit** (integrada en el editor de Unreal, ícono en la toolbar/File menu). Alpakit:
   - Lista plugins de mods existentes.
   - Permite crear un nuevo plugin ("Blueprint Only") con nombre propio → genera un `.uplugin`.
   - Tiene configuración para mover automáticamente el mod empaquetado a la carpeta del juego y lanzar el juego tras el build (mejora la iteración, pero es conveniencia del editor, no algo que la app pueda replicar sin el editor).
   - Al presionar "Alpakit!" en la entrada del mod, **cookea y empaqueta SOLO ese plugin** como una especie de DLC (no un `.pak` clásico con sufijo `_P`), generando el artefacto final en `Saved/ArchivedPlugins/` (zip) o moviéndolo directo a `{Game}/End/Mods/` si está configurado.
4. Dentro del plugin, el autor debe crear dos tipos de `DataAsset` obligatorios (activos de Unreal Engine, es decir, objetos serializados `.uasset` — **no archivos de texto/JSON**):
   - **`DA_ModMetaData`** (clase base `PDA_ModMetaData`), ubicado obligatoriamente en `{PluginContent}/MetaData/DA_ModMetaData`.
   - Un **`PDA_ModData_Character`** (o `PDA_ModData_Weapon`, cambiando el `ModType`), ubicado en cualquier parte del plugin, que actúa de "índice" apuntando al Skeletal Mesh custom.
5. Los assets custom (Skeletal Mesh, Material Instances, texturas, KDI, BNM, VFX) pueden colocarse **en cualquier ruta dentro del plugin**. La única restricción dura: el **Skeleton** referenciado por el Skeletal Mesh debe seguir apuntando a la ruta ORIGINAL del juego (porque los Animation Blueprints tienen referencias duras a esa ruta) — si no, el personaje aparece en T-Pose.
6. El empaquetado final se hace pura y exclusivamente **dentro del editor de Unreal Engine vía Alpakit**, que ejecuta el pipeline normal de "cook" de UE (serialización binaria final, generación del AssetRegistry del plugin, compresión, empaquetado en el contenedor pak/IoStore). **No existe un modo "headless"/línea de comandos documentado públicamente** para este paso; es una acción de editor.

## 3. Estructura de archivos que espera Dresscode / FF7RML

**Estructura del plugin dentro del proyecto de Unreal Engine (antes de cookear, tal como la ve/edita el modder en el editor):**

```
{PluginContentDir}/
├── MetaData/
│   └── DA_ModMetaData.uasset       (+ .uexp)   ← metadatos del mod (obligatorio, ruta fija)
├── <CualquierCarpeta>/
│   └── DA_<Nombre>.uasset          ← PDA_ModData_Character o _Weapon (1 por tipo por plugin)
└── <CualquierCarpeta>/
    ├── SK_CustomMesh.uasset(+uexp) ← Skeletal Mesh custom
    ├── MI_Custom.uasset            ← Material Instances
    ├── T_Custom.uasset             ← Texturas
    └── ...                          (KDI, BNM, VFX, EndMaterialPack, etc.)
```

**Estructura del artefacto final distribuido/instalado (confirmada por el usuario, verificada in-game en `End/Mods/`)** — este es el resultado real que produce Alpakit tras el "cook", y por lo tanto **el target exacto que debe producir el Modo B**:

```
{GameDir}/End/Mods/
└── {PluginName}/
    ├── {PluginName}.uplugin                     ← manifiesto del plugin (texto/JSON, generado por Alpakit)
    ├── Resources/
    │   └── Icon.png                             ← ícono del plugin (mostrado en ModMenu de FF7RML)
    └── Content/
        └── Paks/
            └── WindowsNoEditor/
                ├── {PluginName}-WindowsNoEditor.pak    ← contenedor IoStore (índice/metadata del pak)
                ├── {PluginName}-WindowsNoEditor.utoc   ← tabla de contenidos IoStore
                └── {PluginName}-WindowsNoEditor.ucas   ← datos binarios de los assets cookeados (IoStore container)
```

Confirmación clave: **todos los `.uasset`/`.uexp` sueltos (DataAssets + Skeletal Mesh + materiales + texturas) terminan cookeados y empaquetados DENTRO de la tripleta `.pak`/`.utoc`/`.ucas`**. No quedan archivos `.uasset` sueltos en el mod distribuido — el árbol `Content/MetaData/...` de la sección anterior es exclusivamente la vista del **proyecto fuente en el editor**, no del artefacto final. Esto reduce la superficie del artefacto de salida a solo 4-5 archivos, pero **incrementa la dificultad del Modo B**, ya que replicar fielmente el formato interno de esa tripleta (particularmente el `.utoc`/`.ucas` de IoStore con su AssetRegistry embebido) es la parte no documentada públicamente por los autores de Dresscode/FF7RML.

## 4. Contenido de cada archivo clave

### 4.1 `DA_ModMetaData` (clase `PDA_ModMetaData`)
Contiene los mismos campos que la ventana de propiedades del `.uplugin` en Alpakit: nombre amistoso ("Friendly Name" — usado también para **agrupar** costumes/armas del mismo personaje/outfit entre distintos mods), descripción, versión (se debe incrementar en cada actualización), autor, y un array de **Plugins/dependencias** (FF7RML se agrega solo por defecto; Dresscode no agrega dependencias propias).

### 4.2 `PDA_ModData_Character` / `PDA_ModData_Weapon`
- **General Data**: nombre del costume/arma mostrado en el menú de Dresscode, descripción, imagen de preview (`Texture2D` o `Sprite`).
- **Skeletal Mesh Data**: `PlayerType` (a qué personaje/slot base reemplaza) + referencia al `SkeletalMesh` custom que Dresscode inyectará. Puede incluir `AssetUserData` adicional (condition mesh, emission, KDB, etc.).
- **Actor** (alternativa a Skeletal Mesh directo desde v1.1): permite pasar un Blueprint (`EndPlayerCharacter` / `EndWeaponSkeletalMeshActor`) en vez de solo el mesh, útil para variantes de color que solo cambian el `EndMaterialPack` sin duplicar el mesh — reduce tamaño de archivo. No soportado para armas hasta próxima actualización según la guía.
- **Additional Data / CustomData**: permite un `GroupKey` (string) para forzar agrupamiento manual de variantes que no comparten el mismo Friendly Name.
- Solo puede existir **1 DataAsset de cada ModType (Character/Weapon) por plugin** — si un autor quiere empaquetar múltiples outfits/armas, debe agregar múltiples entradas dentro del MISMO DataAsset array, no crear varios DataAssets del mismo tipo.

### 4.3 Assets custom (Skeletal Mesh, materiales, texturas, VFX, etc.)
Formato binario nativo de Unreal Engine (`.uasset`/`.uexp`/`.ubulk`), cookeados para la versión específica del motor custom de FF7R. Idénticos en formato a los que ya trae cualquier mod "replacer" clásico, con la diferencia de que en un replacer están ubicados/nombrados para **sobreescribir la ruta original del asset del juego**, mientras que en Dresscode pueden vivir en cualquier ruta dentro del plugin.

## 5. Cómo identifica Dresscode personajes, outfits y armas

- El **`PlayerType`** dentro de `PDA_ModData_Character`/`_Weapon` es el identificador de a qué personaje/slot corresponde el mesh (enum definido por FF7RML/Dresscode, con un valor por cada personaje jugable y por cada arma base).
- El **agrupamiento visual** en el menú (qué variantes aparecen juntas) se basa en el `Friendly Name` del `DA_ModMetaData`, o en el `GroupKey` manual si se define.
- No existe "detección" de outfit por nombre de archivo o ruta: es explícito, vía los campos del DataAsset. Esto es una diferencia clave respecto a los replacers clásicos, donde el "outfit reemplazado" se infiere implícitamente por la ruta del asset que se sobreescribe (p.ej. `.../Chara/1st/Player/Aerith/.../SK_Aerith_Costume1...`).

## 6. Registro de nuevos trajes

El registro es 100% declarativo a través de los dos `DataAsset` descritos. FF7RML escanea, en cada arranque del juego, el `AssetRegistry` en busca de:
- Objetos que descienden de `PDA_ModMetaData` en la ruta fija `MetaData/DA_ModMetaData` de cada plugin activo en `End/Mods/`.
- Objetos que descienden de cualquier `PDA_ModData_*` en cualquier ruta del plugin (Dresscode específicamente consume `PDA_ModData_Character`; el resto de tipos existen para otros sistemas futuros/terceros).

No hay archivos `.json`/`.ini`/`.xml` de configuracion editables por fuera del editor: **todo el "manifiesto" del mod son objetos Unreal Engine serializados en binario**.

## 7. Herramientas y proyectos existentes relevantes

- **Reunion Mod Loader (FF7RML)**: framework base, open source (fuentes C++/BP disponibles), pero el build oficial de Nexus es el que debe usarse en juego (no se debe re-compilar y distribuir).
- **Alpakit**: herramienta de empaquetado dentro del editor (mismo nombre que la usada en modding de Satisfactory; en este contexto es la integrada en el fork custom del motor de FF7R). Es una acción de **editor**, no una utilidad de línea de comandos publicada.
- **UnrealEngine-CEEnd** (`github.com/narknon/UnrealEngine-CEEnd`, rama `7Reb`) + **FF7R2UProj** (`github.com/narknon/FF7R2UProj`): motor y archivos de proyecto necesarios para poder abrir el editor y usar Alpakit. Son un prerrequisito pesado (varios GB, compilación de motor completa) que la aplicación **no puede evitar** si se quiere producir el artefacto final "oficial" vía cook real.
- No se encontró ningún conversor "replacer → Dresscode" open source existente ni una utilidad de empaquetado headless/community para generar estos plugins sin el editor. Tampoco hay documentación pública sobre el formato exacto del contenedor IoStore que usa el "cook as plugin/DLC" de este motor custom — es un detalle interno del pipeline de cocina de Unreal Engine, no versionado ni documentado por los autores de Dresscode.
- Librerías de terceros relevantes para la parte que SÍ podemos automatizar en C#:
  - **UAssetAPI** (MIT, C#/.NET): permite leer y **escribir/editar** archivos `.uasset`/`.uexp` de Unreal Engine sin el editor, incluyendo edición de propiedades de `DataAsset` (strings, soft object paths, enums, arrays). Es la pieza clave para poder generar/parchear `DA_ModMetaData` y `DA_ModData_Character` mediante binary patching de una plantilla, sin abrir Unreal Engine.
  - **CUE4Parse** (C#, solo lectura): permite leer contenedores `.pak`/`.utoc`/`.ucas` (incluye variantes IoStore) y extraer/enumerar assets — apto para el módulo lector de "replacers" existentes.
  - No se identificó una librería .NET madura para **escribir** contenedores IoStore (`.utoc`/`.ucas`) desde cero; existen utilidades en Rust/C++ en la escena de modding de otros juegos UE (no evaluadas en profundidad aquí por estar fuera del stack pedido), lo cual es una limitación importante documentada en la sección de riesgos.

## 8. Conclusión de la investigación

El punto crítico de toda la investigación es este: **el paso final de empaquetado ("cook as DLC plugin") de un mod Dresscode es una operación de Unreal Engine, ejecutada desde el editor gráfico (Alpakit), y no tiene una vía headless/documentada públicamente.** Esto determina fuertemente la arquitectura de la aplicación (ver documento técnico, sección de limitaciones y arquitectura propuesta).
