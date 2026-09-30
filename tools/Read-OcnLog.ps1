param([int]$Last = 80, [string]$Since = '')
$ErrorActionPreference = 'Stop'
$path = Join-Path $env:APPDATA 'XIVLauncher\dalamud.log'
$stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
$reader = [IO.StreamReader]::new($stream)
$lines = [Collections.Generic.Queue[string]]::new()
$withinRange = !$Since
try {
    while ($null -ne ($line = $reader.ReadLine())) {
        if ($line -match '^\d{4}-') { $withinRange = !$Since -or $line.Substring(0, 19) -ge $Since }
        if (!$withinRange) { continue }
        if ($line -notmatch '\[(OCNFarmer|OCBFR)\]|Finished loading (OCNFarmer|OCBFR)|Unloading (OCNFarmer|OCBFR)|Finished unloading (OCNFarmer|OCBFR)|NorthIslandChestPlugin' -or
            $line -match 'TROUBLESHOOTING:|LASTEXCEPTION:') { continue }
        $lines.Enqueue($line)
        if ($lines.Count -gt $Last) { $null = $lines.Dequeue() }
    }
} finally { $reader.Dispose() }
$lines.ToArray()
