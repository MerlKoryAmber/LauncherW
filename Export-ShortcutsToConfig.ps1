# Builds <app> XML fragments from .lnk files for pasting into config\apps.config
param(
    [Parameter(Mandatory = $true)]
    [string]$Folder,

    [string]$OutFile = '',

    [switch]$IncludeIcon,
    [switch]$IncludeArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Folder)) {
    throw "Folder not found: $Folder"
}

function Escape-Xml([string]$text) {
    if ($null -eq $text) { return '' }
    return ($text -replace '&', '&amp;' -replace '<', '&lt;' -replace '>', '&gt;' -replace '"', '&quot;')
}

function Icon-Path([string]$location) {
    if ([string]::IsNullOrWhiteSpace($location)) { return $null }
    $path = $location.Trim()
    $comma = $path.LastIndexOf(',')
    if ($comma -gt 0) {
        $tail = $path.Substring($comma + 1).Trim()
        $index = 0
        if ([int]::TryParse($tail, [ref]$index)) {
            $path = $path.Substring(0, $comma).Trim()
        }
    }
    if ([string]::IsNullOrWhiteSpace($path)) { return $null }
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return $path
}

$shell = New-Object -ComObject WScript.Shell
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('<!-- paste these <app> blocks into config\apps.config before </apps> -->')

Get-ChildItem -LiteralPath $Folder -Filter *.lnk -File | Sort-Object Name | ForEach-Object {
    $lnk = $shell.CreateShortcut($_.FullName)
    $name = [IO.Path]::GetFileNameWithoutExtension($_.Name)
    $target = $lnk.TargetPath
    if ([string]::IsNullOrWhiteSpace($target)) {
        Write-Warning ("Skip (empty target): " + $_.Name)
        return
    }

    $lines.Add('  <app>')
    $lines.Add('    <name>' + (Escape-Xml $name) + '</name>')
    $lines.Add('    <path>' + (Escape-Xml $target) + '</path>')

    if ($IncludeArgs -and -not [string]::IsNullOrWhiteSpace($lnk.Arguments)) {
        $lines.Add('    <args>' + (Escape-Xml $lnk.Arguments) + '</args>')
    }

    if ($IncludeIcon) {
        $icon = Icon-Path $lnk.IconLocation
        if (-not $icon -and (Test-Path -LiteralPath $target)) { $icon = $target }
        if ($icon) {
            $lines.Add('    <icon>' + (Escape-Xml $icon) + '</icon>')
        }
    }

    $lines.Add('  </app>')
}

$text = ($lines -join [Environment]::NewLine) + [Environment]::NewLine

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path $Folder 'apps.from-shortcuts.xml'
}

[IO.File]::WriteAllText($OutFile, $text, [Text.UTF8Encoding]::new($false))
Write-Output $OutFile
Write-Output $text
