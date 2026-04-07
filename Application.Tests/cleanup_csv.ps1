#!/usr/bin/env pwsh
# Clean CSV: remove (inferred) suffix and delete Exception: Exception rows

$csv_path = Join-Path $PSScriptRoot 'UTCID_Matrix_All_DecisionTable.csv'

# Read CSV
$data = Import-Csv $csv_path

# Count before
$before_count = @($data).Count
$before_generic = @($data | Where-Object { $_.GiaTri -eq 'Exception: Exception' }).Count

Write-Host "Before cleanup:"
Write-Host "  - Total rows: $before_count"
Write-Host "  - Exception: Exception rows: $before_generic"

# Clean data
$cleaned = @($data | ForEach-Object {
    # Remove (inferred) suffix
    $_.GiaTri = $_.GiaTri -replace ' \(inferred\)$', ''
    $_
} | Where-Object { $_.GiaTri -ne 'Exception: Exception' })

# Write back
$cleaned | Export-Csv -Path $csv_path -NoTypeInformation -Encoding UTF8 -Force

# Verify
$verify_data = Import-Csv $csv_path
$after_count = @($verify_data).Count
$after_generic = @($verify_data | Where-Object { $_.GiaTri -eq 'Exception: Exception' }).Count

Write-Host ""
Write-Host "After cleanup:"
Write-Host "  - Total rows: $after_count (removed $($before_count - $after_count) rows)"
Write-Host "  - Exception: Exception rows: $after_generic"
Write-Host ""
Write-Host "✓ Cleanup completed successfully!"
