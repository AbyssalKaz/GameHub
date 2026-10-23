param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Preview,
    [switch]$Quiet,
    [switch]$Pause,
    [string]$PreviewJson
)

$ErrorActionPreference = 'Stop'

$AppFolder = $PSScriptRoot
$ScriptPath = $PSCommandPath
$ConfigPath = Join-Path $AppFolder 'config.json'
$StatePath = Join-Path $AppFolder 'managed.json'
$LogPath = Join-Path $AppFolder 'last-run.log'
$LibraryPath = Join-Path $AppFolder 'library.json'
$ShortcutIconMapPath = Join-Path $AppFolder 'Hub\cache\shortcut-icons\map.json'
$TaskName = 'Game Shortcut Sync'

$UserPrograms = [Environment]::GetFolderPath('Programs')
$CommonPrograms = [Environment]::GetFolderPath('CommonPrograms')
$SyncShortcutPath = Join-Path $UserPrograms 'Sync Game Shortcuts.lnk'

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    Copy-Item -LiteralPath (Join-Path $AppFolder 'config.example.json') -Destination $ConfigPath
}

$ConfigStamp ="$((Get-Item -LiteralPath $ConfigPath).LastWriteTimeUtc.Ticks)"
$Config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$ShortcutFolder = Join-Path $UserPrograms $Config.ShortcutFolderName

$Shell = New-Object -ComObject WScript.Shell
$Log = New-Object System.Collections.Generic.List[string]
$Launchers = [ordered]@{}
$ClaimedFolders = New-Object System.Collections.Generic.List[string]
$SkippedShortcuts = New-Object System.Collections.Generic.List[object]

$ExcludedExePattern = '(?i)^(unins\d*|.*setup.*|.*install.*|.*redist.*|dxsetup|dxwebsetup|directx.*|dotnet.*|ndp\d.*|oalinst|physx.*|.*crash.*|.*report.*|createdump|quicksfv|ue\d?prereq.*|easyanticheat.*|eac(launcher)?|beservice.*|battleye.*|.*updater.*|.*update|patch|.*helper.*|cefsharp.*|ffmpeg.*|gifski|pngcrush.*|smartsteam.*|sselauncher|7z.*|python.*|.*config.*|.*settings.*|.*editor.*|.*server|zfgamebrowser|.*benchmark.*|.*touchup.*|.*cleanup.*|.*elevate.*|xenos.*|.*inject.*|.*trainer.*|.*modder.*|.*sdk.*|.*crack.*|.*keygen.*|.*uninstall.*|.*browser.*|.*error.*|.*webhelper.*|.*overlay.*|.*anticheat.*)$'
$ExcludedDirPattern = '(?i)^(_?commonredist|_?redist|redistributables?|directx|direct x|__installer|installers?|support|engine|easyanticheat|battleye|prerequisites|vcredist|dotnet|monobleedingedge|bepinex|\[utility\].*|smartsteamemu|dlc|.+_data|data_.*|temp-.*|md5|update|patch|crack|save files|screenshots|mods?)$'
$EngineMarkerPattern = '(?i)^(unityplayer\.dll|gameassembly\.dll|steam_api(64)?\.dll|steam_emu\.ini|data\.win|.+\.pck|nw\.dll|package\.nw|rgss\w*\.dll|.+\.rgss\d?a|mkxp\.json|engine|love\.dll|lime\.ndll|fmod(studio)?(64)?\.dll|fna\.dll|monogame\.framework\.dll|galaxy(64)?\.dll|eossdk-win(32|64)-shipping\.dll|bink2?w(32|64)\.dll|goggame-\d+\.(ico|info|dll))$'
$NonGameMarkerPattern = '(?i)^(python\d*\.dll|_internal|base_library\.zip|electron\.exe\.config)$'
$GenericProductPattern ='(?i)^(unity.*|unreal.*|ue[45].*|.*bootstrap.*|game|launcher|nw\.?js|electron|godot.*|game ?maker.*|yoyo.*|rpg ?maker.*|mkxp.*|clickteam.*|.*fusion.*|microsoft.*|windows.*|java.*|python.*|l[oö]ve|chowdren|mono.*)$'
$GenericFolderPattern = '(?i)^(game|games|bin|bin64|binaries|win64|win32|x64|x86|ship|app|files|game files|data|release|build)$'
$ExcludedSteamPattern = '(?i)^(steamworks common redistributables|proton.*|steam linux runtime.*|steamvr.*)$'

function Write-Status([string]$message) {
    $Log.Add($message)
    if (-not $Quiet) { Write-Host $message }
}

function ConvertTo-NameKey([string]$name) {
    if (-not $name) { return '' }
    return ($name.ToLowerInvariant() -replace '[^\p{L}\p{Nd}]', '')
}

function ConvertTo-FileName([string]$name) {
    $clean = $name -replace '[™®©]', ''
    $clean = $clean -replace '\s*:\s*', ' - '
    $clean = $clean -replace '[<>"/\\|?*\x00-\x1F]', ' '
    $clean = ($clean -replace '\s+', ' ').Trim(' ', '.', '-')
    return $clean
}

function Get-CleanFolderName([string]$name) {
    $clean = $name -replace '\[[^\]]*\]|\([^)]*\)|\{[^}]*\}', ' '
    $clean = $clean -replace '(?i)[-_. ]+(steamrip\.com|fitgirl.*|dodi.*|elamigos|tenoke|rune|codex|plaza|skidrow|doge|p2p|gog|ankergames|anker games|insaneramzes|repack|portable|repacks\.site.*)(?=$|[-_. ])', ' '
    $clean = $clean -replace '(?i)[-_. ]+(v?\d+([._]\d+)+[\w.]*|build[-_. ]?\d+.*|update[-_. ].*)$', ''
    if ($clean -notmatch '\s' -and $clean -match '[._]') { $clean = $clean -replace '[._]', ' ' }
    $clean = $clean -replace '_', ' '
    return (($clean -replace '\s+', ' ').Trim(' ', '-', '.'))
}

function Test-ExistingFile([string]$path) {
    return ($path -and (Test-Path -LiteralPath $path -PathType Leaf))
}

function Get-CandidateExes([string]$directory) {
    try {
        return @([IO.Directory]::GetFiles($directory, '*.exe') | Where-Object { [IO.Path]::GetFileNameWithoutExtension($_) -notmatch $ExcludedExePattern })
    }
    catch {
        return @()
    }
}

function Test-EngineMarkers([string]$directory, [string[]]$exes) {
    foreach ($exe in $exes) {
        if ([IO.Directory]::Exists(($exe -replace '\.exe$', '_Data'))) { return $true }
    }

    $isGame = $false
    try {
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($directory)) {
            $name = [IO.Path]::GetFileName($entry)
            if ($name -match $NonGameMarkerPattern) { return $false }
            if ($name -match $EngineMarkerPattern) { $isGame = $true }
        }
    }
    catch { }
    return $isGame
}

function Find-GameRoots([string]$directory, [int]$depth, [bool]$strict) {
    $candidates = Get-CandidateExes $directory
    if ($candidates.Count -gt 0 -and (-not $strict -or (Test-EngineMarkers $directory $candidates))) {
        return [pscustomobject]@{ Root = $directory; Exes = $candidates }
    }
    if ($depth -le 0) { return }

    try { $children = [IO.Directory]::GetDirectories($directory) } catch { return }
    foreach ($child in $children) {
        $info = New-Object IO.DirectoryInfo $child
        if ($info.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
        if ($info.Name -match $ExcludedDirPattern) { continue }
        Find-GameRoots $child ($depth - 1) $strict
    }
}

function Get-FolderKeys([string]$root, [string]$stopAt) {
    $keys = @()
    $folder = $root
    while ($folder -and $folder.Length -gt $stopAt.Length) {
        $key = ConvertTo-NameKey (Get-CleanFolderName (Split-Path $folder -Leaf))
        if ($key) { $keys += $key }
        $folder = Split-Path $folder -Parent
    }
    return $keys
}

function Get-NameMatchScore([string]$exeKey, [string]$folderKey) {
    if (-not $exeKey -or -not $folderKey) { return 0 }
    if ($exeKey -eq $folderKey) { return 100 }
    if ($folderKey.Contains($exeKey) -or $exeKey.Contains($folderKey)) { return 60 }
    if ($exeKey.Length -ge 3 -and $folderKey.StartsWith($exeKey.Substring(0, 3))) { return 20 }
    return 0
}

function Select-MainExe([string[]]$exes, [string]$root, [string]$stopAt) {
    $folderKeys = Get-FolderKeys $root $stopAt
    $best = $null
    $bestScore = [double]::MinValue

    foreach ($exe in $exes) {
        $exeKey = ConvertTo-NameKey ([IO.Path]::GetFileNameWithoutExtension($exe))
        $score = 0.0
        foreach ($folderKey in $folderKeys) {
            $score = [Math]::Max($score, (Get-NameMatchScore $exeKey $folderKey))
        }
        $score += [Math]::Log10([Math]::Max((New-Object IO.FileInfo $exe).Length, 1))
        if ($exeKey -match 'launcher') { $score -= 15 }
        if ($exeKey -match '(x86|32)$') { $score -= 3 }

        if ($score -gt $bestScore) {
            $bestScore = $score
            $best = $exe
        }
    }
    return $best
}

function Find-MainExe([string]$directory, [int]$depth = 4) {
    foreach ($found in @(Find-GameRoots $directory $depth $false)) {
        return Select-MainExe $found.Exes $found.Root (Split-Path $directory -Parent)
    }
    return $null
}

function Get-ScannedGameName([string]$exe, [string]$root, [string]$stopAt) {
    $product = ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductName)
    if ($product) { $product = $product.Trim() }
    if ($product -and $product -notmatch $GenericProductPattern) { return $product }

    $exeName = [IO.Path]::GetFileNameWithoutExtension($exe)
    if ($exeName -match '\S\s+\S' -and $exeName -notmatch $GenericProductPattern) { return $exeName }

    $folder = $root
    while ((Split-Path $folder -Leaf) -match $GenericFolderPattern) {
        $parent = Split-Path $folder -Parent
        if (-not $parent -or $parent.Length -le $stopAt.Length) { break }
        $folder = $parent
    }
    return Get-CleanFolderName (Split-Path $folder -Leaf)
}

function New-Game([string]$name, [string]$source, [string]$url, [string]$exe, [string]$icon, [hashtable]$details = @{}) {
    $game = [ordered]@{
        Id              = $null
        Name            = $name
        Source          = $source
        Status          = 'installed'
        Url             = $url
        Exe             = $exe
        Arguments       = $null
        Icon            = $icon
        SteamAppId      = $null
        InstallPath     = $null
        Parts           = @()
        LastPlayed      = $null
        PlaytimeMinutes = $null
        ReportedBytes   = $null
        LauncherPage    = $null
    }
    foreach ($key in $details.Keys) { $game[$key] = $details[$key] }
    if (-not $game.Id) { $game.Id = 'local:' + "$exe".ToLowerInvariant() }
    return [pscustomobject]$game
}

