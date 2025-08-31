# Script para limpiar procesos MCP y Claude
Write-Host "🧹 Limpiando procesos..." -ForegroundColor Yellow

# Terminar procesos relacionados
$processes = @("claude.exe", "dotnet.exe", "ElasticsearchMcpServer.exe")

foreach ($process in $processes) {
    try {
        $result = taskkill /F /IM $process 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Terminados procesos de $process" -ForegroundColor Green
        }
    }
    catch {
        # Ignorar si no hay procesos
    }
}

# Verificar que no queden procesos
$remaining = Get-Process | Where-Object {$_.ProcessName -like "*claude*" -or $_.ProcessName -like "*dotnet*" -or $_.ProcessName -like "*ElasticsearchMcp*"} 2>$null
if ($remaining) {
    Write-Host "⚠️ Procesos restantes:" -ForegroundColor Yellow
    $remaining | Format-Table ProcessName,Id -AutoSize
} else {
    Write-Host "🎯 Sistema completamente limpio" -ForegroundColor Green
}
