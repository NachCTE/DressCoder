# Licencias de dependencias de terceros (herramientas externas)

Este documento registra el estado legal de las herramientas de terceros que la app invoca
como subprocesos, para poder distribuir DressCoder (incluyendo en Nexus Mods) sin problemas
de licencia.

## repak (CLI + biblioteca Rust)

- Repositorio: https://github.com/trumank/repak
- Licencia: **dual MIT / Apache-2.0**
- Autores: Truman Kilen, spuds
- **Redistribución: permitida.** Se embebe el binario `repak.exe` en `tools/bin/` y en el
  paquete final de la app. Se debe incluir el aviso de copyright y una copia de la licencia
  (ambas licencias exigen conservar el aviso) en la carpeta de créditos de la app distribuida.

## retoc (CLI + biblioteca Rust)

- Repositorio: https://github.com/trumank/retoc
- Licencia: **MIT**
- Autores: Truman Kilen, Archengius (con contribuciones de LongerWarrior)
- **Redistribución: permitida.** Se embebe el binario `retoc.exe` en `tools/bin/` y en el
  paquete final de la app, con el mismo requisito de incluir el aviso de copyright.

## Oodle (oo2core_*.dll) — ⚠️ NO SE REDISTRIBUYE

- Propietario: RAD Game Tools (Epic Games)
- Licencia: **propietaria/comercial**, sin términos públicos que autoricen la redistribución
  libre de la DLL fuera del contexto de un juego licenciado que la incluya.
- `retoc.exe` puede descargar/copiar esta DLL automáticamente la primera vez que se ejecuta
  contra contenido comprimido con Oodle (comportamiento observado durante el spike técnico,
  ver docs/03-spike-tecnico-conclusiones.md). **Esto no equivale a tener el derecho de
  redistribuirla nosotros.**
- **Decisión de producto**: la app **NUNCA** empaqueta ni versiona `oo2core_*.dll`.
  - Se agrega a `.gitignore` para evitar que se cometa por accidente.
  - En tiempo de ejecución, la app debe:
    1. Buscar la DLL en la propia instalación del juego del usuario (ruta típica:
       `{FF7RebirthDir}/End/Binaries/Win64/oo2core_*.dll`), donde existe legítimamente
       porque el juego la trae.
    2. Si no la encuentra, pedirle al usuario que indique la ruta de instalación del juego
       o la ubicación de la DLL manualmente (pantalla de configuración inicial).
    3. Copiarla (no redistribuirla en el instalador) al directorio de trabajo temporal que
       usan `repak`/`retoc` en tiempo de ejecución, si estas herramientas la requieren en un
       path específico.
  - Este comportamiento debe documentarse claramente en el README/onboarding de la app para
    que el usuario entienda por qué se le pide la ruta del juego.

## Consecuencia para la arquitectura

El módulo `Infrastructure/ExternalTools` (wrapper de retoc/repak) debe incluir un paso de
resolución de la ruta de Oodle antes de invocar cualquier operación que la requiera, y fallar
con un mensaje claro y accionable si no se encuentra ni se puede localizar.
