# Spike Técnico — Conclusiones

Fecha: 2026-08-03
Herramientas probadas: repak v0.2.3, retoc v0.1.5
Muestras: `AerithNierEC` (Dresscode), `ZAerithBahamutRobeStandard_P` (replacer)

---

## 1. Estructura real confirmada

### Mod Dresscode (target de salida)

```
AerithNierEC/
├── AerithNierEC.uplugin                            ← JSON simple, sin Modules
├── Resources/Icon128.png
└── Content/Paks/WindowsNoEditor/
    ├── AerithNierECEnd-WindowsNoEditor.pak         ← 3.5 KB (índice IoStore, no los datos)
    ├── AerithNierECEnd-WindowsNoEditor.utoc        ← 35 KB (tabla de contenidos)
    └── AerithNierECEnd-WindowsNoEditor.ucas        ← 51.7 MB (datos binarios Oodle-comprimidos)
```

**Convención de nombre del pak**: `{PluginName}End-WindowsNoEditor` — el "End" proviene del nombre del proyecto Unreal (`End`).

**mount_point del contenedor**: `../../../` (la raíz)  
**Paths internos**: `../../../End/Mods/{PluginName}/Content/...`

### Mod Replacer (input)

```
ZAerithBahamutRobeStandard_P/
├── ZAerithBahamutRobeStandard_P.pak         ← 0.3 KB
├── ZAerithBahamutRobeStandard_P.utoc        ← 12.4 KB
└── ZAerithBahamutRobeStandard_P.ucas        ← 8.7 MB (Oodle)
```

**mount_point**: `../../../`  
**Paths internos**: `../../../End/Content/Character/Player/{CharPath}/...`

---

## 2. Contenido real del Dresscode mod (extraído)

```
../../../End/Mods/AerithNierEC/Content/
├── MetaData/
│   ├── DA_ModMetaData.uasset       (1.2 KB) ← metadatos del mod (FriendlyName, Category, CreatedBy...)
│   └── AerithNier.uasset           (5 KB)   ← ModData (PDA_ModData_Character): lista de outfits
├── AerithNierEC.uasset                       ← Material Instance (variante principal)
├── AerithNierECNoMask.uasset                 ← Material Instance (variante sin máscara)
├── AerithNierECNoMaskWhiteOutfit.uasset      ← Material Instance
├── AerithNierECWhite.uasset                  ← Material Instance
├── White.uasset                              ← Material Instance
├── Skin/
│   ├── PC0003_00_Head_C.uasset + .ubulk     ← Texturas de skin (existían en juego, reuseadas)
│   ├── PC0003_00_Head_Mb/Mg/N/O.uasset      ← etc.
│   ├── PC0003_00_Skin_Br/C/Mb/Mg/N/o.uasset ← etc.
│   ├── PC0003_07_Head_O.uasset              ← Skin alternativa
│   ├── Head.uasset                           ← Skeletal Mesh (custom)
│   └── Skin.uasset                           ← Skeletal Mesh (custom)
└── Textures/
    ├── 2BICULOOKING.uasset                   ← Textura custom
    ├── 2P.uasset + .ubulk                    ← Textura custom (grande)
    ├── 2POutfit.uasset
    ├── BNormalpng.uasset + .ubulk
    ├── BodyAlpha/AOX/M/Rough.uasset + .ubulk
    ├── Gemini_Generated_Image_*.uasset + .ubulk
    ├── tex_ch003_024_body_AAAT.uasset + .ubulk
    └── White.uasset
```

**Observación**: este mod no tiene un Skeletal Mesh `SK_` clásico en la raíz. Tiene dos assets `Head.uasset` y `Skin.uasset` en `Skin/`, que son los meshes custom. El `ModData` (`AerithNier.uasset`) referencia estas rutas internas del plugin.

---

## 3. Contenido del replacer (extraído)

```
../../../End/Content/Character/Player/PC0003_00_Aerith_Standard/
├── Model/PC0003_00.uasset              ← Skeletal Mesh que reemplaza al de Aerith Standard
└── BahamutRobe/
    ├── A.uasset + .ubulk               ← Texture Alpha
    ├── C.uasset + .ubulk               ← Texture Color/Albedo
    ├── Mb.uasset + .ubulk              ← Texture MetaB
    ├── Mg.uasset + .ubulk              ← Texture MetaG
    ├── Mr.uasset + .ubulk              ← Texture MetaR
    ├── N.uasset + .ubulk               ← Texture Normal
    ├── StrujA.uasset                   ← Material Instance (StrucJ A)
    └── StrujB.uasset                   ← Material Instance (StrucJ B)
```

