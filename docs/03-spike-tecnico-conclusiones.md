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

