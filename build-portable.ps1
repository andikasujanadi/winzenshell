param(
    [string]$Version = "0.5.0"
)

$ErrorActionPreference = "Stop"

Write-Host "Stopping running WinZenShell processes..."
Get-Process WinZenShell -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

Write-Host "Publishing standard release to publish\WinZenShell..."
dotnet publish src\WinZenShell\WinZenShell.csproj -c Release -r win-x64 --self-contained true -o publish\WinZenShell
Copy-Item -Path src\WinZenShell\WebUI\* -Destination publish\WinZenShell\WebUI -Recurse -Force
Copy-Item -Path Widgets\* -Destination publish\WinZenShell\Widgets -Recurse -Force

$portableDir = "publish\WinZenShell-Portable"
if (Test-Path $portableDir) {
    Remove-Item -Path $portableDir -Recurse -Force
}

Write-Host "Publishing single-file portable executable to $portableDir..."
dotnet publish src\WinZenShell\WinZenShell.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $portableDir

Copy-Item -Path src\WinZenShell\WebUI\* -Destination "$portableDir\WebUI" -Recurse -Force
Copy-Item -Path Widgets\* -Destination "$portableDir\Widgets" -Recurse -Force

$zipPath = "publish\WinZenShell-v$Version-win-x64-Portable.zip"
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Write-Host "Creating portable archive $zipPath..."
Compress-Archive -Path "$portableDir\*" -DestinationPath $zipPath -Force
Write-Host "Done! Portable executable: $portableDir\WinZenShell.exe | Archive: $zipPath"
