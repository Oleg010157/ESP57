param(
    [Parameter(Mandatory=$true)]
    [string]$InputPath,

    [Parameter(Mandatory=$false)]
    [string]$OutputPath,

    [int]$MaxPendingPerCmd = 4096,

    [switch]$KeepPcanHeader
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

trap {
    Write-Error ("PcanRttAnalyzer failed at line $($_.InvocationInfo.ScriptLineNumber): $($_.Exception.Message)")
    throw
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "Input file not found: $InputPath"
}

$lines = Get-Content -LiteralPath $InputPath

$outLines = New-Object System.Collections.Generic.List[string]

# Pending TX timestamps per cmd (list used as FIFO)
$pending = @{}

# Parse converted lines.
# Supported inputs:
# 1) TSV output from PcanViewCmdExtractor (Format=Tsv):
#    N<TAB>Time_ms<TAB>dt_us<TAB>ID/cmd<TAB>d1<TAB>d2<TAB>d3
#    1<TAB>4600.949<TAB>168<TAB>ID=1   cmd=2<TAB>0<TAB>0<TAB>0
# 2) Fixed-width output from PcanViewCmdExtractor (Format=Fixed, default):
#         1    4600.949     168  ID=1   cmd=2          0      0      0
$rxTsv = New-Object System.Text.RegularExpressions.Regex('^\s*(\d+)\t(\S+)\t(\S*)\tID=(\d+)\s+cmd=(\d+)\t(-?\d+)\t(-?\d+)\t(-?\d+)\s*$')
$rxFixed = New-Object System.Text.RegularExpressions.Regex('^\s*(\d+)\s+(\S+)\s+(\S*)\s+ID=(\d+)\s+cmd=(\d+)\s+(-?\d+)\s+(-?\d+)\s+(-?\d+)\s*$')

$wroteHeader = $false

foreach ($line in $lines) {
    if ($null -eq $line) {
        continue
    }

    if (-not $KeepPcanHeader) {
        if ($line -match '^\s*;') { continue }
        if ($line -match '^\s*\|') { continue }
        if ($line -match '^\s*-{3,}') { continue }
    }

    $m = $rxTsv.Match($line)
    if (-not $m.Success) {
        $m = $rxFixed.Match($line)
    }
    if (-not $m.Success) {
        if ($KeepPcanHeader) {
            $outLines.Add($line) | Out-Null
        }
        continue
    }

    if (-not $wroteHeader) {
        $outLines.Add("N`tTime_ms   dt_us`tID`tcmd`td1`td2`td3`trtt_us") | Out-Null
        $wroteHeader = $true
    }

    $n = $m.Groups[1].Value
    $timeMsStr = $m.Groups[2].Value
    $dtUsStr = $m.Groups[3].Value
    $id = [int]$m.Groups[4].Value
    $cmd = [int]$m.Groups[5].Value
    $d1 = $m.Groups[6].Value
    $d2 = $m.Groups[7].Value
    $d3 = $m.Groups[8].Value

    $timeMs = 0.0
    if (-not [double]::TryParse($timeMsStr, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$timeMs)) {
        $outLines.Add("$n`t$timeMsStr   $dtUsStr`t$id`t$cmd`t$d1`t$d2`t$d3`t") | Out-Null
        continue
    }

    $rttUs = ''

    if ($id -eq 2) {
        if (-not $pending.ContainsKey($cmd) -or $null -eq $pending[$cmd]) {
            $pending[$cmd] = New-Object System.Collections.ArrayList
        }

        $list = [System.Collections.ArrayList]$pending[$cmd]
        while ($list.Count -gt $MaxPendingPerCmd) {
            $list.RemoveAt(0)
        }
        $null = $list.Add($timeMs)
    }
    elseif ($id -eq 1) {
        if ($pending.ContainsKey($cmd)) {
            $list = [System.Collections.ArrayList]$pending[$cmd]
            if ($null -ne $list -and $list.Count -gt 0) {
                $txMs = [double]$list[0]
                $list.RemoveAt(0)
                $rttUs = [string]([int][Math]::Round(($timeMs - $txMs) * 1000.0))
            }
        }
    }

    $outLines.Add("$n`t$timeMsStr   $dtUsStr`t$id`t$cmd`t$d1`t$d2`t$d3`t$rttUs") | Out-Null
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = [System.IO.Path]::ChangeExtension($InputPath, '.rtt.tsv')
}

[System.IO.File]::WriteAllLines($OutputPath, $outLines, [System.Text.Encoding]::UTF8)
Write-Host "Wrote $($outLines.Count) lines to $OutputPath"