**Personaje detectado automáticamente**: `PC0003` → Aerith  
**Outfit/costume**: `00` (Standard, costume base)  
**Mesh**: `PC0003_00.uasset` en `Model/`

---

## 4. Workflow de conversión confirmado (viable sin Unreal Engine)

```
replacer.utoc/.ucas
       │
       ▼  retoc unpack-raw
  manifest.json  ←── chunk_id → "../../../End/Content/Character/.../Asset.uasset"
  chunks/        ←── archivos binarios raw (Zen-format packages)
       │
       ▼  MODIFICACIONES EN C#
  1. Reparh manifest: 
       "../../../End/Content/.../Asset" → "../../../End/Mods/{PluginName}/Content/..."
  2. Crear chunks de DataAssets:
       - DA_ModMetaData chunk  (desde template binario, parchear FriendlyName/Description/CreatedBy)
       - ModData chunk         (desde template binario, parchear PlayerType + SkeletalMesh path)
  3. Agregar entradas al manifest para los nuevos DataAssets
       │
       ▼  retoc pack-raw
  {PluginName}End-WindowsNoEditor.utoc/.ucas  (sin compresión — 3x más grande pero funcional)
       │
       ▼  Estructura final
  {PluginName}/
  ├── {PluginName}.uplugin               ← generar JSON
  ├── Resources/Icon.png                  ← copiar del usuario o usar default
  └── Content/Paks/WindowsNoEditor/
      ├── {PluginName}End-WindowsNoEditor.pak    ← ¿generar con repak? o copiar esqueleto del template
      ├── {PluginName}End-WindowsNoEditor.utoc   ← generado por retoc pack-raw
      └── {PluginName}End-WindowsNoEditor.ucas   ← generado por retoc pack-raw
```

---

## 5. Hallazgos técnicos clave

### 5.1 Formato de los assets en IoStore

Los `.uasset` dentro del contenedor IoStore son **Zen packages** (formato binario diferente a los `.uasset` legacy de UE4). Los primeros 4 bytes son `0x00 0x00 0x00 0x00` (no el magic `C1 83 2A 9E` de `.pak` legacy). Esto significa:
- **UAssetAPI NO puede leer/escribir estos archivos directamente** (solo soporta legacy format)
- Para parchear los DataAssets, necesitamos hacerlo a nivel binario (buscar y reemplazar strings dentro del chunk raw) o encontrar una librería que entienda Zen packages.
- **Alternativa viable**: embeber chunks de DataAsset pre-construidos como plantillas binarias y hacer patch de strings.

### 5.2 El `.pak` en IoStore mods

El archivo `.pak` en un mod IoStore es mínimo (3.5 KB) y actúa solo como cabecera del contenedor. El `.utoc` tiene la tabla de contenidos y el `.ucas` los datos. **El `.pak` puede copiarse de un template existente** o generarse con `repak` si conocemos el formato exacto.

### 5.3 Compresión

`retoc pack-raw` NO comprime los chunks (sin Oodle). El resultado es funcional pero ~3x más grande. Para la v1 esto es aceptable; en futuras versiones podría añadirse compresión si retoc la soporta.

### 5.4 ContainerHeader

El chunk de tipo `ContainerHeader` (sufijo `0000000a`) existe en el contenedor pero retoc no puede parsearlo (es un formato custom del motor fork de FF7R). Sin embargo, `retoc unpack-raw` lo extrae como chunk opaco y `retoc pack-raw` lo re-incluye. Para un plugin nuevo, este chunk no existiría — hay que evaluar si el juego lo requiere o si es suficiente con el DirectoryIndex.

### 5.5 Detección automática de personaje

Del mount_point del replacer: `../../../End/Content/Character/Player/PC0003_00_Aerith_Standard/`  
→ `PC0003` = código de personaje (Aerith)  
→ `00` = índice de costume (Standard)  
→ El nombre de la carpeta del personaje codifica toda la información necesaria.

Se necesita un **diccionario PC00XX → nombre de personaje + PlayerType enum** para la detección automática.

---

## 6. Incógnitas pendientes (para resolver antes de implementar)

