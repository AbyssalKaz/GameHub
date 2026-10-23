$ErrorActionPreference = 'Stop'

$hub = $PSScriptRoot
$lib = Join-Path $hub 'lib'
$version = '1.0.4258.31'
$files = @{
    'lib/net462/Microsoft.Web.WebView2.Core.dll'          = 'Microsoft.Web.WebView2.Core.dll'
    'lib/net462/Microsoft.Web.WebView2.WinForms.dll'      = 'Microsoft.Web.WebView2.WinForms.dll'
    'runtimes/win-x64/native/WebView2Loader.dll'          = 'WebView2Loader.dll'
}

$missing = $files.Values | Where-Object { -not (Test-Path (Join-Path $lib $_)) }
if (-not $missing) { return }

Write-Host "Downloading WebView2 SDK $version from NuGet"
New-Item -ItemType Directory -Force $lib | Out-Null
$package = Join-Path ([IO.Path]::GetTempPath()) "webview2-$version.nupkg"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $package -UseBasicParsing

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($entry in $files.Keys) {
        $source = $archive.GetEntry($entry)
        if (-not $source) { throw "WebView2 package is missing $entry" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($source, (Join-Path $lib $files[$entry]), $true)
    }
}
finally {
    $archive.Dispose()
    Remove-Item $package -Force -ErrorAction SilentlyContinue
}
