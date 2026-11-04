# Ищет строки в exe и в UTF-16 (строки C#), и в UTF-8 (имена типов и методов).
param([string]$Exe, [string]$Pattern)
$b = [IO.File]::ReadAllBytes($Exe)
foreach ($t in @([Text.Encoding]::Unicode.GetString($b), [Text.Encoding]::UTF8.GetString($b))) {
    [regex]::Matches($t, $Pattern, 'IgnoreCase') | ForEach-Object { $_.Value }
}