1. **¿Cómo parchear los Zen packages?** Los assets en el contenedor son Zen format — ¿basta con un string patch del chunk binario para cambiar las rutas internas, o necesitamos entender el formato completo? (Test necesario: cambiar path y verificar que el juego lo carga correctamente)

2. **¿El juego requiere el ContainerHeader para cargar un plugin?** Si un plugin nuevo generado con `pack-raw` (sin ContainerHeader) funciona, perfecto. Si no, necesitamos o copiar/adaptar el ContainerHeader de una plantilla o entender su formato.

3. ~~¿Hay formato especial para el `.pak` IoStore?~~ **RESUELTO** (ver sección 5.5): el `.pak` de Dresscode NO es un contenedor IoStore vacío, es un **pak legacy real** (repak V11) generado por Alpakit, con 3 archivos: `AssetRegistry.bin`, `Config/AccessTransformers.ini`, `Config/PluginSettings.ini`, mount point `../../../End/Mods/{PluginName}/`. `repak pack` puede generarlo. Las dos `.ini` son boilerplate genérico (idénticas para cualquier plugin) — se pueden embeber como plantilla estática. El `AssetRegistry.bin` sí contiene datos específicos del mod (rutas de assets, stats de mesh, nombres de materiales) — queda abierta la incógnita 5 sobre cómo generarlo/parchearlo.

4. **¿Qué contiene el `AerithNier.uasset` (el ModData) exactamente?** Necesitamos entender qué campos hay y cómo parchearlos para crear uno para un nuevo mod.

5. **¿Cómo se genera/parchea `AssetRegistry.bin`?** (nueva, ver 5.5) Contiene referencias a paths de assets, conteos de bones/vértices/triángulos/morph targets, nombres de materiales y texturas. Falta determinar si FF7RML/el juego lo necesita para que el mod cargue correctamente, o si es solo un cache de editor que puede omitirse o dejarse desactualizado sin romper nada (test necesario: ¿el mod funciona en juego si este archivo falta o está "vacío"?).

### 5.5 El `.pak` legacy: contenido real, no un stub vacío

Comparando `repak info`/`list` sobre el `.pak` de un mod Dresscode real vs. el de un replacer:

| | Dresscode (`AerithNierEC...pak`) | Replacer (`ZAerithBahamutRobeStandard_P.pak`) |
|---|---|---|
| mount point | `../../../End/Mods/AerithNierEC/` | `/` |
| version | V11 (Fnv64BugFix) | V11 (Fnv64BugFix) |
| compresión | None | None |
| archivos | 3 (`AssetRegistry.bin`, 2 `.ini`) | 0 |

El replacer no usa su `.pak` para nada (todo el contenido real vive en `.utoc`/`.ucas`); es el patrón estándar de IoStore donde el `.pak` acompañante queda vacío. El plugin Dresscode, en cambio, sí usa su `.pak` legacy para 3 archivos de metadata/config que Alpakit siempre genera al empaquetar un plugin de UE. Esto confirma que `repak pack` (con mount point y version correctos) es la herramienta adecuada para generar este `.pak`, sin necesidad de "adivinar" un formato binario opaco.

---

## 7. Próximos pasos (Etapa 3 — Arquitectura)

Con estos hallazgos, la arquitectura del Converter (Modo B) puede diseñarse con confianza:

1. **PakReader**: wrapper sobre `retoc unpack-raw` — extrae chunks + manifest
2. **AssetAnalyzer**: parsea el manifest para detectar personaje/costume/assets por paths
3. **ManifestRewriter**: renombra paths de replacer a paths de plugin
4. **DataAssetPatcher**: genera/parchea chunks de DA_ModMetaData y ModData desde templates binarios
5. **ContainerBuilder**: invoca `retoc pack-raw` para generar el contenedor final
6. **PluginAssembler**: arma la estructura de carpetas + uplugin + icon + pak

Las incógnitas 1-4 se resuelven con spikes adicionales cortos durante la implementación de cada módulo.

## 8. Investigación del formato Zen (para Modo Full — DataAsset patching)

Investigación estática (sin decompilar el engine, sin acceso al juego) sobre `DA_ModMetaData.uasset` y `AerithNier.uasset` (el `PDA_ModData_Character`) extraídos del mod Dresscode de ejemplo, buscando entender si es viable patchear estos DataAssets de forma genérica.

### 8.1 Header fijo de 64 bytes

Los dos archivos analizados (1207 y 5168 bytes) comparten exactamente esta estructura en los primeros 64 bytes:

