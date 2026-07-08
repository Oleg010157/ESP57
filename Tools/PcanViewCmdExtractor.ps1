param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$InputPath,

    [Parameter(Mandatory = $false, Position = 1)]
    [string]$OutputPath,

    [Parameter(Mandatory = $false, Position = 2)]
    [ValidateSet('Fixed','Tsv')]
    [string]$Format = 'Fixed'
)

if ([string]::IsNullOrWhiteSpace($InputPath)) {
    $InputPath = Read-Host "InputPath (PCAN-View trace file)"
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Input file not found: $InputPath"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "pcan_log.txt"
}

# PCAN-View trace line example:
#  22)      3125.3  Rx         0002  8  06 00 00 00 05 0D FA 00
# We extract DATA[0] and DATA[1] (hex), interpret as little-endian ushort.
# Output keeps original line prefix up to DLC and replaces Data Bytes with single decimal CMD.
# Important: only parse byte tokens AFTER the DLC field to avoid picking up ID (0002) etc.
$outLines = New-Object System.Collections.Generic.List[string]
$prevUs = $null
$wroteHeader = $false

function Add-Header {
    param([System.Collections.Generic.List[string]]$Lines, [string]$Format)

    if ($Format -eq 'Tsv') {
        $Lines.Add("N`tTime_ms`tdt_us`tID/cmd`td1`td2`td3") | Out-Null
        return
    }

    # Fixed-width, Notepad-friendly
    $Lines.Add(('N'.PadLeft(6)) + ' ' +
        ('Time_ms'.PadLeft(11)) + ' ' +
        ('dt_us'.PadLeft(7)) + '  ' +
        ('ID/cmd'.PadRight(14)) + ' ' +
        ('d1'.PadLeft(6)) + ' ' +
        ('d2'.PadLeft(6)) + ' ' +
        ('d3'.PadLeft(6))) | Out-Null
}

function Add-DataLine {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Format,
        [string]$MsgNo,
        [string]$Time,
        [string]$DtUs,
        [int]$IdDec,
        [int]$Cmd,
        [int]$D1,
        [int]$D2,
        [int]$D3
    )

    if ($Format -eq 'Tsv') {
        $Lines.Add("$MsgNo`t$Time`t$DtUs`tID=$IdDec   cmd=$Cmd`t$D1`t$D2`t$D3") | Out-Null
        return
    }

    $idCmd = ("ID=$IdDec   cmd=$Cmd").PadRight(16)
    $Lines.Add(
        ($MsgNo.PadLeft(6)) + ' ' +
        ($Time.PadLeft(11)) + ' ' +
        ($DtUs.PadLeft(7)) + '  ' +
        $idCmd + ' ' +
        ($D1.ToString().PadLeft(6)) + ' ' +
        ($D2.ToString().PadLeft(6)) + ' ' +
        ($D3.ToString().PadLeft(6))
    ) | Out-Null
}

Get-Content -LiteralPath $InputPath | ForEach-Object {
    $line = $_

    if ([string]::IsNullOrWhiteSpace($line)) {
        $outLines.Add($line) | Out-Null
        return
    }
    if ($line.StartsWith(';')) {
        $outLines.Add($line) | Out-Null
        return
    }

    # Parse a classic PCAN-View trace line and emit a reduced line:
    # "{MsgNo}) {TimeOffset}  ID=0002  cmd=55"
    # We drop Rx/Tx column, convert first two data bytes to CMD (little-endian).
    # Capture groups:
    #  1) message number (with possible leading spaces) like "     1)"
    #  2) time offset token like "3113.7"
    #  3) ID (hex) like "0002"
    #  4) DLC like "8"
    #  5) first data byte (hex)
    #  6) second data byte (hex)
    # Supports both formats:
    # 1) Classic example (has MsgNo with ")" and then Time, Dir, ID, DLC, data...)
    #    "  22)      3125.3  Rx         0002  8  06 00 ..."
    # 2) Newer example (separate "DT" column before ID and then Dir, DLC, data...)
    #    "      1      4600.949 DT     0002 Rx 8  02 00 ..."
    $m = [System.Text.RegularExpressions.Regex]::Match(
        $line,
        '^\s*(\d+)\)?\s+(\d+(?:\.[0-9]+)?)\s+(?:\S+\s+)?([0-9A-Fa-f]{1,8})\s+(?:Rx|Tx)\s+([0-8])\s+((?:[0-9A-Fa-f]{2}\s+){0,7}[0-9A-Fa-f]{2})(?:\s+.*)?$'
    )

    if (-not $m.Success) {
        # Adjust header hints if present.
        $h = $line
        $h = $h -replace '\|\s*\|\s*\|\s*\|\s*\|\s*\|\s*Data Bytes \(hex\) \.\.\.', '|   |   Time | dt_us | Dir | ID | cmd | d1 | d2 | d3'
        $h = $h -replace '\|\s*\|\s*\|\s*\|\s*\|\s*Data Length', '|   |   Time | dt_us | Dir | ID | cmd | d1 | d2 | d3'
        $h = $h -replace 'ID \(hex\)', 'ID (hex)'
        $outLines.Add($h) | Out-Null
        return
    }

    $msgNo = $m.Groups[1].Value.Trim()
    $time = $m.Groups[2].Value
    $idHex = $m.Groups[3].Value
    $dlc = [int]$m.Groups[4].Value
    $dataStr = $m.Groups[5].Value

    $idDec = [Convert]::ToInt32($idHex, 16)

    $timeDouble = 0.0
    $timeUs = $null
    if ([double]::TryParse($time, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$timeDouble)) {
        $timeUs = [int64][Math]::Round($timeDouble * 1000.0)
    }

    $dtUs = ''
    if ($timeUs -ne $null) {
        if ($prevUs -ne $null) {
            $dtUs = [string]($timeUs - $prevUs)
        }
        $prevUs = $timeUs
    }

    $bytes = @()
    if (-not [string]::IsNullOrWhiteSpace($dataStr)) {
        $bytes = $dataStr -split '\s+' | Where-Object { $_ -ne '' }
    }

    $b = @(0,0,0,0,0,0,0,0)
    for ($i=0; $i -lt 8; $i++) {
        if ($i -lt $bytes.Count) {
            $b[$i] = [Convert]::ToInt32($bytes[$i], 16)
        }
    }

    $cmd = ($b[1] -shl 8) -bor $b[0]
    $d3 = ($b[3] -shl 8) -bor $b[2]
    $d2 = ($b[5] -shl 8) -bor $b[4]
    $d1 = ($b[7] -shl 8) -bor $b[6]

    if (-not $wroteHeader) {
        Add-Header -Lines $outLines -Format $Format
        $wroteHeader = $true
    }

    Add-DataLine -Lines $outLines -Format $Format -MsgNo $msgNo -Time $time -DtUs $dtUs -IdDec $idDec -Cmd $cmd -D1 $d1 -D2 $d2 -D3 $d3
}

$outLines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Wrote $($outLines.Count) lines to $OutputPath"
