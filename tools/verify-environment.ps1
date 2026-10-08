$ErrorActionPreference = 'Stop'

Write-Host '== AI Messenger PC environment =='
Write-Host ("OS: " + [Environment]::OSVersion.VersionString)
Write-Host ("dotnet: " + (dotnet --version))
Write-Host ("git: " + (git --version))
Write-Host ("node: " + (node --version))
Write-Host ("openclaw: " + (openclaw.cmd --version))

$hermes = Join-Path $env:LOCALAPPDATA 'hermes\bin\hermes.cmd'
if (Test-Path $hermes) {
    Write-Host ("hermes: " + (($hermes | ForEach-Object { & $_ --version }) | Select-Object -First 1))
} else {
    Write-Warning 'Hermes launcher not found'
}

if (Test-Path 'C:\AI\tools\FlaUInspect') {
    Write-Host 'FlaUInspect: present'
}

if (Test-Path 'C:\AI\tools\downloads\AccessibilityInsights.msi') {
    Write-Host 'Accessibility Insights installer: present'
}

if (Test-Path 'C:\AI\tools\downloads\OpenClawCompanion-Setup-x64.exe') {
    Write-Host 'OpenClaw Hub installer: present'
}
