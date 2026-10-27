$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot 'SmaComplaints')
try {
    & dotnet build -p:UseAppHost=false
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Open http://127.0.0.1:5079 and select a preview role. Press Ctrl+C to stop.'
    & dotnet '.\bin\Debug\net10.0\SmaComplaints.dll' --environment Development --urls 'http://127.0.0.1:5079'
}
finally { Pop-Location }