```
[0:4)   bHasVersioningInfo / flags       (0 en ambos)
[4:12)  Name (FMappedName del propio paquete, 8 bytes) (0,0 en ambos)
[12:16) PackageFlags                     (0 en ambos)
[16:20) campo grande, no es un offset válido (0x80000000 en ambos) — probablemente CookedHeaderSize con un bit de flag empaquetado
[20:24) offset absoluto (varía por archivo, cerca del final del name map + hashes)
[24:64) 10 campos más de 4 bytes, todos offsets absolutos dentro del archivo salvo alguno en 0
```

**Hallazgo clave**: el campo en `[24:28)` vale **64 en los dos archivos** — confirma que el name map SIEMPRE arranca justo después del header fijo de 64 bytes, sin importar el tamaño del paquete.

### 8.2 Formato del Name Map

Se decodificó exitosamente el name map completo de `DA_ModMetaData.uasset` (21 entradas) parseando cada entrada como:

```
1 byte header: bit7 = ancho (UTF-16 si está seteado), bits0-6 = longitud N
N bytes de datos (ANSI o UTF-16LE según el bit de ancho)
1 byte 0x00 terminador (solo si N > 0)
```

Verificado hasta hacer coincidir el offset final del último nombre con el valor del campo de header `[28:32)` (interpretado como "ImportMapOffset"): coincide exactamente (0x223 = 547 para el archivo de 1207 bytes). Esto da alta confianza en que el parseo es correcto.

Las cadenas de valores reales de las propiedades (ej. `"Aerith Nier"` como valor de `FriendlyName`, `"By TJ"` como `CreatedBy`) **no** están en el name map — están más adelante, en la sección de datos de propiedades tageadas, con el formato clásico de `FString` de Unreal (int32 de longitud + datos + null).

### 8.3 Qué se puede patchear con confianza y qué no

- **Con longitud de string igual (mismo Nº de bytes)**: reemplazar el contenido sin tocar nada más — riesgo prácticamente nulo, ningún offset cambia.
- **Con longitud distinta**: es necesario (a) desplazar todos los bytes posteriores al punto de parche, y (b) sumar el delta a todo campo del header de 64 bytes cuyo valor actual sea un offset mayor al punto de parche. Esto es mecánicamente sencillo de automatizar de forma genérica (sin necesitar saber el nombre semántico de cada campo, solo detectar "parece un offset válido dentro del archivo").
- **Incógnita no resuelta**: la región del "export map" (bytes entre los offsets de `[32:36)` y `[40:44)`, 176 bytes en el archivo pequeño) no tiene un patrón reconocible de offsets/tamaños plausibles — parece contener hashes/GUIDs en vez de una tabla de `FExportMapEntry` con `CookedSerialOffset` legible a simple vista. No se pudo confirmar si additional fixups son necesarios ahí sin decompilar el motor custom de FF7R o probar en el juego.

### 8.4 Decisión

Dado que un error en este parcheo solo es detectable con un crash o un mod que no carga en el juego real (algo que este entorno no puede probar), se decidió **no implementar el patcher de DataAssets todavía** y priorizar en su lugar el Modo Simple/Wrapper (ver `docs/02-documento-tecnico.md` sección 11), que no requiere ninguna de estas modificaciones binarias. Esta investigación queda documentada para retomarla cuando se pueda validar incrementalmente en el juego.

## 9. Modo Simple confirmado insuficiente + pivote a writer propio (post Etapa 6)

### 9.1 Modo Simple no es visible para FF7RML

Probado en el juego real por el usuario: un plugin generado con Modo Simple (contenedor original sin modificar + `.uplugin`/ícono generados) **no aparece en absoluto en la lista de mods de FF7RML**, mientras que mods Dresscode completos sí se listan y funcionan. Esto confirma `docs/01` sección 6: FF7RML descubre mods escaneando el `AssetRegistry` en busca de un `PDA_ModMetaData` en `MetaData/DA_ModMetaData` dentro del contenedor — sin él, el plugin es directamente invisible, no solo ausente del selector de trajes de Dresscode como se asumía antes.

### 9.2 Test de round-trip de identidad con `retoc` — FALLÓ

Antes de arriesgar un patch binario, se probó si `retoc unpack-raw` → `pack-raw` **sin modificar nada** preservaba un contenedor funcional (`tools/roundtrip-test/`). Resultado: **el mod dejó de aparecer en el juego**, igual que Modo Simple.

