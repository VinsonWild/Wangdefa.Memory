# add-copyright.ps1
$header = @'
// Copyright © 2025-2026 VinsonWild (wangdefa)
// Licensed under the Apache License, Version 2.0.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// See the LICENSE file in the repository root for full text.
'@

$extensions = @("*.cs", "*.ts")
$excludeDirs = @("bin", "obj", "node_modules", "lib", "Test", "ConsoleDemo")

foreach ($ext in $extensions) {
    Get-ChildItem -Recurse -Filter $ext | ForEach-Object {
        $exclude = $false
        foreach ($dir in $excludeDirs) {
            if ($_.FullName -match "\\$dir\\") {
                $exclude = $true
                break
            }
        }
        if ($exclude) { return }

        $content = Get-Content $_.FullName -Raw
        if ($content -notmatch "Copyright") {
            Write-Host "Adding copyright to: $($_.Name)"
            "$header`n`n$content" | Set-Content $_.FullName -NoNewline
        }
    }
}

Write-Host "Done."