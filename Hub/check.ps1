param([Parameter(Mandatory)][string]$Out)

$ErrorActionPreference = 'Stop'

$hub = $PSScriptRoot
& (Join-Path $hub 'restore.ps1')
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft Visual Studio", "$env:ProgramFiles\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Roslyn\\csc\.exe$' } |
    Sort-Object { $_.VersionInfo.FileVersion } -Descending |
    Select-Object -First 1

New-Item -ItemType Directory -Force $Out | Out-Null

$frameworkReferences = 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Web.Extensions.dll', 'System.Net.Http.dll', 'System.Management.dll', 'System.IO.Compression.dll', 'System.IO.Compression.FileSystem.dll', 'System.Xml.dll', 'System.Xml.Linq.dll'
$arguments = @('/nologo', '/noconfig', '/target:winexe', '/platform:x64', '/langversion:latest', '/nowarn:CS1701,CS1702', "/out:$Out\GameHub.exe")
$arguments += $frameworkReferences | ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$arguments += "/reference:$hub\lib\Microsoft.Web.WebView2.Core.dll", "/reference:$hub\lib\Microsoft.Web.WebView2.WinForms.dll"
$arguments += (Get-ChildItem "$hub\src" -Filter *.cs).FullName

& $compiler.FullName @arguments
if ($LASTEXITCODE -ne 0) { Write-Host 'Compile failed'; exit 1 }
Write-Host 'Compile OK'