function New-Part([string]$label, [string]$path) {
    return [pscustomobject][ordered]@{ Label = $label; Path = $path }
}

function ConvertFrom-UnixTime([string]$seconds) {
    if (-not $seconds -or $seconds -eq '0') { return $null }
    return [DateTimeOffset]::FromUnixTimeSeconds([int64]$seconds).UtcDateTime.ToString('o')
}

function Test-PathInside([string]$path, [string]$folder) {
    if (-not $path -or -not $folder) { return $false }
    $path = $path.TrimEnd('\')
    $folder = $folder.TrimEnd('\')
    return ($path -ieq $folder -or $path.StartsWith($folder + '\', [StringComparison]::OrdinalIgnoreCase))
}

function Test-NamesOverlap([string]$first, [string]$second) {
    if (-not $first -or -not $second) { return $false }
    if ($first.Length -ge 4 -and $second.Length -ge 4 -and ($first.Contains($second) -or $second.Contains($first))) { return $true }
    return ($first.Length -ge 6 -and $second.Length -ge 6 -and $first.Substring(0, 6) -eq $second.Substring(0, 6))
}

function Get-LibraryRoots {
    return @($Config.LibraryFolders | ForEach-Object { ([Environment]::ExpandEnvironmentVariables($_)).TrimEnd('\') })
}

function Get-StopFolders {
    if (-not $script:StopFolders) {
        $set = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
        $paths = @(Get-LibraryRoots) + @($Config.SearchFolders | ForEach-Object { [Environment]::ExpandEnvironmentVariables($_) }) +
            @($env:USERPROFILE, "$env:USERPROFILE\Downloads", "$env:USERPROFILE\Desktop", "$env:USERPROFILE\Documents", $env:ProgramFiles, ${env:ProgramFiles(x86)}, "$env:ProgramFiles\Epic Games")
        foreach ($path in $paths) { if ($path) { [void]$set.Add($path.TrimEnd('\')) } }
        $script:StopFolders = $set
    }
    return $script:StopFolders
}

function Get-InstallRoot([string]$exe) {
    $directory = Split-Path $exe -Parent
    foreach ($library in Get-LibraryRoots) {
        if ($directory.StartsWith($library + '\', [StringComparison]::OrdinalIgnoreCase)) {
            return Join-Path $library ($directory.Substring($library.Length + 1).Split('\')[0])
        }
    }

    $steamCommon = [regex]::Match($directory, '(?i)^(.*\\steamapps\\common\\[^\\]+)')
    if ($steamCommon.Success) { return $steamCommon.Groups[1].Value }

    $stops = Get-StopFolders
    $current = $directory
    while ($true) {
        $parent = Split-Path $current -Parent
        if (-not $parent -or $parent.Length -le 3 -or $stops.Contains($parent)) { break }

        try {
            $subfolderCount = [IO.Directory]::GetDirectories($parent).Count
            $fileCount = [IO.Directory]::GetFiles($parent).Count
        }
        catch { break }

        $leaf = Split-Path $current -Leaf
        $overlap = Test-NamesOverlap (ConvertTo-NameKey (Get-CleanFolderName $leaf)) (ConvertTo-NameKey (Get-CleanFolderName (Split-Path $parent -Leaf)))
        $climb = (($leaf -match $GenericFolderPattern) -and $subfolderCount -le 4) -or ($subfolderCount -le 2 -and $overlap)
        if (-not $climb) { break }
        $current = $parent
    }
    return $current
}

function Get-ProtocolHandlerExists([string]$scheme) {
    return (Test-Path -LiteralPath "Registry::HKEY_CLASSES_ROOT\$scheme\shell\open\command")
}

function Get-ProtocolHandlerExe([string]$scheme) {
    $command = (Get-ItemProperty -LiteralPath "Registry::HKEY_CLASSES_ROOT\$scheme\shell\open\command" -ErrorAction SilentlyContinue).'(default)'
    if (-not $command) { return $null }
    $match = [regex]::Match($command.Trim(), '^"([^"]+)"|^(\S+)')
    $exe = $match.Groups[1].Value + $match.Groups[2].Value
    if (Test-ExistingFile $exe) { return $exe }
    return $null
}

function ConvertTo-WindowsPath([string]$path) {
    if (-not $path) { return $null }
    try { return [IO.Path]::GetFullPath($path.Trim('"').Replace('/', '\')).TrimEnd('\') } catch { return $null }
}

function Test-FolderHasContent([string]$path) {
    if (-not $path -or -not [IO.Directory]::Exists($path)) { return $false }
    try { return [IO.Directory]::EnumerateFileSystemEntries($path).GetEnumerator().MoveNext() } catch { return $false }
}

function Get-YamlValue([string]$text, [string]$key) {
    $match = [regex]::Match($text, '(?m)^' + [regex]::Escape($key) + ':[ \t]*"?([^"\r\n]*)"?[ \t]*\r?$')
    if ($match.Success) { return $match.Groups[1].Value }
    return $null
}

function Get-VdfValue([string]$text, [string]$key) {
    $match = [regex]::Match($text, '(?im)^\s*"' + [regex]::Escape($key) + '"\s+"((?:[^"\\]|\\.)*)"')
    if ($match.Success) { return ($match.Groups[1].Value -replace '\\(.)', '$1') }
    return $null
}

function Get-SteamGames {
    $steamKey = Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if (-not $steamKey -or -not $steamKey.SteamPath) { return }

    $steamRoot = [IO.Path]::GetFullPath($steamKey.SteamPath).TrimEnd('\')
    $steamExe = Join-Path $steamRoot 'steam.exe'
    $libraries = New-Object System.Collections.Generic.List[string]
    $libraries.Add($steamRoot)

    $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $libraryFile) {
        $text = Get-Content -LiteralPath $libraryFile -Raw -Encoding UTF8
        foreach ($match in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
            $libraries.Add([IO.Path]::GetFullPath(($match.Groups[1].Value -replace '\\\\', '\')).TrimEnd('\'))
        }
    }

    foreach ($library in ($libraries | Sort-Object -Unique)) {
        $appsFolder = Join-Path $library 'steamapps'
        if (-not (Test-Path -LiteralPath $appsFolder)) { continue }

        foreach ($manifest in Get-ChildItem -LiteralPath $appsFolder -Filter 'appmanifest_*.acf' -ErrorAction SilentlyContinue) {
            $text = Get-Content -LiteralPath $manifest.FullName -Raw -Encoding UTF8
            $appId = Get-VdfValue $text 'appid'
            $name = Get-VdfValue $text 'name'
            $installDir = Get-VdfValue $text 'installdir'
            $stateFlags = [int64](Get-VdfValue $text 'StateFlags')

            if (-not $appId -or -not $name -or $appId -eq '228980') { continue }
            if ($name -match $ExcludedSteamPattern) { continue }

            $installed = ($stateFlags -band 4) -ne 0
            $download = Get-SteamDownload $text $stateFlags
            if (-not $installed -and -not $download) { continue }

            $gameFolder = Join-Path $appsFolder "common\$installDir"
            if ($installed -and -not (Test-Path -LiteralPath $gameFolder)) { continue }

            $icon = $(if ($installed) { Find-MainExe $gameFolder } else { $null })
            if (-not $icon) { $icon = $steamExe }

            $parts = @()
            foreach ($extra in @(@('Workshop content', "workshop\content\$appId"), @('Shader cache', "shadercache\$appId"), @('Pending download', "downloading\$appId"))) {
                $extraPath = Join-Path $appsFolder $extra[1]
                if (Test-Path -LiteralPath $extraPath) { $parts += New-Part $extra[0] $extraPath }
            }

            $reported = Get-VdfValue $text 'SizeOnDisk'
            New-Game $name 'Steam' "steam://rungameid/$appId" $null $icon @{
                Id            = "steam:$appId"
                SteamAppId    = $appId
                InstallPath   = $gameFolder
                Parts         = $parts
                LastPlayed    = ConvertFrom-UnixTime (Get-VdfValue $text 'LastPlayed')
                ReportedBytes = $(if ($reported) { [int64]$reported } else { $null })
                LauncherPage  = $(if ($installed) { "steam://nav/games/details/$appId" } else { 'steam://open/downloads' })
                Status        = $(if ($installed) { 'installed' } else { 'downloading' })
                Download      = $download
                NeedsUpdate   = $(if ($installed -and ($stateFlags -band 2)) { $true } else { $null })
            }
        }
    }
}

function New-DownloadState($progress, $bytesDone, $bytesTotal, [string]$state) {
    return [pscustomobject][ordered]@{
        Progress   = $progress
        BytesDone  = $bytesDone
        BytesTotal = $bytesTotal
        State      = $state
    }
}

function Get-SteamDownload([string]$text, [int64]$flags) {
    $activeFlags = 256 -bor 512 -bor 1024 -bor 262144 -bor 524288 -bor 1048576 -bor 2097152 -bor 4194304
    $installed = ($flags -band 4) -ne 0
    if ($installed -and -not ($flags -band $activeFlags)) { return $null }

    $toDownload = [int64]("0" + (Get-VdfValue $text 'BytesToDownload'))
    $downloaded = [int64]("0" + (Get-VdfValue $text 'BytesDownloaded'))
    $toStage = [int64]("0" + (Get-VdfValue $text 'BytesToStage'))
    $staged = [int64]("0" + (Get-VdfValue $text 'BytesStaged'))
    if (-not $installed -and -not ($flags -band $activeFlags) -and $toDownload -eq 0) { return $null }

    $staging = $toDownload -gt 0 -and $downloaded -ge $toDownload -and $toStage -gt 0
    $done = $(if ($staging) { $staged } else { $downloaded })
    $total = $(if ($staging) { $toStage } else { $toDownload })
    if ($installed -and $total -gt 0 -and $done -ge $total) { return $null }

    $state = 'queued'
    if ($flags -band 512) { $state = 'paused' }
    elseif ($staging -or ($flags -band (2097152 -bor 4194304))) { $state = 'installing' }
    elseif ($flags -band (256 -bor 1048576 -bor 262144 -bor 524288)) { $state = 'downloading' }

    $progress = $(if ($total -gt 0) { [Math]::Round([Math]::Min(1.0, $done / $total), 4) } else { $null })
    return New-DownloadState $progress $(if ($total -gt 0) { $done } else { $null }) $(if ($total -gt 0) { $total } else { $null }) $state
}

function ConvertTo-HydraDownload($download) {
    $total = $(if ($download.fileSize) { [int64]$download.fileSize } else { $null })
    $done = $(if ($download.bytesDownloaded) { [int64]$download.bytesDownloaded } else { $null })
    $progress = $(if ($null -ne $download.progress) { [Math]::Round([double]$download.progress, 4) } elseif ($total -and $done) { [Math]::Round($done / $total, 4) } else { $null })

    $state = switch ($download.status) {
        'active' { 'downloading' }
        'paused' { 'paused' }
        'error' { 'error' }
        default { 'queued' }
    }
    if ($download.extracting) { $state = 'installing' }
    return New-DownloadState $progress $done $total $state
}

function Get-EpicGames {
    $manifestFolder = Join-Path $env:ProgramData 'Epic\EpicGamesLauncher\Data\Manifests'
    if (-not (Test-Path -LiteralPath $manifestFolder)) { return }

    foreach ($file in Get-ChildItem -LiteralPath $manifestFolder -Filter '*.item') {
        try { $item = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { continue }
        if (-not $item.LaunchExecutable -or -not $item.InstallLocation) { continue }
        if ($item.AppCategories -and ($item.AppCategories -notcontains 'games')) { continue }

        $exe = [IO.Path]::GetFullPath((Join-Path $item.InstallLocation $item.LaunchExecutable))
        $incomplete = [bool]$item.bIsIncompleteInstall
        if (-not $incomplete -and -not (Test-ExistingFile $exe)) { continue }

        $url = 'com.epicgames.launcher://apps/{0}%3A{1}%3A{2}?action=launch&silent=true' -f $item.CatalogNamespace, $item.CatalogItemId, $item.AppName
        New-Game $item.DisplayName 'Epic' $url $exe $exe @{
            Id            = "epic:$($item.AppName)"
            Status        = $(if ($incomplete) { 'downloading' } else { 'installed' })
            Download      = $(if ($incomplete) { New-DownloadState $null $null $(if ($item.InstallSize) { [int64]$item.InstallSize } else { $null }) 'downloading' } else { $null })
            InstallPath   = [IO.Path]::GetFullPath($item.InstallLocation).TrimEnd('\')
            ReportedBytes = $(if ($item.InstallSize) { [int64]$item.InstallSize } else { $null })
            LauncherPage  = 'com.epicgames.launcher://store/library'
        }
    }
}

function Get-HydraGames {
    $hydraData = Join-Path $env:APPDATA 'hydralauncher'
    $database = Join-Path $hydraData 'hydra-db'
    if (-not (Test-Path -LiteralPath $database)) { return }

    if (-not ('LevelDbReader' -as [type])) { Add-Type -Path (Join-Path $AppFolder 'LevelDbReader.cs') }
    $records = [LevelDbReader]::ReadAll($database)
    $useDeepLink = Get-ProtocolHandlerExists 'hydralauncher'

    $downloadFolders = @{}
    $activeDownloads = @{}
    foreach ($key in $records.Keys) {
        if (-not $key.StartsWith('!downloads!')) { continue }
        try { $download = $records[$key] | ConvertFrom-Json } catch { continue }
        if ($download.status -eq 'removed') { continue }
        $downloadKey = $key.Substring('!downloads!'.Length)
        if ($download.status -in 'active', 'waiting', 'paused', 'error' -or $download.extracting) {
            $activeDownloads[$downloadKey] = ConvertTo-HydraDownload $download
        }
        if (-not $download.folderName -or -not $download.downloadPath) { continue }
        $folder = Join-Path $download.downloadPath $download.folderName
        if (Test-Path -LiteralPath $folder) { $downloadFolders[$downloadKey] = $folder.TrimEnd('\') }
    }

    foreach ($key in $records.Keys) {
        if (-not $key.StartsWith('!games!')) { continue }
        try { $game = $records[$key] | ConvertFrom-Json } catch { continue }
        if ($game.isDeleted) { continue }

        $gameKey = $key.Substring('!games!'.Length)
        $downloadFolder = $downloadFolders[$gameKey]
        $activeDownload = $activeDownloads[$gameKey]
        $exe = $game.executablePath
        $installed = Test-ExistingFile $exe
        if (-not $installed -and -not $downloadFolder -and -not $activeDownload) { continue }

        $icon = Join-Path $hydraData ("Assets\{0}-{1}\icon.ico" -f $game.shop, $game.objectId)
        if (-not (Test-ExistingFile $icon)) { $icon = $(if ($installed) { $exe } else { $null }) }

        $details = @{
            Id              = "hydra:$gameKey"
            SteamAppId      = $(if ($game.shop -eq 'steam') { "$($game.objectId)" } else { $null })
            LastPlayed      = $(if ($game.lastTimePlayed) { ([datetime]$game.lastTimePlayed).ToUniversalTime().ToString('o') } else { $null })
            PlaytimeMinutes = $(if ($game.playTimeInMilliseconds) { [int]([int64]$game.playTimeInMilliseconds / 60000) } else { $null })
            ReportedBytes   = $(if ($game.installedSizeInBytes) { [int64]$game.installedSizeInBytes } else { $null })
        }

        if ($activeDownload) { $details.Download = $activeDownload }

        if (-not $installed) {
            $details.Status = $(if ($activeDownload) { 'downloading' } else { 'installer' })
            $details.Parts = @()
            if ($downloadFolder) { $details.Parts = @(New-Part $(if ($activeDownload) { 'Download' } else { 'Downloaded installer' }) $downloadFolder) }
            New-Game $game.title 'Hydra' $null $null $icon $details
            continue
        }

        $installPath = Get-InstallRoot $exe
        $parts = @()
        if ($downloadFolder) {
            if (Test-PathInside $installPath $downloadFolder) { $installPath = $downloadFolder }
            elseif (-not (Test-PathInside $downloadFolder $installPath)) { $parts += New-Part 'Downloaded installer' $downloadFolder }
        }
        $details.InstallPath = $installPath
        $details.Parts = $parts

        $url = $(if ($useDeepLink) { "hydralauncher://run?shop={0}&objectId={1}" -f $game.shop, $game.objectId } else { $null })
        New-Game $game.title 'Hydra' $url $exe $icon $details
    }
}

$RiotProductNames = @{
    league_of_legends = 'League of Legends'
    teamfighttactics  = 'Teamfight Tactics'
    valorant          = 'VALORANT'
    lion              = '2XKO'
    bacon             = 'Legends of Runeterra'
}

function Get-RiotClientExe {
    $installsFile = Join-Path $env:ProgramData 'Riot Games\RiotClientInstalls.json'
    if (Test-Path -LiteralPath $installsFile) {
        try {
            $installs = Get-Content -LiteralPath $installsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($candidate in @($installs.rc_default, $installs.rc_live)) {
                $client = ConvertTo-WindowsPath $candidate
                if (Test-ExistingFile $client) { return $client }
            }
        }
        catch { }
    }
    return Get-ProtocolHandlerExe 'riotclient'
}

function Get-RiotGames {
    $metadata = Join-Path $env:ProgramData 'Riot Games\Metadata'
    $client = Get-RiotClientExe
    if (-not $client -or -not (Test-Path -LiteralPath $metadata)) { return }
    $Launchers['riot'] = $client

    foreach ($file in Get-ChildItem -LiteralPath $metadata -Recurse -Filter '*.product_settings.yaml' -ErrorAction SilentlyContinue) {
        $product = $file.Name -replace '\.product_settings\.yaml$', ''
        $productId, $patchline = $product.Split('.', 2)
        if (-not $patchline) { continue }

        $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $installPath = ConvertTo-WindowsPath (Get-YamlValue $text 'product_install_full_path')
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $uninstall = Get-ItemProperty -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Riot Game $product" -ErrorAction SilentlyContinue
        $name = "$($uninstall.DisplayName)".Trim()
        if (-not $name) { $name = (Get-YamlValue $text 'shortcut_name') -replace '\.lnk$', '' }
        if (-not $name) { $name = $RiotProductNames[$productId] }
        if (-not $name) { $name = (Get-Culture).TextInfo.ToTitleCase($productId.Replace('_', ' ')) }
        if ($patchline -ne 'live' -and $name -notmatch [regex]::Escape($patchline)) { $name = "$name ($($patchline.ToUpperInvariant()))" }

        $icon = Join-Path $file.DirectoryName "$product.ico"
        if (-not (Test-ExistingFile $icon)) { $icon = ConvertTo-WindowsPath $uninstall.DisplayIcon }
        if (-not (Test-ExistingFile $icon)) { $icon = Find-MainExe $installPath }
        if (-not $icon) { $icon = $client }

        New-Game $name 'Riot' $null $client $icon @{
            Id            = "riot:$product"
            Arguments     = "--launch-product=$productId --launch-patchline=$patchline"
            InstallPath   = $installPath
            ReportedBytes = $(if ($uninstall.EstimatedSize) { [int64]$uninstall.EstimatedSize * 1024 } else { $null })
            LauncherPage  = $client
        }
    }
}

function Get-XboxGameRoots {
    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($drive in [IO.DriveInfo]::GetDrives()) {
        if ($drive.DriveType -notin 'Fixed', 'Removable' -or -not $drive.IsReady) { continue }
        $marker = Join-Path $drive.RootDirectory.FullName '.GamingRoot'
        if (-not [IO.File]::Exists($marker)) { continue }

        $bytes = [IO.File]::ReadAllBytes($marker)
        if ($bytes.Length -le 8) { continue }
        foreach ($folder in [Text.Encoding]::Unicode.GetString($bytes, 8, $bytes.Length - 8).Split([char]0)) {
            if (-not $folder) { continue }
            $root = Join-Path $drive.RootDirectory.FullName $folder
            if ([IO.Directory]::Exists($root)) { $roots.Add($root.TrimEnd('\')) }
        }
    }
    return $roots | Sort-Object -Unique
}

function Get-PngWidth([string]$file) {
    try {
        $stream = [IO.File]::OpenRead($file)
        try {
            $header = New-Object byte[] 24
            if ($stream.Read($header, 0, 24) -lt 24) { return 0 }
        }
        finally { $stream.Dispose() }
        return ([int]$header[16] -shl 24) -bor ([int]$header[17] -shl 16) -bor ([int]$header[18] -shl 8) -bor [int]$header[19]
    }
    catch { return 0 }
}

function Find-PackageLogo([string]$content, [string[]]$logos) {
    $best = $null
    $bestWidth = 0
    foreach ($logo in $logos | Where-Object { $_ } | Select-Object -Unique) {
        $folder = [IO.Path]::Combine($content, [IO.Path]::GetDirectoryName($logo))
        if (-not [IO.Directory]::Exists($folder)) { continue }
        foreach ($file in [IO.Directory]::GetFiles($folder, [IO.Path]::GetFileNameWithoutExtension($logo) + '*.png')) {
            if ([IO.Path]::GetFileName($file) -match '(?i)contrast-|lightunplated') { continue }
            $width = Get-PngWidth $file
            if ($width -gt $bestWidth) {
                $best = $file
                $bestWidth = $width
            }
        }
    }
    return $best
}

function Get-XboxGames {
    $roots = @(Get-XboxGameRoots)
    if ($roots.Count -eq 0) { return }

    $packages = @{}
    foreach ($package in Get-AppxPackage -ErrorAction SilentlyContinue) { $packages[$package.Name] = $package }

    $xboxApp = $packages['Microsoft.GamingApp']
    if ($xboxApp) {
        $xboxAppExe = Join-Path $xboxApp.InstallLocation 'XboxPcApp.exe'
        if (Test-ExistingFile $xboxAppExe) { $Launchers['xbox'] = $xboxAppExe }
    }
    $useXboxApp = Test-Path -LiteralPath 'Registry::HKEY_CLASSES_ROOT\msxbox'

    foreach ($root in $roots) {
        foreach ($folder in [IO.Directory]::GetDirectories($root)) {
            $content = Join-Path $folder 'Content'
            $manifestPath = Join-Path $content 'appxmanifest.xml'
            if (-not (Test-ExistingFile $manifestPath)) { continue }

            try { [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 } catch { continue }
            $package = $packages[$manifest.Package.Identity.Name]
            if (-not $package) { continue }

            $config = $null
            $configPath = Join-Path $content 'MicrosoftGame.Config'
            if (Test-ExistingFile $configPath) {
                try { [xml]$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 } catch { }
            }

            $executables = @($config.Game.ExecutableList.Executable | Where-Object { $_ -and $_.Name })
            $applications = @($manifest.Package.Applications.Application | Where-Object { $_ -and $_.Id })
            if ($applications.Count -eq 0) { continue }
            $application = $applications | Where-Object { $executables.Count -gt 0 -and $_.Id -eq $executables[0].Id } | Select-Object -First 1
            if (-not $application) { $application = $applications[0] }

            $name = $manifest.Package.Properties.DisplayName
            if (-not $name -or $name -match '^ms-resource:') { $name = $config.Game.ShellVisuals.DefaultDisplayName }
            if (-not $name -or $name -match '^ms-resource:') { $name = Split-Path $folder -Leaf }

            $logos = @($application.VisualElements.Square44x44Logo, $application.VisualElements.Square150x150Logo, $manifest.Package.Properties.Logo)
            if ($config) { $logos += @($config.Game.ShellVisuals.Attributes | Where-Object { $_.Name -match 'Logo$' } | ForEach-Object { $_.Value }) }
            $icon = Find-PackageLogo $content $logos
            if (-not $icon -and $executables.Count -gt 0) { $icon = Join-Path $content $executables[0].Name }
            if (-not (Test-ExistingFile $icon)) { $icon = Find-MainExe $content 2 }

            $storeId = "$($config.Game.StoreId)".Trim()
            New-Game $name 'Xbox' ('shell:AppsFolder\{0}!{1}' -f $package.PackageFamilyName, $application.Id) $null $icon @{
                Id           = "xbox:$($package.PackageFamilyName)"
                InstallPath  = $folder
                LauncherPage = $(if ($useXboxApp -and $storeId) { "msxbox://game/?productId=$storeId" } else { $null })
            }
        }
    }
}

$HoYoPlayGameNames = @{
    hk4e  = @('Genshin Impact', 'GenshinImpact.exe')
    hkrpg = @('Honkai: Star Rail', 'StarRail.exe')
    nap   = @('Zenless Zone Zero', 'ZenlessZoneZero.exe')
    bh3   = @('Honkai Impact 3rd', 'BH3.exe')
}

function Read-HoYoPlayGameData([string]$file) {
    $records = @{}
    if (-not (Test-ExistingFile $file)) { return $records }

    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file))
    foreach ($match in [regex]::Matches($text, '\{"download_transaction_no".*?"wpf_pkg_version_dir":"[^"]*"\}\}')) {
        try { $record = $match.Value | ConvertFrom-Json } catch { continue }
        if (-not $record.gameBiz) { continue }

        $key = $record.gameBiz + $record.packageName
        $previous = $records[$key]
        if ($previous -and [int64]("0" + $previous.playStatus.lastPlayTime) -gt [int64]("0" + $record.playStatus.lastPlayTime)) {
            $record.playStatus.lastPlayTime = $previous.playStatus.lastPlayTime
        }
        $records[$key] = $record
    }
    return $records
}

function Get-HoYoPlayGames {
    foreach ($vendor in 'Cognosphere', 'miHoYo') {
        $registryRoot = "HKCU:\Software\$vendor\HYP\1_0"
        if (-not (Test-Path -LiteralPath $registryRoot)) { continue }

        $launcherFolder = (Get-ItemProperty -LiteralPath $registryRoot -ErrorAction SilentlyContinue).InstallPath
        $launcher = $(if ($launcherFolder) { Join-Path $launcherFolder 'launcher.exe' } else { $null })
        if (-not (Test-ExistingFile $launcher)) { $launcher = $null }
        if ($launcher -and -not $Launchers.Contains('hoyoplay')) { $Launchers['hoyoplay'] = $launcher }

        $records = Read-HoYoPlayGameData (Join-Path $env:APPDATA "$vendor\HYP\1_0\data\gamedata.dat")
        foreach ($key in Get-ChildItem -LiteralPath $registryRoot -ErrorAction SilentlyContinue) {
            $settings = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue
            $installPath = ConvertTo-WindowsPath $settings.GameInstallPath
            if (-not (Test-FolderHasContent $installPath)) { continue }

            $biz = $(if ($settings.GameBiz) { $settings.GameBiz } else { $key.PSChildName })
            $record = $records[$biz + $settings.GamePackageName]
            if (-not $record) { $record = $records[$biz] }
            $known = $HoYoPlayGameNames[($biz -replace '_.*$', '')]

            $exeName = $record.gameInstallStatus.gameExeName
            if (-not $exeName -and $known) { $exeName = $known[1] }
            $exe = $(if ($exeName) { Join-Path $installPath $exeName } else { $null })
            if (-not (Test-ExistingFile $exe)) { $exe = Find-MainExe $installPath 2 }
            if (-not $exe) { continue }

            $name = $record.gameShortcutName
            if (-not $name -and $known) { $name = $known[0] }
            if (-not $name) { $name = Split-Path $installPath -Leaf }

            $icon = $settings.GameIconPath
            if (-not (Test-ExistingFile $icon)) { $icon = $exe }

            New-Game $name 'HoYoPlay' $null $exe $icon @{
                Id           = "hoyoplay:$biz$($settings.GamePackageName)"
                InstallPath  = $installPath
                LastPlayed   = ConvertFrom-UnixTime $record.playStatus.lastPlayTime
                LauncherPage = $launcher
            }
        }
    }
}

function Get-EAGames {
    $desktop = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Electronic Arts\EA Desktop' -ErrorAction SilentlyContinue).DesktopAppPath
    if (-not (Test-ExistingFile $desktop)) { $desktop = Get-ProtocolHandlerExe 'origin2' }
    if (-not $desktop) { return }
    $Launchers['ea'] = $desktop

    $folders = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($uninstallRoot in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        foreach ($key in Get-ChildItem -LiteralPath $uninstallRoot -ErrorAction SilentlyContinue) {
            $entry = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue
            if ($entry.Publisher -notmatch '^Electronic Arts') { continue }
            $folder = ConvertTo-WindowsPath $entry.InstallLocation
            if ($folder) { [void]$folders.Add($folder) }
        }
    }
    foreach ($library in @("$env:ProgramFiles\EA Games", "${env:ProgramFiles(x86)}\Origin Games")) {
        if (-not [IO.Directory]::Exists($library)) { continue }
        foreach ($folder in [IO.Directory]::GetDirectories($library)) { [void]$folders.Add($folder) }
    }

    foreach ($folder in $folders) {
        $manifestPath = Join-Path $folder '__Installer\installerdata.xml'
        if (-not (Test-ExistingFile $manifestPath)) { continue }
        try { [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 } catch { continue }

        $runtimeLaunchers = @($manifest.SelectNodes('//*[local-name()="runtime"]/*[local-name()="launcher"]'))
        $launcher = $runtimeLaunchers | Where-Object { $_.SelectSingleNode('*[local-name()="trial"]').InnerText -ne 'true' } | Select-Object -First 1
        if (-not $launcher) { continue }

        $relativeExe = $launcher.SelectSingleNode('*[local-name()="filePath"]').InnerText -replace '^\[[^\]]*\]', ''
        $exe = Join-Path $folder $relativeExe.TrimStart('\')
        if (-not (Test-ExistingFile $exe)) { continue }

        $titleNode = $manifest.SelectSingleNode('//*[local-name()="gameTitle"][@locale="en_US"]')
        if (-not $titleNode) { $titleNode = $manifest.SelectSingleNode('//*[local-name()="localeInfo"][@locale="en_US"]/*[local-name()="title"]') }
        if (-not $titleNode) { $titleNode = $manifest.SelectSingleNode('//*[local-name()="gameTitle"]') }
        $name = $(if ($titleNode) { $titleNode.InnerText.Trim() } else { Split-Path $folder -Leaf })

        $contentId = $manifest.SelectSingleNode('//*[local-name()="contentID"]')
        New-Game $name 'EA' $null $exe $exe @{
            Id           = 'ea:' + $(if ($contentId) { $contentId.InnerText.Trim() } else { $folder.ToLowerInvariant() })
            InstallPath  = $folder
            LauncherPage = $desktop
        }
    }
}

function Get-RockstarGames {
    $registryRoot = 'HKLM:\SOFTWARE\WOW6432Node\Rockstar Games'
    $launcherFolder = (Get-ItemProperty -LiteralPath "$registryRoot\Launcher" -ErrorAction SilentlyContinue).InstallFolder
    if (-not $launcherFolder) { return }

    $launcher = Join-Path $launcherFolder 'LauncherPatcher.exe'
    if (-not (Test-ExistingFile $launcher)) { $launcher = Join-Path $launcherFolder 'Launcher.exe' }
    if (-not (Test-ExistingFile $launcher)) { return }
    $Launchers['rockstar'] = $launcher

    foreach ($key in Get-ChildItem -LiteralPath $registryRoot -ErrorAction SilentlyContinue) {
        if ($key.PSChildName -in 'Launcher', 'Rockstar Games Social Club') { continue }
        $installPath = ConvertTo-WindowsPath (Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue).InstallFolder
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $icon = Find-MainExe $installPath 2
        if (-not $icon) { continue }

        $name = $key.PSChildName -replace '^GTA ?V\b', 'Grand Theft Auto V'
        New-Game $name 'Rockstar' $null $launcher $icon @{
            Id           = 'rockstar:' + $key.PSChildName.ToLowerInvariant()
            Arguments    = "-launchTitleInFolder `"$installPath`""
            InstallPath  = $installPath
            LauncherPage = $launcher
        }
    }
}

function Expand-ConfigPath([string]$path) {
    if (-not $path) { return $null }
    return ([Environment]::ExpandEnvironmentVariables($path.Trim().Trim('"'))).TrimEnd('\')
}

function Get-UninstallEntries {
    if ($script:UninstallEntries) { return $script:UninstallEntries }

    $entries = New-Object System.Collections.Generic.List[object]
    $locations = @(
        @([Microsoft.Win32.RegistryHive]::CurrentUser, 'Software\Microsoft\Windows\CurrentVersion\Uninstall'),
        @([Microsoft.Win32.RegistryHive]::LocalMachine, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'),
        @([Microsoft.Win32.RegistryHive]::LocalMachine, 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')
    )
    foreach ($location in $locations) {
        try {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($location[0], [Microsoft.Win32.RegistryView]::Default)
            $root = $base.OpenSubKey($location[1])
            if (-not $root) { continue }
            foreach ($name in $root.GetSubKeyNames()) {
                $key = $root.OpenSubKey($name)
                if (-not $key) { continue }
                $entries.Add([pscustomobject]@{
                    Key             = $name
                    DisplayName     = "$($key.GetValue('DisplayName'))".Trim()
                    UninstallString = "$($key.GetValue('UninstallString'))"
                    InstallLocation = "$($key.GetValue('InstallLocation'))"
                    DisplayIcon     = "$($key.GetValue('DisplayIcon'))"
                    EstimatedSize   = $key.GetValue('EstimatedSize')
                })
                $key.Close()
            }
            $root.Close()
        }
        catch { }
    }
    $script:UninstallEntries = $entries
    return $entries
}

function Get-EntryIcon($entry, [string[]]$fallbacks) {
    $icon = ConvertTo-WindowsPath ("$($entry.DisplayIcon)" -replace ',\s*-?\d+$', '')
    if (Test-ExistingFile $icon) { return $icon }
    foreach ($fallback in $fallbacks) { if (Test-ExistingFile $fallback) { return $fallback } }
    return $null
}

function Get-EntrySize($entry) {
    if ($entry -and $entry.EstimatedSize) { return [int64]$entry.EstimatedSize * 1024 }
    return $null
}

function Get-GogGames {
    $root = 'HKLM:\SOFTWARE\WOW6432Node\GOG.com\Games'
    if (-not (Test-Path -LiteralPath $root)) { return }

    $galaxyFolder = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths' -ErrorAction SilentlyContinue).client
    $galaxy = $(if ($galaxyFolder) { Join-Path $galaxyFolder 'GalaxyClient.exe' } else { $null })
    if (-not (Test-ExistingFile $galaxy)) { $galaxy = Get-ProtocolHandlerExe 'goggalaxy' }
    if ($galaxy) { $Launchers['gog'] = $galaxy }

    foreach ($key in Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue) {
        $entry = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue
        if (-not $entry -or $entry.dependsOn) { continue }

        $gameId = $(if ($entry.gameID) { "$($entry.gameID)" } else { $key.PSChildName })
        $installPath = ConvertTo-WindowsPath $entry.path
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $exe = "$($entry.exe)".Trim('"')
        if ($exe -and -not [IO.Path]::IsPathRooted($exe)) { $exe = Join-Path $installPath $exe }
        if (-not (Test-ExistingFile $exe)) { $exe = Find-MainExe $installPath 3 }
        if (-not $exe) { continue }

        $name = "$($entry.gameName)".Trim()
        if (-not $name) { $name = Split-Path $installPath -Leaf }

        $icon = Join-Path $installPath "goggame-$gameId.ico"
        if (-not (Test-ExistingFile $icon)) { $icon = $exe }

        New-Game $name 'GOG' $null $exe $icon @{
            Id           = "gog:$gameId"
            Arguments    = $(if ("$($entry.launchParam)".Trim()) { "$($entry.launchParam)".Trim() } else { $null })
            InstallPath  = $installPath
            LauncherPage = $(if ($galaxy) { "goggalaxy://openGameView/$gameId" } else { $null })
        }
    }
}

function Get-UbisoftGames {
    $root = 'HKLM:\SOFTWARE\WOW6432Node\Ubisoft\Launcher'
    $installs = Join-Path $root 'Installs'
    if (-not (Test-Path -LiteralPath $installs)) { return }

    $launcherFolder = (Get-ItemProperty -LiteralPath $root -ErrorAction SilentlyContinue).InstallDir
    $launcher = $null
    foreach ($candidate in 'UbisoftConnect.exe', 'upc.exe') {
        if ($launcherFolder -and (Test-ExistingFile (Join-Path $launcherFolder $candidate))) {
            $launcher = Join-Path $launcherFolder $candidate
            break
        }
    }
    if (-not $launcher) { $launcher = Get-ProtocolHandlerExe 'uplay' }
    if ($launcher) { $Launchers['ubisoft'] = $launcher }

    $uninstall = @{}
    foreach ($entry in Get-UninstallEntries) {
        if ($entry.Key -match '^Uplay Install (\d+)$') { $uninstall[$Matches[1]] = $entry }
    }

    foreach ($key in Get-ChildItem -LiteralPath $installs -ErrorAction SilentlyContinue) {
        $gameId = $key.PSChildName
        $installPath = ConvertTo-WindowsPath (Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue).InstallDir
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $entry = $uninstall[$gameId]
        $name = $(if ($entry -and $entry.DisplayName) { $entry.DisplayName } else { Split-Path $installPath -Leaf })
        $exe = Find-MainExe $installPath 3
        $icon = Get-EntryIcon $entry @($exe, $launcher)

        New-Game $name 'Ubisoft' "uplay://launch/$gameId/0" $exe $icon @{
            Id            = "ubisoft:$gameId"
            InstallPath   = $installPath
            ReportedBytes = Get-EntrySize $entry
            LauncherPage  = $launcher
        }
    }
}

$BattleNetProductCodes = @{
    prometheus = 'Pro'; wow = 'WoW'; wow_classic = 'WoWC'; wow_classic_era = 'WoWC'; diablo3 = 'D3'; fenris = 'Fen'; hs_beta = 'WTCG'
    heroes = 'Hero'; s2 = 'S2'; s1 = 'S1'; w3 = 'W3'; osi = 'OSI'; anbs = 'ANBS'; odin = 'ODIN'; auks = 'AUKS'
    lazarus = 'LAZR'; fore = 'FORE'; zeus = 'ZEUS'; viper = 'VIPR'; wlby = 'WLBY'; gryphon = 'GRY'; rtro = 'RTRO'
}

function Get-BattleNetGames {
    $client = Get-ProtocolHandlerExe 'battlenet'
    foreach ($candidate in "${env:ProgramFiles(x86)}\Battle.net\Battle.net.exe", "$env:ProgramFiles\Battle.net\Battle.net.exe") {
        if (-not $client -and (Test-ExistingFile $candidate)) { $client = $candidate }
    }
    if (-not $client) { return }
    $Launchers['battlenet'] = $client

    foreach ($entry in Get-UninstallEntries) {
        if ($entry.UninstallString -notmatch 'Blizzard Uninstaller') { continue }
        $uid = [regex]::Match($entry.UninstallString, '--uid=([^\s"]+)').Groups[1].Value.ToLowerInvariant()
        if (-not $uid -or $uid -match '^(battle\.net|bna|agent)') { continue }

        $installPath = ConvertTo-WindowsPath $entry.InstallLocation
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $code = $BattleNetProductCodes[$uid]
        if (-not $code) { $code = $BattleNetProductCodes[($uid -replace '_.*$', '')] }
        $exe = Find-MainExe $installPath 3
        $name = $(if ($entry.DisplayName) { $entry.DisplayName } else { Split-Path $installPath -Leaf })

        New-Game $name 'BattleNet' $null $client (Get-EntryIcon $entry @($exe, $client)) @{
            Id            = "battlenet:$uid"
            Arguments     = $(if ($code) { "--exec=`"launch $code`"" } else { $null })
            InstallPath   = $installPath
            ReportedBytes = Get-EntrySize $entry
            LauncherPage  = $(if ($code) { "battlenet://$code" } else { $client })
        }
    }
}

function Read-GzipJson([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    try {
        $gzip = New-Object IO.Compression.GZipStream($stream, [IO.Compression.CompressionMode]::Decompress)
        $reader = New-Object IO.StreamReader($gzip, [Text.Encoding]::UTF8)
        return ($reader.ReadToEnd() | ConvertFrom-Json)
    }
    finally { $stream.Dispose() }
}

function Get-ItchGames {
    $roots = @(Join-Path $env:APPDATA 'itch\apps') + @($Config.ItchFolders | ForEach-Object { Expand-ConfigPath $_ })
    $folders = New-Object System.Collections.Generic.List[string]
    foreach ($root in $roots | Where-Object { $_ } | Select-Object -Unique) {
        if (-not [IO.Directory]::Exists($root)) { continue }
        foreach ($folder in [IO.Directory]::GetDirectories($root)) {
            if ([IO.File]::Exists((Join-Path $folder '.itch\receipt.json.gz'))) { $folders.Add($folder) }
        }
    }
    if ($folders.Count -eq 0) { return }

    $itch = Get-ProtocolHandlerExe 'itch'
    if (-not $itch) {
        $candidate = Join-Path $env:LOCALAPPDATA 'itch\itch.exe'
        if (Test-ExistingFile $candidate) { $itch = $candidate }
    }
    if ($itch) { $Launchers['itch'] = $itch }

    foreach ($folder in $folders) {
        try { $receipt = Read-GzipJson (Join-Path $folder '.itch\receipt.json.gz') } catch { continue }
        $game = $receipt.game
        if ($game.classification -and $game.classification -notin 'game', 'tool') { continue }

        $exe = Find-MainExe $folder 4
        if (-not $exe) { continue }

        $name = "$($game.title)".Trim()
        if (-not $name) { $name = Get-CleanFolderName (Split-Path $folder -Leaf) }

        New-Game $name 'Itch' $null $exe $exe @{
            Id           = $(if ($game.id) { "itch:$($game.id)" } else { 'itch:' + $folder.ToLowerInvariant() })
            InstallPath  = $folder.TrimEnd('\')
            LauncherPage = $(if ($itch -and $game.id) { "itch://games/$($game.id)" } else { $itch })
        }
    }
}

function Get-AmazonGames {
    $launcher = Join-Path $env:LOCALAPPDATA 'Amazon Games\App\Amazon Games.exe'
    if (-not (Test-ExistingFile $launcher)) { $launcher = Get-ProtocolHandlerExe 'amazon-games' }

    $found = $false
    foreach ($entry in Get-UninstallEntries) {
        if ($entry.UninstallString -notmatch 'Amazon Game Remover') { continue }
        $gameId = [regex]::Match($entry.UninstallString, '-p\s+"?([^\s"]+)').Groups[1].Value
        if (-not $gameId) { continue }

        $installPath = ConvertTo-WindowsPath $entry.InstallLocation
        if (-not (Test-FolderHasContent $installPath)) { continue }

        $found = $true
        $exe = Find-MainExe $installPath 3
        $name = $(if ($entry.DisplayName) { $entry.DisplayName } else { Split-Path $installPath -Leaf })
        New-Game $name 'Amazon' "amazon-games://play/$gameId" $exe (Get-EntryIcon $entry @($exe, $launcher)) @{
            Id            = "amazon:$gameId"
            InstallPath   = $installPath
            ReportedBytes = Get-EntrySize $entry
            LauncherPage  = $launcher
        }
    }
    if ($found -and $launcher) { $Launchers['amazon'] = $launcher }
}

function Read-VdfString([byte[]]$bytes, [ref]$position) {
    $start = $position.Value
    while ($position.Value -lt $bytes.Length -and $bytes[$position.Value] -ne 0) { $position.Value++ }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $start, $position.Value - $start)
    $position.Value++
    return $text
}

function Read-VdfMap([byte[]]$bytes, [ref]$position) {
    $map = @{}
    while ($position.Value -lt $bytes.Length) {
        $type = $bytes[$position.Value]
        $position.Value++
        if ($type -eq 8) { break }

        $key = Read-VdfString $bytes $position
        switch ($type) {
            0 { $map[$key] = Read-VdfMap $bytes $position }
            1 { $map[$key] = Read-VdfString $bytes $position }
            2 { $map[$key] = [BitConverter]::ToUInt32($bytes, $position.Value); $position.Value += 4 }
            3 { $map[$key] = [BitConverter]::ToSingle($bytes, $position.Value); $position.Value += 4 }
            7 { $map[$key] = [BitConverter]::ToUInt64($bytes, $position.Value); $position.Value += 8 }
            10 { $map[$key] = [BitConverter]::ToInt64($bytes, $position.Value); $position.Value += 8 }
            default { throw "Unknown binary VDF type $type" }
        }
    }
    return $map
}

function Get-SteamRoot {
    $steamPath = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $steamPath) { return $null }
    return [IO.Path]::GetFullPath($steamPath).TrimEnd('\')
}

function Get-SteamUserIds([string]$steamRoot) {
    $active = (Get-ItemProperty 'HKCU:\Software\Valve\Steam\ActiveProcess' -ErrorAction SilentlyContinue).ActiveUser
    if ($active) { return @("$active") }

    $loginUsers = Join-Path $steamRoot 'config\loginusers.vdf'
    if (Test-ExistingFile $loginUsers) {
        $text = Get-Content -LiteralPath $loginUsers -Raw -Encoding UTF8
        foreach ($match in [regex]::Matches($text, '"(\d{17})"\s*\{([^}]*)\}')) {
            if ($match.Groups[2].Value -match '(?i)"MostRecent"\s+"1"') { return @("$([int64]$match.Groups[1].Value - 76561197960265728)") }
        }
    }

    $userdata = Join-Path $steamRoot 'userdata'
    if (-not [IO.Directory]::Exists($userdata)) { return @() }
    return @([IO.Directory]::GetDirectories($userdata) | ForEach-Object { Split-Path $_ -Leaf } | Where-Object { $_ -match '^\d+$' -and $_ -ne '0' })
}

function Get-SteamShortcutGames {
    $steamRoot = Get-SteamRoot
    if (-not $steamRoot) { return }

    foreach ($userId in Get-SteamUserIds $steamRoot) {
        $file = Join-Path $steamRoot "userdata\$userId\config\shortcuts.vdf"
        if (-not (Test-ExistingFile $file)) { continue }

        $position = 0
        try { $root = Read-VdfMap ([IO.File]::ReadAllBytes($file)) ([ref]$position) } catch { continue }
        $shortcuts = $root['shortcuts']
        if (-not $shortcuts) { continue }

        foreach ($entry in $shortcuts.Values) {
            if ($entry -isnot [hashtable]) { continue }
            if ($entry['tags'] -is [hashtable] -and @($entry['tags'].Values) -contains 'Game Hub') { continue }

            $exe = "$($entry['exe'])".Trim().Trim('"')
            $name = "$($entry['appname'])".Trim()
            if (-not $name -or -not (Test-ExistingFile $exe)) { continue }

            $appId = $entry['appid']
            $url = $null
            if ($appId) {
                $gameId = [Numerics.BigInteger]([uint32]$appId) * 4294967296 + 33554432
                $url = "steam://rungameid/$gameId"
            }

            $icon = "$($entry['icon'])".Trim().Trim('"')
            if (-not (Test-ExistingFile $icon)) { $icon = $exe }
            $arguments = "$($entry['LaunchOptions'])".Trim()
            $lastPlayed = $entry['LastPlayTime']

            New-Game $name 'SteamShortcut' $url $exe $icon @{
                Id           = $(if ($appId) { "steamshortcut:$appId" } else { 'steamshortcut:' + $exe.ToLowerInvariant() })
                Arguments    = $(if ($arguments) { $arguments } else { $null })
                InstallPath  = $(if ($exe -match '(?i)\.exe$') { Get-InstallRoot $exe } else { $null })
                LastPlayed   = $(if ($lastPlayed) { ConvertFrom-UnixTime "$lastPlayed" } else { $null })
                LauncherPage = 'steam://nav/games'
            }
        }
    }
}

function Get-RomFiles([string]$folder, $extensions, [int]$depth) {
    try {
        foreach ($file in [IO.Directory]::EnumerateFiles($folder)) {
            if ($extensions.Contains([IO.Path]::GetExtension($file))) { $file }
        }
        if ($depth -le 0) { return }
        foreach ($child in [IO.Directory]::GetDirectories($folder)) {
            $info = New-Object IO.DirectoryInfo $child
            if ($info.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            Get-RomFiles $child $extensions ($depth - 1)
        }
    }
    catch { }
}

function Get-EmulatorGames {
    foreach ($emulator in @($Config.Emulators)) {
        if (-not $emulator -or -not $emulator.Name -or -not $emulator.Exe) { continue }
        $exe = Expand-ConfigPath $emulator.Exe
        if (-not (Test-ExistingFile $exe)) { continue }
        $Launchers['emulator-' + (ConvertTo-NameKey $emulator.Name)] = $exe

        $template = $(if ("$($emulator.Arguments)".Trim()) { "$($emulator.Arguments)".Trim() } else { '"{rom}"' })
        $extensions = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
        foreach ($extension in @($emulator.Extensions)) {
            if ($extension) { [void]$extensions.Add('.' + "$extension".Trim().TrimStart('*').TrimStart('.')) }
        }
        if ($extensions.Count -eq 0) { continue }

        foreach ($entry in @($emulator.RomFolders)) {
            $folder = Expand-ConfigPath $entry
            if (-not $folder -or -not [IO.Directory]::Exists($folder)) { continue }
            $script:ClaimedFolders.Add($folder)

            foreach ($rom in @(Get-RomFiles $folder $extensions 3)) {
                $baseName = [IO.Path]::GetFileNameWithoutExtension($rom)
                $name = Get-CleanFolderName $baseName
                if (-not $name) { $name = $baseName }

                New-Game $name "Emulator:$($emulator.Name)" $null $exe $exe @{
                    Id            = 'emu:' + $rom.ToLowerInvariant()
                    Arguments     = $template.Replace('{rom}', $rom)
                    ReportedBytes = (New-Object IO.FileInfo $rom).Length
                    LauncherPage  = $exe
                }
            }
        }
    }
}

function Get-ManualGames {
    foreach ($entry in @($Config.ManualGames)) {
        if (-not $entry -or -not $entry.Exe) { continue }
        $exe = Expand-ConfigPath $entry.Exe
        if (-not (Test-ExistingFile $exe)) { continue }

        $name = "$($entry.Name)".Trim()
        if (-not $name) { $name = Get-ScannedGameName $exe (Split-Path $exe -Parent) (Split-Path $exe -Parent) }
        $arguments = "$($entry.Arguments)".Trim()
        $installPath = $(if ($exe -match '(?i)\.exe$') { Get-InstallRoot $exe } else { Split-Path $exe -Parent })
        $protected = @($env:WINDIR, $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:USERPROFILE) | Where-Object { $_ -and (Test-PathInside $_ $installPath) }
        if ($installPath.Length -le 3 -or (Test-PathInside $installPath $env:WINDIR) -or $protected) { $installPath = $null }

        New-Game $name 'Manual' $null $exe $exe @{
            Id          = $(if ($entry.Id) { "$($entry.Id)" } else { 'manual:' + $exe.ToLowerInvariant() })
            Arguments   = $(if ($arguments) { $arguments } else { $null })
            InstallPath = $installPath
        }
    }
}

function Remove-ShadowedGames($games) {
    $scanned = 'Folder', 'Extra', 'Downloads', 'SteamShortcut'
    $launcherExes = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    $nonShortcutKeys = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($game in $games) {
        if ($game.Source -ne 'SteamShortcut') {
            if ($game.Exe) { [void]$nonShortcutKeys.Add($game.Exe) }
            if ($game.InstallPath) { [void]$nonShortcutKeys.Add('path:' + $game.InstallPath.TrimEnd('\')) }
            [void]$nonShortcutKeys.Add('name:' + (ConvertTo-NameKey $game.Name))
        }
        if ($game.Exe -and $game.Source -notin $scanned) { [void]$launcherExes.Add($game.Exe) }
    }

    foreach ($game in $games) {
        if ($game.Source -eq 'SteamShortcut') {
            if ($game.Exe -and $nonShortcutKeys.Contains($game.Exe)) { continue }
            if ($game.InstallPath -and $nonShortcutKeys.Contains('path:' + $game.InstallPath.TrimEnd('\'))) { continue }
            if ($nonShortcutKeys.Contains('name:' + (ConvertTo-NameKey $game.Name))) { continue }
        }
        if ($game.Exe -and $game.Source -in 'Folder', 'Extra', 'Downloads' -and $launcherExes.Contains($game.Exe)) { continue }
        $game
    }
}

function Get-ConfigSet([string]$key) {
    $set = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($value in @($Config.$key)) { if ($value) { [void]$set.Add("$value") } }
    return ,$set
}

function Get-AbsorbedIds {
    $set = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($merge in @($Config.Merges)) {
        if ($merge -and $merge.Keep -and $merge.Absorb -and $merge.Keep -ne $merge.Absorb) { [void]$set.Add("$($merge.Absorb)") }
    }
    return ,$set
}

function Use-Override($game) {
    if (-not $Config.Overrides) { return $game }
    $property = $Config.Overrides.PSObject.Properties[$game.Id]
    if (-not $property -or -not $property.Value) { return $game }

    $override = $property.Value
    $copy = $game.PSObject.Copy()
    if ("$($override.Name)".Trim()) { $copy.Name = "$($override.Name)".Trim() }

    $exe = Expand-ConfigPath $override.Exe
    if ($exe -and (Test-ExistingFile $exe)) {
        if ($copy.Icon -eq $copy.Exe -or -not $copy.Icon) { $copy.Icon = $exe }
        $copy.Exe = $exe
        $copy.Url = $null
    }
    if ($override.PSObject.Properties['Arguments'] -and $null -ne $override.Arguments) {
        $arguments = "$($override.Arguments)".Trim()
        $copy.Arguments = $(if ($arguments) { $arguments } else { $null })
        if ($arguments -and $copy.Exe) { $copy.Url = $null }
    }
    return $copy
}

function Add-SkippedShortcut($game, [string]$reason) {
    $script:SkippedShortcuts.Add([pscustomobject][ordered]@{ Id = $game.Id; Name = $game.Name; Source = $game.Source; Reason = $reason })
}

function New-ScannedGame([string]$exe, [string]$root, [string]$stopAt, [string]$source, [string]$installPath) {
    $name = Get-ScannedGameName $exe $root $stopAt
    if (-not $name) { return }
    if (-not $installPath) { $installPath = Get-InstallRoot $exe }
    New-Game $name $source $null $exe $exe @{ InstallPath = $installPath }
}

function Get-FolderGames {
    foreach ($entry in @($Config.LibraryFolders)) {
        $library = [Environment]::ExpandEnvironmentVariables($entry)
        if (-not (Test-Path -LiteralPath $library)) { continue }
        foreach ($folder in [IO.Directory]::GetDirectories($library)) {
            foreach ($found in @(Find-GameRoots $folder 5 $false)) {
                New-ScannedGame (Select-MainExe $found.Exes $found.Root $library) $found.Root $library 'Folder' $folder
            }
        }
    }

    foreach ($entry in @($Config.ExtraGames)) {
        $path = [Environment]::ExpandEnvironmentVariables($entry)
        if (Test-ExistingFile $path) {
            New-ScannedGame $path (Split-Path $path -Parent) (Split-Path $path -Parent) 'Extra'
        }
        elseif (Test-Path -LiteralPath $path -PathType Container) {
            foreach ($found in @(Find-GameRoots $path 6 $false)) {
                New-ScannedGame (Select-MainExe $found.Exes $found.Root (Split-Path $path -Parent)) $found.Root (Split-Path $path -Parent) 'Extra'
            }
        }
    }

    foreach ($entry in @($Config.SearchFolders)) {
        $folder = [Environment]::ExpandEnvironmentVariables($entry)
        if (-not (Test-Path -LiteralPath $folder)) { continue }
        foreach ($found in @(Find-GameRoots $folder 8 $true)) {
            New-ScannedGame (Select-MainExe $found.Exes $found.Root $folder) $found.Root $folder 'Downloads'
        }
    }
}

function Get-ExistingStartMenuIndex {
    $index = [pscustomobject]@{
        Names   = New-Object System.Collections.Generic.HashSet[string]
        Targets = New-Object System.Collections.Generic.HashSet[string]
    }

    foreach ($programs in @($UserPrograms, $CommonPrograms)) {
        foreach ($file in Get-ChildItem -LiteralPath $programs -Recurse -Include '*.lnk', '*.url' -ErrorAction SilentlyContinue) {
            if ($file.FullName.StartsWith($ShortcutFolder + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
            if ($file.FullName -eq $SyncShortcutPath) { continue }

            if ($file.Extension -eq '.url') {
                $match = Select-String -LiteralPath $file.FullName -Pattern '^URL=(.+)$' -ErrorAction SilentlyContinue | Select-Object -First 1
                if (-not $match) { continue }
                [void]$index.Targets.Add($match.Matches[0].Groups[1].Value.Trim().ToLowerInvariant())
                [void]$index.Names.Add((ConvertTo-NameKey $file.BaseName))
                continue
            }

            try { $link = $Shell.CreateShortcut($file.FullName) } catch { continue }
            $target = $link.TargetPath
            if ($target -and -not (Test-Path -LiteralPath $target)) { continue }
            if ($target) { [void]$index.Targets.Add($target.ToLowerInvariant()) }
            if ($link.Arguments -match '^\S+://') { [void]$index.Targets.Add($link.Arguments.Trim().ToLowerInvariant()) }
            elseif ($target -and $link.Arguments) { [void]$index.Targets.Add(("$target " + $link.Arguments.Trim()).ToLowerInvariant()) }
            [void]$index.Names.Add((ConvertTo-NameKey $file.BaseName))
        }
    }

    try {
        foreach ($app in Get-StartApps) {
            if ($app.AppID -notmatch '^[^\\!]+_[0-9a-z]{13}![^\\]+$') { continue }
            [void]$index.Targets.Add(('shell:AppsFolder\' + $app.AppID).ToLowerInvariant())
            [void]$index.Names.Add((ConvertTo-NameKey $app.Name))
        }
    }
    catch { }
    return $index
}

function Get-IgnoredNames {
    $ignored = New-Object System.Collections.Generic.HashSet[string]
    foreach ($name in @($Config.IgnoreNames)) { if ($name) { [void]$ignored.Add((ConvertTo-NameKey $name)) } }
    return ,$ignored
}

function Get-LaunchTargets($game) {
    $targets = @()
    if ($game.Url) { $targets += $game.Url.ToLowerInvariant() }
    if ($game.Exe) { $targets += ("$($game.Exe) $($game.Arguments)").Trim().ToLowerInvariant() }
    if ($targets.Count -eq 0) { $targets += $game.Id.ToLowerInvariant() }
    return $targets
}

function Select-LibraryGames($games) {
    $seenTargets = New-Object System.Collections.Generic.HashSet[string]

    foreach ($game in $games) {
        $nameKey = ConvertTo-NameKey $game.Name
        if (-not $nameKey) { continue }

        $targets = Get-LaunchTargets $game
        if (@($targets | Where-Object { $seenTargets.Contains($_) }).Count -gt 0) { continue }
        foreach ($target in $targets) { [void]$seenTargets.Add($target) }
        $game
    }
}

function Merge-InstallerEntries($games) {
    $installed = @($games | Where-Object { $_.Status -eq 'installed' -and $_.InstallPath })
    foreach ($game in $games) {
        if ($game.Status -ne 'installer') {
            $game
            continue
        }

        $downloadFolder = @($game.Parts)[0].Path
        $owner = $installed | Where-Object { (Test-PathInside $_.InstallPath $downloadFolder) -or (Test-PathInside $downloadFolder $_.InstallPath) } | Select-Object -First 1
        if (-not $owner) {
            $game
            continue
        }

        foreach ($field in 'SteamAppId', 'LastPlayed', 'PlaytimeMinutes', 'ReportedBytes') {
            if (-not $owner.$field -and $game.$field) { $owner.$field = $game.$field }
        }
    }
}

function Split-SharedInstallFolders($games) {
    $groups = $games | Where-Object { $_.Status -eq 'installed' -and $_.InstallPath -and $_.Exe } | Group-Object { $_.InstallPath.ToLowerInvariant() } | Where-Object Count -gt 1
    foreach ($group in $groups) {
        foreach ($game in $group.Group) {
            $shared = $game.InstallPath.TrimEnd('\')
            if (-not $game.Exe.StartsWith($shared + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
            $segments = $game.Exe.Substring($shared.Length + 1).Split('\')
            if ($segments.Count -gt 1) { $game.InstallPath = Join-Path $shared $segments[0] }
        }
    }
    return $games
}

function Get-LeftoverFolders($games) {
    $claimed = @($ClaimedFolders)
    foreach ($game in $games) {
        if ($game.InstallPath) { $claimed += $game.InstallPath }
        foreach ($part in @($game.Parts)) { if ($part) { $claimed += $part.Path } }
    }

    foreach ($library in Get-LibraryRoots) {
        if (-not (Test-Path -LiteralPath $library)) { continue }
        foreach ($folder in [IO.Directory]::GetDirectories($library)) {
            $isClaimed = @($claimed | Where-Object { (Test-PathInside $_ $folder) -or (Test-PathInside $folder $_) }).Count -gt 0
            if ($isClaimed) { continue }

            $name = Get-CleanFolderName (Split-Path $folder -Leaf)
            if (-not $name) { $name = Split-Path $folder -Leaf }
            New-Game $name 'Folder' $null $null $null @{
                Id          = 'leftover:' + $folder.ToLowerInvariant()
                Status      = 'leftover'
                InstallPath = $folder
            }
        }
    }
}

function Select-ShortcutGames($library) {
    $seenNames = New-Object System.Collections.Generic.HashSet[string]
    $existing = $null
    if ($Config.SkipGamesAlreadyInStartMenu) { $existing = Get-ExistingStartMenuIndex }
    $ignoredNames = Get-IgnoredNames
    $ignoredIds = Get-ConfigSet 'IgnoreIds'
    $absorbedIds = Get-AbsorbedIds

    foreach ($entry in $library) {
        if ($entry.Status -ne 'installed') { continue }
        if ($ignoredIds.Contains($entry.Id) -or $ignoredNames.Contains((ConvertTo-NameKey $entry.Name))) {
            Add-SkippedShortcut $entry 'hidden'
            continue
        }
        if ($absorbedIds.Contains($entry.Id)) {
            Add-SkippedShortcut $entry 'merged'
            continue
        }

        $game = Use-Override $entry
        $nameKey = ConvertTo-NameKey $game.Name
        if ($ignoredNames.Contains($nameKey)) {
            Add-SkippedShortcut $game 'hidden'
            continue
        }
        if (-not $seenNames.Add($nameKey)) {
            Add-SkippedShortcut $game 'duplicate name'
            continue
        }

        if ($existing) {
            if ($existing.Names.Contains($nameKey) -or @(Get-LaunchTargets $game | Where-Object { $existing.Targets.Contains($_) }).Count -gt 0) {
                Add-SkippedShortcut $game 'already in Start Menu'
                continue
            }
        }
        $game
    }
}

function Export-Library($library) {
    $document = [ordered]@{
        GeneratedAt = (Get-Date).ToUniversalTime().ToString('o')
        ConfigStamp = $ConfigStamp
        Launchers   = $Launchers
        Games       = @($library)
    }
    $json = ConvertTo-Json -InputObject $document -Depth 6 -Compress
    [IO.File]::WriteAllText($LibraryPath, $json, (New-Object Text.UTF8Encoding $false))
}

function Set-UrlShortcut([string]$path, [string]$url, [string]$icon, [bool]$dryRun) {
    $lines = @('[InternetShortcut]', "URL=$url")
    if ($icon -and $icon -notmatch '[^\x20-\x7E]') {
        $lines += "IconFile=$icon"
        $lines += 'IconIndex=0'
    }
    $content = ($lines -join "`r`n") + "`r`n"

    $exists = Test-Path -LiteralPath $path
    if ($exists -and [IO.File]::ReadAllText($path) -eq $content) { return 'keep' }
    if (-not $dryRun) { [IO.File]::WriteAllText($path, $content, [Text.Encoding]::ASCII) }
    return $(if ($exists) { 'update' } else { 'add' })
}

function Set-ExeShortcut([string]$path, [string]$exe, [string]$arguments, [string]$icon, [bool]$dryRun) {
    if (-not $icon) { $icon = $exe }
    $workingDirectory = Split-Path $exe -Parent
    $iconLocation = "$icon,0"
    $exists = Test-Path -LiteralPath $path
    $link = $Shell.CreateShortcut($path)

    if ($exists -and $link.TargetPath -eq $exe -and $link.Arguments -eq $arguments -and $link.WorkingDirectory -eq $workingDirectory -and $link.IconLocation -eq $iconLocation) { return 'keep' }
    if (-not $dryRun) {
        $link.TargetPath = $exe
        $link.Arguments = $arguments
        $link.WorkingDirectory = $workingDirectory
        $link.IconLocation = $iconLocation
        $link.Save()
    }
    return $(if ($exists) { 'update' } else { 'add' })
}

function Get-ShortcutIconMap {
    $map = @{}
    if (-not (Test-ExistingFile $ShortcutIconMapPath)) { return $map }
    try {
        $json = Get-Content -LiteralPath $ShortcutIconMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($property in $json.PSObject.Properties) {
            if (Test-ExistingFile "$($property.Value)") { $map[$property.Name] = "$($property.Value)" }
        }
    }
    catch { }
    return $map
}

function Sync-Shortcuts($games, [bool]$dryRun) {
    $iconMap = Get-ShortcutIconMap
    $previous = Read-ManagedShortcuts
    $current = New-Object System.Collections.Generic.List[string]
    $plan = New-Object System.Collections.Generic.List[object]

    foreach ($game in $games | Sort-Object Name) {
        $baseName = ConvertTo-FileName $game.Name
        if (-not $baseName) { continue }

        $icon = $(if ($iconMap.ContainsKey($game.Id)) { $iconMap[$game.Id] } else { $game.Icon })
        if ($game.Url -and $game.Url -notmatch '^shell:') {
            $fileName = "$baseName.url"
            $target = $game.Url
            $action = Set-UrlShortcut (Join-Path $ShortcutFolder $fileName) $game.Url $icon $dryRun
        }
        elseif ($game.Url) {
            $fileName = "$baseName.lnk"
            $target = $game.Url
            $shortcutIcon = $(if ($icon -match '(?i)\.(exe|ico|dll)$') { $icon } else { $null })
            $action = Set-ExeShortcut (Join-Path $ShortcutFolder $fileName) (Join-Path $env:WINDIR 'explorer.exe') $game.Url $shortcutIcon $dryRun
        }
        else {
            $fileName = "$baseName.lnk"
            $target = "$($game.Exe) $($game.Arguments)".Trim()
            $action = Set-ExeShortcut (Join-Path $ShortcutFolder $fileName) $game.Exe $game.Arguments $icon $dryRun
        }

        if ($current.Contains($fileName)) { continue }
        $current.Add($fileName)
        $plan.Add([pscustomobject][ordered]@{ Id = $game.Id; Name = $game.Name; File = $fileName; Action = $action; Source = $game.Source; Target = $target })
    }

    foreach ($fileName in $previous) {
        if ($current.Contains($fileName)) { continue }
        if (-not $dryRun) { Remove-ManagedShortcut $fileName }
        $plan.Add([pscustomobject][ordered]@{ Id = $null; Name = [IO.Path]::GetFileNameWithoutExtension($fileName); File = $fileName; Action = 'remove'; Source = $null; Target = $null })
    }

    if (-not $dryRun) { Save-ManagedShortcuts $current.ToArray() }
    return $plan
}

function Write-ShortcutPreview($plan) {
    $document = [ordered]@{
        GeneratedAt = (Get-Date).ToUniversalTime().ToString('o')
        Folder      = $ShortcutFolder
        Items       = @($plan)
        Skipped     = $SkippedShortcuts.ToArray()
    }
    $json = ConvertTo-Json -InputObject $document -Depth 5 -Compress
    [IO.File]::WriteAllText($PreviewJson, $json, (New-Object Text.UTF8Encoding $false))
}

function Read-ManagedShortcuts {
    if (-not (Test-Path -LiteralPath $StatePath)) { return @() }
    try { return @(Get-Content -LiteralPath $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { return @() }
}

function Save-ManagedShortcuts([string[]]$names) {
    $json = ConvertTo-Json -InputObject @($names | Sort-Object)
    [IO.File]::WriteAllText($StatePath, $json, (New-Object Text.UTF8Encoding $false))
}

function Remove-ManagedShortcut([string]$fileName) {
    $path = Join-Path $ShortcutFolder $fileName
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

function Invoke-Sync {
    $started = Get-Date
    $allGames = @()
    $collectors = 'Get-ManualGames', 'Get-SteamGames', 'Get-EpicGames', 'Get-HydraGames', 'Get-GogGames', 'Get-UbisoftGames', 'Get-BattleNetGames', 'Get-ItchGames', 'Get-AmazonGames',
        'Get-RiotGames', 'Get-XboxGames', 'Get-HoYoPlayGames', 'Get-EAGames', 'Get-RockstarGames', 'Get-EmulatorGames', 'Get-FolderGames', 'Get-SteamShortcutGames'
    foreach ($collector in $collectors) {
        try { $allGames += @(& $collector) }
        catch { Write-Status "$collector failed: $($_.Exception.Message)" }
    }
    $allGames = @(Remove-ShadowedGames $allGames)

    $library = @(Split-SharedInstallFolders @(Merge-InstallerEntries @(Select-LibraryGames $allGames)))
    $library += @(Get-LeftoverFolders $library)
    $games = @(Select-ShortcutGames $library)

    if ($PreviewJson) {
        Write-ShortcutPreview @(Sync-Shortcuts $games $true)
        Write-Status ("Shortcut preview written to {0}" -f $PreviewJson)
        return
    }

    if ($Preview) {
        $shortcutIds = New-Object System.Collections.Generic.HashSet[string]
        foreach ($game in $games) { [void]$shortcutIds.Add($game.Id) }
        foreach ($game in $allGames | Sort-Object Source, Name) {
            $mark = if ($shortcutIds.Contains($game.Id)) { '+' } else { ' ' }
            Write-Status ("{0} [{1}] {2}  ->  {3}" -f $mark, $game.Source, $game.Name, $(if ($game.Url) { $game.Url } else { "$($game.Exe) $($game.Arguments)".Trim() }))
        }
        foreach ($game in $library | Where-Object Status -ne 'installed') {
            Write-Status ("  [{0}] {1} ({2})  ->  {3}" -f $game.Source, $game.Name, $game.Status, (@($game.InstallPath) + @($game.Parts | ForEach-Object { $_.Path }) | Where-Object { $_ }) -join ' | ')
        }
        Write-Status ("{0} found, {1} in library, {2} would get a shortcut (+)" -f $allGames.Count, $library.Count, $games.Count)
        return
    }

    Export-Library $library

    if (-not (Test-Path -LiteralPath $ShortcutFolder)){ New-Item -ItemType Directory -Path $ShortcutFolder | Out-Null }

    $plan = @(Sync-Shortcuts $games $false)
    $changed = 0
    $removed = 0
    foreach ($item in $plan) {
        if ($item.Action -in 'add', 'update') {
            $changed++
            Write-Status ("+ {0}  [{1}]" -f [IO.Path]::GetFileNameWithoutExtension($item.File), $item.Source)
        }
        elseif ($item.Action -eq 'remove') {
            $removed++
            Write-Status "- $($item.File)"
        }
    }

    $bySource = ($games | Group-Object Source | ForEach-Object { "$($_.Name) $($_.Count)" }) -join ', '
    Write-Status ("Found {0} games, {6} in hub library ({1} new to Start Menu: {2}). Written {3}, removed {4}. {5:N1}s" -f $allGames.Count, $games.Count, $bySource, $changed, $removed, ((Get-Date) - $started).TotalSeconds, $library.Count)
    Write-Status "Shortcuts folder: $ShortcutFolder"
}

function Install-Sync {
    $link = $Shell.CreateShortcut($SyncShortcutPath)
    $link.TargetPath = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    $link.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Pause"
    $link.WorkingDirectory = $AppFolder
    $link.IconLocation = "$env:WINDIR\System32\shell32.dll,238"
    $link.Description = 'Add shortcuts for installed games to the Start Menu'
    $link.Save()
    Write-Status "Created $SyncShortcutPath"

    $user = "$env:USERDOMAIN\$env:USERNAME"
    $action = New-ScheduledTaskAction -Execute "$env:WINDIR\System32\conhost.exe" -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" -Quiet" -WorkingDirectory $AppFolder
    $atLogon = New-ScheduledTaskTrigger -AtLogOn -User $user
    $hourly = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Hours 1)
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
    $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $atLogon, $hourly -Settings $settings -Principal $principal -Force | Out-Null
    Write-Status "Registered scheduled task '$TaskName' (at logon + hourly)"
}

function Uninstall-Sync {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $SyncShortcutPath) { Remove-Item -LiteralPath $SyncShortcutPath -Force }
    foreach ($fileName in Read-ManagedShortcuts) { Remove-ManagedShortcut $fileName }
    Save-ManagedShortcuts @()
    if ((Test-Path -LiteralPath $ShortcutFolder) -and -not (Get-ChildItem -LiteralPath $ShortcutFolder -Force)) { Remove-Item -LiteralPath $ShortcutFolder }
    Write-Status 'Removed scheduled task, sync shortcut and all managed game shortcuts'
}

try {
    if ($Uninstall) { Uninstall-Sync }
    else {
        if ($Install) { Install-Sync }
        Invoke-Sync
    }
}
catch {
    Write-Status "Error: $($_.Exception.Message)"
}
finally {
    if (-not $PreviewJson) {
        $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        [IO.File]::WriteAllLines($LogPath, [string[]](@("[$stamp]") + $Log.ToArray()), (New-Object Text.UTF8Encoding $false))
    }
    if ($Pause) { Read-Host 'Press Enter to close' | Out-Null }
}
