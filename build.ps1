$ErrorActionPreference = 'Stop'
function Get-CodeSignCert {
    $certs = @(Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue)
    foreach ($cert in $certs) {
        if (-not $cert.HasPrivateKey) { continue }
        $text = ($cert.Extensions | ForEach-Object { $_.Format($false) }) -join ' '
        if ($text -match 'U1807@' -or $cert.Subject -match 'U1807') { return $cert }
    }
    throw 'U1807 code-signing certificate with a private key was not found.'
}
function Sign-Built([string]$path) {
    $cert = Get-CodeSignCert
    $signed = $null
    foreach ($server in @('http://timestamp.digicert.com', 'http://timestamp.sectigo.com')) {
        try {
            $signed = Set-AuthenticodeSignature -FilePath $path -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $server
            if ($signed.SignerCertificate -and $signed.SignerCertificate.Thumbprint -eq $cert.Thumbprint -and $signed.TimeStamperCertificate) { break }
        } catch { $signed = $null }
    }
    if (-not $signed -or -not $signed.TimeStamperCertificate) {
        $signed = Set-AuthenticodeSignature -FilePath $path -Certificate $cert -HashAlgorithm SHA256
    }
    if (-not $signed.SignerCertificate -or $signed.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
        throw ("Signature failed for " + $path + ": " + $signed.Status + " " + $signed.StatusMessage)
    }
    Write-Output ("signed " + (Split-Path -Leaf $path) + " " + $signed.Status + " " + $signed.SignerCertificate.Thumbprint)
}
function Publish-Staged([string]$fresh, [string]$dest) {
    if (-not (Test-Path -LiteralPath $dest)) {
        Move-Item -LiteralPath $fresh -Destination $dest
        return
    }
    try {
        [System.IO.File]::Replace($fresh, $dest, $null)
        return
    } catch {
        $old = [System.IO.Path]::ChangeExtension($dest, '.old.exe')
        if (Test-Path -LiteralPath $old) {
            try { Remove-Item -LiteralPath $old -Force } catch { }
        }
        Rename-Item -LiteralPath $dest -NewName (Split-Path -Leaf $old)
        Move-Item -LiteralPath $fresh -Destination $dest
    }
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler not found.' }
Push-Location $PSScriptRoot
try {
    $versionFile = Join-Path $PSScriptRoot 'build.version'
    $version = 11
    if (Test-Path -LiteralPath $versionFile) {
        $parsed = 0
        if ([int]::TryParse((Get-Content -LiteralPath $versionFile -Raw).Trim(), [ref]$parsed) -and $parsed -ge 11) {
            $version = $parsed
        }
    }
    $stamp = 'v' + $version
    New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'config') | Out-Null

    $launcherStaged = Join-Path $PSScriptRoot ("Launcher." + $stamp + ".exe")
    & $compiler /nologo /codepage:65001 /target:winexe /platform:anycpu /win32manifest:app.manifest /win32icon:CorporateApps.ico /resource:CorporateApps.ico,CorporateApps.ico /out:$launcherStaged /r:System.Drawing.dll /r:System.Windows.Forms.dll Launcher.cs Shared.cs Theme.cs
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed.' }
    Publish-Staged $launcherStaged (Join-Path $PSScriptRoot 'Launcher.exe')

    $configuratorStaged = Join-Path $PSScriptRoot ("config\Configurator." + $stamp + ".exe")
    & $compiler /nologo /codepage:65001 /target:winexe /platform:anycpu /win32manifest:app.manifest /win32icon:Configurator.ico /resource:Configurator.ico,Configurator.ico /out:$configuratorStaged /r:System.Drawing.dll /r:System.Windows.Forms.dll Configurator.cs Shared.cs Theme.cs DomainPicker.cs
    if ($LASTEXITCODE -ne 0) { throw 'Configurator build failed.' }
    Publish-Staged $configuratorStaged (Join-Path $PSScriptRoot 'config\Configurator.exe')

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Configurator.ico') -Destination (Join-Path $PSScriptRoot 'config\Configurator.ico') -Force
    $stale = Join-Path $PSScriptRoot 'Configurator.exe'
    if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
    Sign-Built (Join-Path $PSScriptRoot 'Launcher.exe')
    Sign-Built (Join-Path $PSScriptRoot 'config\Configurator.exe')
    Set-Content -LiteralPath $versionFile -Value ($version + 1) -Encoding ascii
    Write-Output ("Built " + $stamp + ". Next build is v" + ($version + 1) + ".")
} finally { Pop-Location }