Diagnóstico (`retoc info` sobre ambos contenedores):

| campo | original | reconstruido por retoc |
|---|---|---|
| `container_flags` | `Compressed \| Indexed` | `Indexed` (sin Compressed) |
| `compression_methods` | `[Oodle]` | `[]` |
| tamaño `.ucas` | 52.9 MB | 165 MB (sin comprimir) |

**Causa raíz confirmada**: `retoc pack-raw` (v0.1.5) **no soporta compresión al reempaquetar** — hay un PR abierto y sin mergear en el repo (`trumank/retoc#58`, "Add compression support to IoStoreWriter") que lo confirma explícitamente. El motor de FF7R aparentemente rechaza/ignora un contenedor sin el flag `Compressed` correctamente poblado. Conclusión: **`retoc` no es viable como base para reconstruir contenedores de este juego**, ni para Modo Simple "reempaquetado" ni para Modo Full.

### 9.3 Pivote: puerto propio del formato IoStore, basado en un proyecto de referencia ya validado

Se encontró **FFVII-Rebirth-Mesh-Patcher** (github.com/nikolaybutnik/FFVII-Rebirth-Mesh-Patcher, MIT), una herramienta Python que repara mods de mallas rotos por el patch V1.005 del juego, parcheando directamente el contenido de paquetes Zen dentro de `.utoc`/`.ucas`. Su código (`lib/iostore.py`, `lib/writer.py`, `lib/dirindex.py`, `lib/zen.py`) documenta el formato binario completo y **prueba explícitamente que su writer reproduce un contenedor sin modificar, byte a byte** (51 chunks, 10821 bytes, idénticos) — la misma prueba que le fallaba a `retoc`.

Se portó esa lógica a C# dentro de `DressCoder.Infrastructure/IoStore/`:

- `IoStoreToc.cs` — reader de `.utoc`/`.ucas` (header de 144 bytes, chunk IDs, tabla offset/length de 10 bytes **big-endian** — la única excepción al resto del formato little-endian —, tabla de bloques de compresión bit-packed, nombres de métodos, directory index, tabla de checksums SHA-1).
- `OodleCompression.cs` — P/Invoke sobre `oo2core_*.dll` (`OodleLZ_Decompress`/`OodleLZ_Compress`, Kraken nivel 4), con verificación de round-trip antes de confiar en cualquier bloque comprimido.
- `DirectoryIndexBuilder.cs` — árbol de directorio como listas enlazadas (`first_child`/`next_sibling`/`first_file`), con **prepend** (no append) para reproducir el orden exacto de Unreal.
- `ZenPackage.cs` — parser de paquete Zen: header fijo de 64 bytes (confirma y reemplaza la investigación empírica de la sección 8), name table, imports, **exports de 72 bytes** con offsets/tamaños exactos.
- `IoStoreContainerWriter.cs` — writer: layout de `.ucas` (bloques alineados a 16 bytes), header de 144 bytes, tabla de checksums.

**Validado**: `tools/roundtrip-test-v2/` reconstruye `AerithNierEC` completo (53 chunks) reusando los bloques crudos originales (sin descomprimir/recomprimir) y compara contra el original — **`.utoc` y `.ucas` resultan 100% idénticos byte a byte**. Esto reemplaza la dependencia de `retoc` para el rebuild del contenedor y desbloquea el patcher de DataAssets (Modo Full) con una base de formato ya probada, en vez de una inferida a mano.

### 9.4 Próximo paso

Usar el mismo patrón que `patch.py`'s `patch_package` (recalcular offsets/tamaños en la export table cuando un export cambia de tamaño, y ajustar `ExportBundlesSize` en el container header) para generar/editar el `PDA_ModMetaData` con los datos reales del replacer. Ver todos `datasset-metadata-patch` en el backlog del proyecto.

### 9.5 `MetadataTemplatePatcher` + `ContainerChunkInjector` implementados y validados

