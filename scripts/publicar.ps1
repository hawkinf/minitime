<#
 Publica o MiniTime para Windows: roda os testes, gera o executável autocontido (win-x64) e junta o leitor de MDB
 (x86, .NET Framework, copiado pelo próprio projeto) na pasta MdbReader\. Saída: dist\MiniTime-<versão>-win-x64.zip
#>
[CmdletBinding()]
param([switch]$SemTestes)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

[xml]$props = Get-Content (Join-Path $raiz 'Directory.Build.props')
$versao = $props.Project.PropertyGroup.Version
if (-not $versao) { $versao = '1.0.0' }

if (-not $SemTestes) {
    dotnet test tests/MiniTime.Tests -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam; publicação cancelada.' }
}

$saida = Join-Path $raiz 'dist\win-x64'
if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }

dotnet publish src/MiniTime.App/MiniTime.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $saida --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o aplicativo.' }

$zip = Join-Path $raiz "dist\MiniTime-$versao-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $saida '*') -DestinationPath $zip
Write-Host "Gerado: $zip"
