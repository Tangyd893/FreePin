#requires -version 5.1
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\FreePin\FreePin.csproj'
$output = Join-Path $root 'dist\FreePin'

$msbuildCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe')
)
$msbuild = $msbuildCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild not found. Install Visual Studio 2022 Build Tools with the .NET desktop workload.'
}

$refasm = Join-Path $root 'tools\refasm'
$presentationTasks = Join-Path $refasm 'build\.NETFramework\v4.8\PresentationBuildTasks.dll'
if (-not (Test-Path $presentationTasks)) {
    Write-Host 'Downloading .NET Framework 4.8 reference assemblies...'
    $package = Join-Path $env:TEMP 'Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3.nupkg'
    $zip = Join-Path $env:TEMP 'Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3.zip'
    $url = 'https://www.nuget.org/api/v2/package/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3'
    Invoke-WebRequest -Uri $url -OutFile $package -UseBasicParsing
    Copy-Item $package $zip -Force
    if (Test-Path $refasm) {
        Remove-Item $refasm -Recurse -Force
    }
    New-Item -ItemType Directory -Path $refasm -Force | Out-Null
    Expand-Archive -Path $zip -DestinationPath $refasm -Force
    Remove-Item $package, $zip -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path (Join-Path $root 'lib\Hardcodet.NotifyIcon.Wpf.dll'))) {
    throw 'Missing lib\Hardcodet.NotifyIcon.Wpf.dll. Make sure the repository lib folder is complete.'
}
if (-not (Test-Path (Join-Path $root 'lib\Newtonsoft.Json.dll'))) {
    throw 'Missing lib\Newtonsoft.Json.dll. Make sure the repository lib folder is complete.'
}

if ($Clean) {
    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }
}

Write-Host "MSBuild: $msbuild"
& $msbuild $project /p:Configuration=Release /p:Platform=AnyCPU /v:minimal /nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed. MSBuild exit code: $LASTEXITCODE."
}

$exe = Join-Path $output 'FreePin.exe'
if (-not (Test-Path $exe)) {
    throw "Build completed but $exe was not found."
}
Write-Host "Build succeeded: $exe"