- `MetadataTemplatePatcher.PatchStrings()`: reemplaza valores `FString` (int32 length + UTF-8 + null) dentro de un paquete Zen de un solo export, recalculando el `SerialSize` de ese export. Verificado por re-parseo: el payload resultante contiene exactamente los nuevos strings y ya no el original, con el tamaño correcto.
- `ContainerChunkInjector.InjectAndWrite()`: agrega un `NewContainerChunk` a una copia de un contenedor existente, reusando todos los chunks originales (bloques crudos, sin recomprimir) y dejando el `ContainerHeader` sin tocar.
- **Hallazgo crítico — `retoc verify` no es un oráculo de corrección válido para este juego**: al intentar validar el contenedor inyectado con `retoc verify`, se obtuvo `Error: hash mismatch for chunk #0`. Se sospechó inicialmente un bug propio. Sin embargo, al correr `retoc verify` sobre los archivos **originales, intactos, sin ninguna modificación** — tanto el replacer de ejemplo como `AerithNierEC` (que sabemos funciona perfecto en juego) — **ambos fallan con el mismo error**. Esto confirma que `retoc` simplemente no puede verificar el checksum del chunk `ContainerHeader` (chunk #0) en el motor custom de FF7R, independientemente de si el contenedor es válido o no. **Se descarta `retoc verify` como herramienta de validación de aquí en adelante.**
- Validación alternativa usada (`tools/diag-ucas/`): en vez de depender de `retoc verify`, se reconstruye un contenedor completo (round-trip sin modificar nada) y se compara byte a byte contra el original. En el contenedor del replacer de ejemplo se detectaron 5992 bytes distintos, pero el 100% caen en los huecos de padding *entre* bloques de compresión (para alinear a 16 bytes) — bytes que el juego nunca lee, porque los límites de cada bloque los define la tabla de compresión del `.utoc`, no un escaneo del archivo. Se corrigió además un bug real encontrado en el camino: faltaba el padding final del `.ucas` completo a 16 bytes (`IoStoreContainerWriter.BuildContainer`), que si acortaba el archivo en unos pocos bytes respecto al original (tamaño total no es múltiplo de 16 sin él).
- Con esto, el pipeline de inyección de `DA_ModMetaData` (`tools/inject-metadata-test/`) se considera **funcionalmente correcto y listo para probar en juego**. Se generó un plugin de prueba completo (`tools/pack-test-plugin/` → `tools/test-plugin-output/DressCoderTest/`) usando el `PluginAssembler` real, con el contenedor inyectado copiado como `Content/Paks/WindowsNoEditor/DressCoderTest-WindowsNoEditor.{pak,utoc,ucas}`.
- **Limitación conocida de esta prueba**: el mount point del contenedor todavía es el original del replacer (`../../../End/Content/Character/Player/PC0003_00_Aerith_Standard/`), no reescrito a la convención de Dresscode (`../../../End/Mods/{PluginName}/Content/...`). Esto significa que, funcionalmente, el mod seguirá comportándose como un reemplazo permanente del traje estándar de Aerith (no aparecerá como una opción alternativa dentro del propio menú de Dresscode). Esta prueba solo aísla si la metadata inyectada (`DA_ModMetaData`) alcanza para que **FF7RML/Dresscode detecten la existencia del plugin** — el paso siguiente, ya trackeado como todo `mount-point-rewrite`, es implementar `IManifestRewriter` para lograr un traje realmente seleccionable.

## 10. Descubrimiento decisivo: guía oficial de Dresscode y pausa del proyecto (fin de la Etapa 6)

El usuario compartió la guía oficial de creación de mods Dresscode ("Creating Dresscode compatible Mods" by YIIS, ver `docs/00-dresscode-porting-guide.md`). Esto cambió completamente el diagnóstico:

### 10.1 El único flujo soportado requiere el motor real

La guía confirma que un mod Dresscode se crea así, sin excepciones:
1. Motor custom compilado (`narknon/UnrealEngine-CEEnd`, rama `7Reb`) + proyecto `narknon/FF7R2UProj`.
2. Crear un plugin con **Alpakit** (herramienta de editor).
3. Importar los assets custom en cualquier ubicación del plugin (ya NO hace falta que estén en la ruta original — esto contradecía nuestra suposición de que había que "reemplazar en el lugar").
4. Crear `DA_ModMetaData` (`PDA_ModMetaData`) **y**, crucialmente, un `PDA_ModData_Character` en cualquier parte del plugin — este último es el que Dresscode realmente escanea para poblar la lista de outfits (con `SkeletalMesh`, `PlayerType`, nombre/descripción). **Nuestra implementación nunca llegó a crear este segundo asset.**
5. Empaquetar con el botón "alpakit!" — esto **cocina de verdad** con el motor, generando un `ContainerHeader` legítimo (imports, exports, asset registry) que ningún parcheo binario externo puede replicar sin reimplementar el cocinador de Unreal.

### 10.2 Automatización headless SÍ es posible (si hay motor)

Se encontró que Alpakit internamente es un `BuildCommand` de UAT (`PackagePlugin.cs`, fork de Satisfactory Modding, MIT), invocable 100% headless sin abrir el editor:

```
RunUAT.bat PackagePlugin -Project="FF7R2UProj.uproject" -PluginName="MiMod" -GameDir="..." -CopyToGameDir
```

Esto significa que, **si el usuario tiene el motor compilado**, nuestra app sí podría automatizar el resto del pipeline por completo (generar el plugin, convertir assets con `retoc to-legacy`, generar los DataAssets clonando una plantilla legacy parcheada con UAssetAPI, invocar `RunUAT` headless, copiar el resultado a `End/Mods/`). El usuario decidió que compilar el motor (Visual Studio, cientos de GB, horas) es un costo que no quiere asumir por ahora.

### 10.3 Tres hipótesis de evasión probadas y descartadas (sin motor)

Se intentaron tres atajos para lograr que FF7RML reconozca un mod sin pasar por el cocinador real, probados en el juego por el usuario:

1. **Modo Simple** (copiar el `.pak`/`.utoc`/`.ucas` del replacer verbatim, solo envuelto en `.uplugin` + ícono): no aparece en ningún menú, y **tampoco aplica el reemplazo visual** en el personaje.
2. **Inyección de `DA_ModMetaData`** dentro del `.utoc`/`.ucas` del replacer (dejando el `ContainerHeader` original intacto, ver sección 9.5): tampoco aparece.
3. **`.pak` legacy real vía `repak`** (mount point correcto `../../../End/Mods/{PluginName}/` + `AssetRegistry.bin` real reusado de AerithNierEC sin modificar, + los 2 `.ini` de Alpakit): tampoco aparece.

La conclusión más probable es que el bloqueo real está en el **`ContainerHeader`** del `.utoc` (confirmado en la sección 9.5 como un formato custom del fork de FF7R que no coincide con ninguna de las 7 versiones estándar conocidas de `FIoContainerHeader`, probadas exhaustivamente con `retoc --override-container-header-version`). Sin ese chunk generado correctamente por el motor real, el juego no reconoce el contenido nuevo del contenedor como parte de un plugin cargable, sin importar qué tan bien formado esté el resto (`.pak`, `AssetRegistry.bin`, directory index, metadata).

### 10.4 Estado al pausar (fin de Etapa 6)

El usuario decidió **pausar el proyecto** en este punto en vez de: (a) compilar el motor, (b) preguntar en el Discord OpenFF7R por el formato del `ContainerHeader`, o (c) redefinir el alcance de la app como asistente de preparación para el flujo oficial.

**Lo que SÍ quedó construido y validado, reutilizable en cualquier futura retomada:**
- Reader/writer C# completo y byte-exacto para el formato IoStore (`.utoc`/`.ucas`) — `DressCoder.Infrastructure/IoStore/*`. Confirmado que reproduce contenedores reales de forma idéntica (salvo padding inerte entre bloques).
- Parser de paquetes Zen (`ZenPackage.cs`) y patcher de strings FString de un solo export (`MetadataTemplatePatcher.cs`), ambos verificados funcionando.
- Inyector de chunks nuevos en un contenedor existente (`ContainerChunkInjector.cs`).
- Wrappers para `retoc` y `repak` (`RetocPakReader`, `RepakLegacyPakBuilder`), y el ensamblador de plugin "Modo Simple" (`PluginAssembler`).
- Documentación exhaustiva del formato `.utoc`/Zen/ContainerHeader (secciones 5, 8, 9 de este documento) — invaluable si en el futuro se decide reverse-engenieer el `ContainerHeader` de FF7R con más tiempo, o se consigue esa información de la comunidad.
- La guía oficial de Dresscode completa en `docs/00-dresscode-porting-guide.md`, que documenta el flujo real con el motor.

**Próximo paso si se retoma el proyecto** (ver todo pendiente `mount-point-rewrite`, reformulado): decidir entre (a) compilar el motor y aprovechar el pipeline headless vía `RunUAT PackagePlugin` ya documentado en la sección 10.2, o (b) conseguir documentación de la comunidad sobre el `ContainerHeader` custom de FF7R antes de seguir con parcheo binario puro.

