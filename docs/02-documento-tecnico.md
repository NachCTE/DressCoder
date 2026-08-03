# Documento Técnico — FF7 Rebirth Replacer → Dresscode Converter

Versión: 0.2 (Etapa 2 — pre-implementación, stack y tooling confirmados)
Basado en: `docs/01-investigacion-dresscode.md`

---

## 0. Stack tecnológico y herramientas (decisión confirmada)

- **Lenguaje/Framework de la app**: C# / .NET 8, WPF, MVVM, portable (sin instalador). Confirmado con el usuario pese a no tener experiencia previa en C/C++, priorizando el ecosistema disponible para este dominio (ver justificación abajo) y la cercanía sintáctica con TypeScript/Node (tipado fuerte, async/await, OOP, LINQ).
- **Lectura/escritura de assets individuales de Unreal Engine (`.uasset`/`.uexp`)**: **UAssetAPI** (C#, MIT) — permite parsear y editar propiedades de `DataAsset`s (strings, enums, soft object paths) y renombrar/reubicar internamente paquetes (necesario para relocar el Skeletal Mesh del replacer fuera de la ruta original del juego).
- **Empaquetado final del contenedor `.pak`/`.utoc`/`.ucas` (IoStore)**: no se implementa un empaquetador propio desde cero. Se usan como **herramientas externas (binarios Rust precompilados), invocadas como subproceso desde C#**:
  - **`repak`** (https://github.com/trumank/repak): lectura/escritura de `.pak` clásico.
  - **`retoc`** (https://github.com/trumank/retoc): lectura/escritura/conversión de contenedores IoStore `.utoc`/`.ucas`, incluye `pack-raw` (empaqueta un directorio de chunks crudos en un contenedor IoStore) — esta es la pieza que resuelve el mayor riesgo identificado en la Etapa 1 (falta de librería .NET nativa para escribir IoStore).
  - Justificación de no usar C++: ambas herramientas ya están compiladas y mantenidas activamente por la comunidad de modding de Unreal Engine 5 (usadas en juegos como Abiotic Factor, Palworld, Stalker 2); no hace falta escribir ni compilar Rust/C++ propio, solo consumir el binario vía `Process.Start` y parsear su output/exit code.
- **Lectura de `.pak` del replacer original (input)**: se evalúa usar **CUE4Parse** (C#) o directamente `repak`/`retoc` en modo lectura para mantener una única dependencia externa de bajo nivel. A confirmar en el spike técnico de la Etapa 3.

---

## 1. Resumen ejecutivo

Se busca construir una aplicación de escritorio (.NET 8, C#, WPF, MVVM, portable) que automatice la mayor parte posible del proceso de convertir un mod **replacer** (.pak clásico que sobreescribe assets del juego en su ruta original) en un mod **Dresscode-compatible** (plugin de Unreal Engine con `DataAsset`s de metadatos, cargado dinámicamente por Reunion Mod Loader).

**Hallazgo central de la investigación:** el empaquetado final de un mod Dresscode es una operación nativa del editor de Unreal Engine (Alpakit → "cook as DLC plugin"), sin API headless pública. Sin embargo, gracias a herramientas de terceros (`repak`/`retoc`) que ya saben leer y escribir el formato de contenedor final (`.pak`/`.utoc`/`.ucas`), es posible reconstruir ese artefacto de forma **standalone, sin instalar Unreal Engine**, aceptando el riesgo de que no sea garantizado bit-idéntico al de Alpakit. La aplicación, por lo tanto, se diseña con una **arquitectura híbrida de dos niveles de salida**, priorizando el modo standalone (Modo B), dejando claro qué se automatiza al 100% y qué requiere una intervención mínima y acotada del usuario.

---

## 2. Qué información puede obtenerse automáticamente desde un replacer

Un mod replacer típico consiste en uno o más `.pak` (posiblemente `_P.pak` + `.utoc`/`.ucas` si es IoStore) cuyos assets **sobreescriben rutas originales del juego**, del estilo:

```
End/Content/Chara/1st/Player/PC0006_Aerith/Model/Costume1/SK_PC0006_Costume1.uasset
```

De esa ruta y de los propios assets cookeados podemos derivar automáticamente (parseando la tabla de rutas del pak + convenciones de nombres conocidas del juego):

- **Personaje**: por el código de personaje en la ruta (`PC0006` = Aerith, etc. — requiere un diccionario de mapeo personaje↔código, construible una vez a partir de assets base del juego o de la comunidad).
- **Outfit reemplazado**: por el nombre de carpeta/costume index (`Costume1`, `Costume2`, ...) en la ruta original sobreescrita.
- **Tipo de asset por archivo**: Skeletal Mesh (`SK_`), Material Instance (`MI_`), Textura (`T_`), Blueprint (`BP_`), animación, etc., por prefijo de nombre y por la clase interna serializada del `.uasset` (leíble sin ejecutar UE, vía parsing de headers de UAsset con UAssetAPI/CUE4Parse).
- **Árbol de dependencias directo** de cada asset (qué otros assets referencia por soft/hard object path) — esto sí es 100% legible de forma estática desde los archivos cookeados.
- **Arma vs. personaje**: por la rama del árbol de rutas (`Weapon/` vs `Chara/1st/Player/`).

Esto cubre: **personaje, outfit, skeletal mesh, materiales, texturas, y grafo de dependencias/asset paths.** Blueprints personalizados (lógica de gameplay) en un replacer clásico son raros — normalmente los replacers solo tocan datos de arte, no lógica; si existieran, se detectan pero se marcan como "no soportado automáticamente" (ver limitaciones).

## 3. Qué información NO puede obtenerse automáticamente y debe pedirse al usuario

- **Friendly Name / nombre visible del outfit** en el menú de Dresscode (dato de presentación, no inferible de forma confiable del nombre técnico del archivo).
- **Descripción y versión del mod**, autor, créditos (metadatos editoriales).
- **Imagen de preview** (`Texture2D`/`Sprite`) — un replacer no trae necesariamente un ícono de menú; si no se detecta ninguna textura candidata clara, se debe pedir al usuario que aporte una imagen o elegir una textura existente del mod como preview.
- **`PlayerType` exacto de destino**, cuando el mapeo de ruta→personaje es ambiguo (variantes regionales de rutas, DLC, mods ya parcialmente convertidos, nomenclaturas no estándar) o cuando el replacer reemplaza a un personaje que Dresscode agrupa distinto al del juego base.
- **Agrupamiento deseado** (¿es una variante de color de un outfit ya existente, o un outfit nuevo?) — el `GroupKey` es una decisión de intención del autor, no derivable del binario.
- **Confirmación de candidato** cuando el detector encuentra múltiples asset roots plausibles (por ejemplo, un pak que toca 2 personajes a la vez, o rutas atípicas).
- **Resolución de conflictos de nombre** con mods ya presentes en `End/Mods/` (elegido por el usuario cuando el detector encuentra colisión).

## 4. Estructura interna de Dresscode (resumen operativo para la app)

**Vista de proyecto/editor** (como se edita en Unreal Engine, antes de cookear):

```
{PluginContentDir}/
├── MetaData/DA_ModMetaData.uasset      (1 por plugin, ruta fija)
└── <Any>/DA_ModData_Character.uasset    (1 por plugin, entradas múltiples adentro)
    └── referencia → SK_*.uasset (custom, ubicación libre) → Skeleton (ruta ORIGINAL del juego, obligatorio)
```

**Estructura del artefacto final distribuido** (confirmada por el usuario, es el target real de exportación de la app):

```
{GameDir}/End/Mods/{PluginName}/
├── {PluginName}.uplugin
├── Resources/
│   └── Icon.png
└── Content/
    └── Paks/
        └── WindowsNoEditor/
            ├── {PluginName}-WindowsNoEditor.pak
            ├── {PluginName}-WindowsNoEditor.utoc
            └── {PluginName}-WindowsNoEditor.ucas
```

Todo lo de la "vista de editor" (DataAssets + Skeletal Mesh + materiales + texturas) queda cookeado **dentro** de la tripleta `.pak/.utoc/.ucas`. El artefacto distribuido en sí es minimalista (uplugin + icono + 3 archivos binarios); toda la complejidad de Dresscode vive empaquetada dentro del contenedor IoStore, que es exactamente lo que el **Converter (Modo B)** debe aprender a producir.

Reglas duras que la app debe validar siempre:
1. Un solo `DA_ModMetaData` por plugin, en `MetaData/`.
2. Un solo `DataAsset` de tipo Character y uno de tipo Weapon por plugin (múltiples outfits = múltiples entradas dentro del mismo asset, no assets duplicados).
3. El Skeleton referenciado por cualquier Skeletal Mesh custom debe seguir apuntando a la ruta original del juego.
4. Los nombres de Material Slots del mesh custom deben coincidir con los nombres usados en el `EndMaterialPack` si se usan variantes de material.
5. El artefacto final debe respetar exactamente el layout `{PluginName}.uplugin` + `Resources/Icon.png` + `Content/Paks/WindowsNoEditor/{PluginName}-WindowsNoEditor.{pak,utoc,ucas}` (nombres de archivo con el sufijo `-WindowsNoEditor` consistente con el `PluginName`).

---

## 5. Limitaciones y casos que NO pueden automatizarse

| Caso | Motivo | Mitigación |
|---|---|---|
| Cook/empaquetado final "oficial" (pak/IoStore + AssetRegistry del plugin) | Es una operación del editor de UE (Alpakit), sin API headless documentada públicamente | Ver arquitectura híbrida (sección 6): generación de proyecto UE + script de automatización, o repack binario best-effort marcado como experimental |
| Creación de nuevos assets (modelos 3D, texturas, retopología) | Fuera de alcance por diseño (pedido explícito del usuario) | No aplica — la app solo reubica/adapta assets ya existentes en el replacer |
| Blueprints de gameplay personalizados dentro de un replacer | Requiere recompilación/relink de Blueprint dentro de UE | Se detectan y reportan como "requiere revisión manual en el editor" |
| Mods replacer que ya vienen con lógica que reemplaza rutas de sistemas no soportados por Dresscode (ej. animaciones custom, VFX con hard refs rotas) | Dependencias no resolubles estáticamente en todos los casos | Validador reporta advertencia y deja el asset fuera del paquete final, marcado en el log |
| Garantía de que el resultado sea *bit-idéntico* a lo que produciría Alpakit | El formato de cook interno no está documentado públicamente y puede variar entre versiones del motor custom | Se documenta como limitación conocida; se ofrece el modo "proyecto UE generado" como vía 100% fiel |

## 6. Arquitectura propuesta (híbrida, dos modos de salida)

Dado que el paso de cook es la única pieza no automatizable de forma headless y confiable, la aplicación soporta **dos modos de exportación**, seleccionables por el usuario según lo que tenga disponible:

### Modo A — "Proyecto UE asistido" (recomendado, 100% fiel al pipeline oficial)
La app automatiza *todo* excepto la pulsación final de "Alpakit!" en el editor:
1. Analiza el replacer y arma el modelo de conversión (personaje, outfit, assets, dependencias).
2. El usuario completa los campos no derivables (nombre, descripción, preview, PlayerType si es ambiguo) en la UI.
3. La app genera, dentro de un directorio de plugin de Unreal Engine ya existente del usuario (`{FF7R2UProj}/Mods/{PluginName}/`):
   - El `.uplugin`.
   - Copia los assets cookeados del replacer reubicándolos en rutas libres dentro de `Content/`.
   - Genera/parchea, vía **UAssetAPI** (sin abrir el editor), los `.uasset` de `DA_ModMetaData` y `DA_ModData_Character/_Weapon` a partir de plantillas binarias vacías (una plantilla de referencia por tipo, construida una única vez con el editor real y embebida en la app), rellenando strings, enums y soft-object-paths con los datos capturados en el paso 2.
4. La app deja todo listo para que el usuario abra su editor (que de todos modos necesita tener instalado, según lo exige el propio flujo oficial de Dresscode) y presione un solo botón: "Alpakit!". Este es el único paso manual irreducible.
5. Opcional: si el usuario indica la ruta a `RunUAT`/`Editor-Cmd` de su instalación, la app puede intentar invocar el cook vía línea de comandos (`-run=Cook`/`BuildCookRun` apuntado al plugin) como conveniencia — sin garantía, ya que no está documentado oficialmente; se ofrece como acción "experimental" con opción de fallback manual.

### Modo B — "Repack binario directo" (experimental, sin motor instalado) — **modo priorizado para el MVP**
Para usuarios sin el motor/proyecto de UE instalados, se ofrece un modo best-effort:
1. Igual análisis y captura de datos que el modo A.
2. En lugar de generar un proyecto de editor, la app intenta **reempaquetar directamente** los bytes cookeados extraídos del replacer + los `.uasset` de DataAsset parcheados, produciendo la tripleta final confirmada `{PluginName}-WindowsNoEditor.pak/.utoc/.ucas` (contenedor IoStore), junto con el `.uplugin` y `Resources/Icon.png`, replicando el layout exacto de un mod Dresscode instalado (sección 4).
3. Este modo se marca explícitamente como **no soportado oficialmente**: puede fallar en tiempo de ejecución (crash, mod no cargado, T-pose) si el AssetRegistry embebido en el `.utoc`/`.ucas` no es coherente con lo que espera el motor custom. Se ofrece igualmente porque maximiza automatización para el caso común (reemplazo simple de un mesh + materiales + texturas sin blueprints custom), que es la mayoría de los replacers reales.
4. Requiere validación exhaustiva post-generación (ver validador) y pruebas manuales del usuario en el juego.

> **Decisión de producto (confirmada con el usuario):** se prioriza el **Modo B (repack binario directo, sin motor de Unreal Engine instalado)** como camino principal del MVP, ya que el objetivo del proyecto es una herramienta portable, standalone, sin dependencias pesadas de UE. El Modo A queda documentado como alternativa de mayor fidelidad para una fase posterior (o para usuarios avanzados que sí tengan el motor), pero **no bloquea el desarrollo inicial**.
>
> Esta decisión implica aceptar explícitamente los riesgos de la sección 8 (formato de contenedor no documentado públicamente, posible incompatibilidad con actualizaciones del motor/juego, necesidad de validación manual en juego por parte del usuario). El Validator y las advertencias en UI deben comunicar siempre que un mod generado en Modo B es "experimental / no oficial" hasta que el usuario lo pruebe en el juego.

### 6.1 Módulos (según lo solicitado)

```
Core/
 ├── Parser/           → Lectura de .pak / .utoc-.ucas (vía wrapper sobre librería de lectura tipo CUE4Parse), lectura de .uasset (UAssetAPI)
 ├── Analyzer/         → Heurísticas de detección: personaje, outfit, tipo de asset, dependencias, ambigüedades
 ├── Converter/        → Generación/parcheo de DataAssets (UAssetAPI), armado de estructura de plugin, (Modo B) empaquetado de contenedor
 ├── Validator/        → Reglas duras de Dresscode (sección 4), chequeo de referencias rotas, conflictos de nombre

Infrastructure/
 ├── PakReader/        → Adaptador sobre librería de lectura de paks
 ├── UnrealAssetReader/→ Adaptador sobre UAssetAPI (lectura y escritura)
 ├── FileSystem/       → Operaciones de IO, staging, export

Application/
 ├── Services/         → Orquestación de flujo (Import → Analyze → Configure → Export)
 ├── DTOs/             → Modelos de transferencia entre capas
 ├── Commands/         → Comandos MVVM (Import, Analyze, Export, etc.)

UI/                    → WPF, MVVM (vistas: Home, Importar, Análisis, Configuración, Vista previa, Exportación, Log)
Models/                → Entidades de dominio (ModProject, DetectedCharacter, DetectedOutfit, AssetNode, etc.)
Configuration/         → Rutas de motor/proyecto UE del usuario, plantillas embebidas, perfiles de conversión
```

Principios: SOLID, DI (Microsoft.Extensions.DependencyInjection), logging (Serilog o Microsoft.Extensions.Logging), manejo de errores centralizado con resultados tipados (`Result<T>`) en vez de excepciones para flujo de negocio esperado (ambigüedades, validaciones), reservando excepciones para errores realmente excepcionales (archivo corrupto, IO).

Extensibilidad: cada "Converter" se registra vía interfaz (`IDresscodeConverter`) para poder agregar soporte a futuras versiones de Dresscode/motor sin tocar el núcleo (Strategy pattern + DI).

## 7. Flujo completo de conversión

1. **Importar**: usuario selecciona `.pak`(s) o carpeta de mod.
2. **Parseo**: se listan assets, se identifican rutas originales sobreescritas.
3. **Análisis**: heurísticas detectan personaje/outfit/tipo de arma/dependencias; se generan candidatos con score de confianza.
4. **Revisión/Configuración**: UI muestra árbol de archivos + detecciones; si hay ambigüedad, el usuario elige entre candidatos o corrige manualmente; completa metadatos no derivables.
5. **Validación previa**: chequeo de reglas duras + referencias rotas + conflictos.
6. **Generación**: según el modo (A o B), se arma la estructura de plugin y se parchean los DataAssets.
7. **Empaquetado**: Modo A → se deja listo para Alpakit (o se invoca headless si el usuario lo configuró); Modo B → se genera el contenedor directamente.
8. **Validación final** del artefacto de salida (estructura de carpetas, presencia de `.uplugin`, presencia de `DA_ModMetaData`, sin referencias rotas conocidas).
9. **Exportación**: copiar a `End/Mods/` o generar ZIP listo para Nexus.

## 8. Riesgos

- **Riesgo de formato**: `retoc` soporta overrides de versión de TOC/container header para juegos donde el IoStore no está bien versionado (frecuente en engines pre-5.0/forks custom como el de FF7R); puede requerir ajuste fino (`--override-container-header-version`, `--override-toc-version`) descubierto empíricamente en el spike técnico. Aun con esta herramienta, el Modo B puede romperse con actualizaciones del motor custom o del juego, ya que dependemos de un proyecto de terceros (no oficial de Epic/narknon) para el formato exacto.
- **Riesgo de dependencia de terceros**: `repak`/`retoc` son binarios externos mantenidos por la comunidad (no por Epic ni por los autores de Dresscode); un cambio de licencia, discontinuación del proyecto, o incompatibilidad con una versión futura del motor custom de FF7R nos dejaría sin vía de empaquetado hasta portar o forkear la herramienta. Mitigación: vendorizar (embeber) la versión específica del binario que valida el spike, no depender de "latest".
- **Riesgo legal/de licencia**: distribuir plantillas binarias de `DA_ModMetaData`/`DA_ModData_Character` embebidas requiere que sean creadas por el propio proyecto (no extraídas de un mod de terceros con permisos restrictivos) — los mods de ejemplo vistos en Nexus tienen permisos de reutilización de assets restringidos. `repak` (MIT/Apache-2.0) y `retoc` (MIT) sí son redistribuibles y se embeben en la app. **Excepción confirmada**: `oo2core_*.dll` (Oodle, de RAD Game Tools) es propietaria y **no se redistribuye** — la app la localiza en tiempo de ejecución desde la instalación del juego del usuario. Ver `docs/04-licencias-terceros.md` para el detalle completo y la decisión de arquitectura derivada.
- **Riesgo de deriva de versión**: Dresscode/FF7RML evolucionan (v1.1 agregó Actor/GroupKey); el diseño por Strategy/Converter versionado mitiga esto pero requiere mantenimiento continuo.
- **Riesgo de falsos positivos en detección** de personaje/outfit por convenciones de nombre no estándar en mods "salvajes" de la comunidad.
- **Riesgo de que el usuario no tenga el motor instalado** (Modo A requiere ~cientos de GB y una compilación de motor pesada) — mitigado ofreciendo Modo B como alternativa priorizada para el MVP.

## 9. Mejoras futuras posibles

- Integración con un cook headless si en el futuro Epic/el proyecto narknon documentan/exponen un commandlet oficial (Modo A automatizado end-to-end).
- Aportar/contribuir mejoras a `retoc`/`repak` upstream si se detectan gaps específicos para el motor custom de FF7R, en vez de forkear en silencio.
- Base de datos comunitaria de mapeos personaje↔código de asset, actualizable sin recompilar la app.
- Sistema de plugins para nuevos "converters" cuando aparezcan nuevos sistemas además de Dresscode (la guía menciona que FF7RML es genérico y otros mods, como "Scenery", ya usan el mismo framework).
- Comparador visual replacer-original vs. resultado generado.
- Detector de conflictos entre mods ya presentes en `End/Mods/`.

## 10. Conclusión / Go decision

**Decisión confirmada:** se avanza a la Etapa 3 (arquitectura detallada) priorizando el **Modo B** (repack binario directo, standalone, sin requerir Unreal Engine instalado) como camino principal del MVP. El Modo A queda documentado como ruta de mayor fidelidad para una fase futura opcional.

Implicaciones directas para la Etapa 3 (arquitectura):
- El módulo `Converter`/empaquetado (Modo B) pasa a ser **crítico de ruta** y debe diseñarse con la máxima cobertura de tests/validación posible, dado que es el componente de mayor riesgo técnico (formato de contenedor no documentado oficialmente).
- Se necesita definir de forma concreta: (a) si el contenedor final será `.pak` legado o `.utoc/.ucas` (IoStore) — a determinar inspeccionando binarios reales de mods Dresscode existentes en la Etapa 3/4; (b) estrategia de generación del `AssetRegistry` embebido del plugin, que el juego necesita para descubrir el mod.
- Plantillas embebidas de `DA_ModMetaData` y `DA_ModData_Character/Weapon` deben crearse desde cero (sin copiar de mods de terceros con permisos restrictivos) — posiblemente generadas por el propio usuario una vez con el editor real, o reconstruidas byte a byte a partir de la estructura documentada por UAssetAPI si se logra inferir el layout sin el editor.

Antes de escribir código de producción, se requiere adicionalmente:
- Confirmar disponibilidad y licencia de **UAssetAPI** para uso en este proyecto (MIT — compatible).
- Evaluar en un spike técnico acotado la lectura de un `.pak`/`.utoc`-`.ucas` real de un mod replacer y de un mod Dresscode de ejemplo (para inspeccionar el contenedor de salida real), a fin de confirmar viabilidad práctica del `Parser` y del `Converter` (Modo B) antes de comprometer la arquitectura final.
