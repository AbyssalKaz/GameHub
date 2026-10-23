$ErrorActionPreference = 'Stop'

$hub = $PSScriptRoot
& (Join-Path $hub 'restore.ps1')
$bin = Join-Path $hub 'bin'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft Visual Studio", "$env:ProgramFiles\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Roslyn\\csc\.exe$' } |
    Sort-Object { $_.VersionInfo.FileVersion } -Descending |
    Select-Object -First 1

if (-not $compiler) { throw 'No Roslyn C# compiler found (install Visual Studio Build Tools).' }

New-Item -ItemType Directory -Force $bin | Out-Null

$frameworkReferences = 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll', 'System.Management.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll', 'System.Xml.dll', 'System.Xml.Linq.dll'
$arguments = @(
    '/nologo', '/noconfig', '/target:winexe', '/platform:x64', '/optimize+', '/langversion:latest', '/nowarn:CS1701,CS1702',
    "/out:$bin\GameHub.exe"
)
$arguments += $frameworkReferences | ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$arguments += "/reference:$hub\lib\Microsoft.Web.WebView2.Core.dll", "/reference:$hub\lib\Microsoft.Web.WebView2.WinForms.dll"
if (Test-Path "$hub\assets\app.ico") { $arguments += "/win32icon:$hub\assets\app.ico" }
$arguments += (Get-ChildItem "$hub\src" -Filter *.cs).FullName

& $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Copy-Item "$hub\lib\*.dll" $bin -Force
robocopy "$hub\web" "$bin\web" /MIR /NJH /NJS /NFL /NDL /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw 'Copying web files failed' }
$global:LASTEXITCODE = 0
Write-Host "Built $bin\GameHub.exe"
