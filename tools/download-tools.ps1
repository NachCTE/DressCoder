<#
.SYNOPSIS
    Descarga repak y retoc (herramientas MIT/Apache-2.0) a tools/bin/.
    Ver docs/04-licencias-terceros.md para el detalle de licencias.

.DESCRIPTION
    No versionamos estos binarios en git (ver .gitignore); este script los
    descarga desde los releases oficiales de GitHub. Ejecutar una vez tras
    clonar el repositorio.
#>

$ErrorActionPreference = "Stop"

$repakVersion = "v0.2.3"
$retocVersion = "v0.1.5"

$repakUrl = "https://github.com/trumank/repak/releases/download/$repakVersion/repak_cli-x86_64-pc-windows-msvc.zip"
$retocUrl = "https://github.com/trumank/retoc/releases/download/$retocVersion/retoc_cli-x86_64-pc-windows-msvc.zip"

$toolsDir = Join-Path $PSScriptRoot "bin"
$tempDir = Join-Path $PSScriptRoot "_download_tmp"

New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "Descargando repak $repakVersion..."
Invoke-WebRequest -Uri $repakUrl -OutFile "$tempDir\repak.zip" -UseBasicParsing

Write-Host "Descargando retoc $retocVersion..."
Invoke-WebRequest -Uri $retocUrl -OutFile "$tempDir\retoc.zip" -UseBasicParsing

Write-Host "Descomprimiendo..."
Expand-Archive -Path "$tempDir\repak.zip" -DestinationPath "$tempDir\repak_extracted" -Force
Expand-Archive -Path "$tempDir\retoc.zip" -DestinationPath "$tempDir\retoc_extracted" -Force

Copy-Item "$tempDir\repak_extracted\repak.exe" "$toolsDir\repak.exe" -Force
Copy-Item "$tempDir\retoc_extracted\retoc.exe" "$toolsDir\retoc.exe" -Force

Remove-Item $tempDir -Recurse -Force

@"
repak $repakVersion (MIT/Apache-2.0) - https://github.com/trumank/repak/releases/tag/$repakVersion
retoc $retocVersion (MIT) - https://github.com/trumank/retoc/releases/tag/$retocVersion

NOTA: la libreria Oodle (oo2core_*.dll) NO se descarga aqui a proposito.
Es propietaria de RAD Game Tools y no se redistribuye. La app la localiza
en tiempo de ejecucion desde la instalacion del juego. Ver docs/04-licencias-terceros.md.
"@ | Set-Content "$toolsDir\VERSIONS.txt"

Write-Host ""
Write-Host "Listo. Herramientas instaladas en: $toolsDir"
& "$toolsDir\repak.exe" --help | Select-Object -First 3
& "$toolsDir\retoc.exe" --help | Select-Object -First 3
