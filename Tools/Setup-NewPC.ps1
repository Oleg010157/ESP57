<#

.SYNOPSIS

    Настройка проекта K57 на новой машине: восстанавливает всё, что не едет с git.



.DESCRIPTION

    Патчи в установленном C2000Ware, пользовательский конфиг clangd и ссылки в

    CLAUDE.md живут вне репозитория, поэтому после переезда или переустановки

    C2000Ware/CCS их приходится накатывать заново. Скрипт делает это сам и

    безопасен для повторного запуска: каждый шаг сначала проверяет, нужен ли он.



    Подробности и история по каждому патчу:

    INVERTOR_CCS/my_Files/C2000Ware_local_patches.txt



.PARAMETER Check

    Ничего не менять, только показать, что требуется.



.PARAMETER C2000Ware

    Путь к нужной установке C2000Ware. По умолчанию берётся самая новая.



.EXAMPLE

    powershell -ExecutionPolicy Bypass -File Tools\Setup-NewPC.ps1 -Check

    powershell -ExecutionPolicy Bypass -File Tools\Setup-NewPC.ps1

#>



[CmdletBinding()]

param(

    [switch]$Check,

    [string]$C2000Ware

)



$ErrorActionPreference = 'Stop'

$script:Todo    = 0   # сколько шагов требуют действия

$script:Changed = 0   # сколько применено

$script:Failed  = 0



$RepoRoot = Split-Path -Parent $PSScriptRoot



function Say([string]$Step, [string]$State, [string]$Note) {

    $color = 'Gray'

    if ($State -eq 'OK')       { $color = 'Green'  }

    if ($State -eq 'ПРИМЕНЁН') { $color = 'Cyan'   }

    if ($State -eq 'НУЖЕН')    { $color = 'Yellow' }

    if ($State -eq 'ОШИБКА')   { $color = 'Red'    }

    Write-Host ("  {0,-30} " -f $Step) -NoNewline

    Write-Host ("{0,-10}" -f $State) -ForegroundColor $color -NoNewline

    Write-Host " $Note"

}



# Шаг требует действия: в режиме -Check только считаем, иначе разрешаем правку.

function Need-Action {

    $script:Todo++

    return (-not $Check)

}



function Backup-Once([string]$Path) {

    $bak = "$Path.bak"

    if (-not (Test-Path $bak)) { Copy-Item $Path $bak }

    return $bak

}



function Read-Text([string]$Path) {

    return [IO.File]::ReadAllText($Path)

}



function Write-Text([string]$Path, [string]$Text) {

    # Без BOM: это заголовки C, их читает cl2000.

    $enc = New-Object System.Text.UTF8Encoding($false)

    [IO.File]::WriteAllText($Path, $Text, $enc)

}



Write-Host ""

Write-Host "=== K57: настройка новой машины ===" -ForegroundColor White

if ($Check) { Write-Host "    (режим проверки, изменения не вносятся)" -ForegroundColor DarkGray }

Write-Host ""



# --------------------------------------------------------------------------

# Поиск установок

# --------------------------------------------------------------------------

Write-Host "Установки" -ForegroundColor White



if (-not $C2000Ware) {

    $cands = @()

    foreach ($root in @('C:\ti', 'C:\TI')) {

        if (Test-Path $root) {

            # Имя вида C2000Ware_26_01_00_00 - только базовый пакет. Надстройки

            # (C2000Ware_MotorControl_SDK_*, C2000Ware_DigitalPower_SDK_*) тоже

            # подходят под C2000Ware_*, но нужных заголовков в них нет, поэтому

            # дополнительно требуем driverlib для f28p55x.

            $cands += Get-ChildItem $root -Directory -Filter 'C2000Ware_*' -ErrorAction SilentlyContinue |

                      Where-Object {

                          ($_.Name -match '^C2000Ware_\d+(_\d+)*$') -and

                          (Test-Path (Join-Path $_.FullName 'driverlib\f28p55x\driverlib\cpu.h'))

                      }

        }

    }

    # C:\ti и C:\TI - один и тот же каталог, убираем дубли по имени.

    $cands = @($cands | Sort-Object -Property Name -Unique)



    # Сортировка строкой поставила бы 6_00_01_00 выше 26_01_00_00, поэтому

    # сравниваем номера версий как числа.

    $byVersion = {

        $m = [regex]::Match($_.Name, '^C2000Ware_(\d+)_(\d+)_(\d+)_(\d+)$')

        if ($m.Success) {

            [version]("{0}.{1}.{2}.{3}" -f $m.Groups[1].Value, $m.Groups[2].Value, $m.Groups[3].Value, $m.Groups[4].Value)

        } else {

            [version]'0.0.0.0'

        }

    }

    $best = @($cands | Sort-Object $byVersion -Descending)

    if ($best.Count -gt 0) { $C2000Ware = $best[0].FullName }

}



if (-not $C2000Ware -or -not (Test-Path $C2000Ware)) {

    Say "C2000Ware" "ОШИБКА" "не найден - укажите путь через -C2000Ware"

    Write-Host ""

    exit 1

}

Say "C2000Ware" "OK" $C2000Ware



$ccsRoot = $null

foreach ($root in @('C:\ti', 'C:\TI')) {

    if (Test-Path $root) {

        $c = @(Get-ChildItem $root -Directory -Filter 'ccs*' -ErrorAction SilentlyContinue |

               Where-Object { Test-Path (Join-Path $_.FullName 'ccs\theia\resources\clangd\clangd.exe') } |

               Sort-Object Name -Descending)

        if ($c.Count -gt 0) { $ccsRoot = $c[0].FullName; break }

    }

}

if ($ccsRoot) {

    Say "CCStudio" "OK" $ccsRoot

} else {

    Say "CCStudio" "НУЖЕН" "не найден (нужен только для проверки clangd)"

}



Write-Host ""

Write-Host "Патчи C2000Ware" -ForegroundColor White



# --------------------------------------------------------------------------

# Патч 1 - только проверка. TI поставляет guard начиная с C2000Ware 26.01.

# --------------------------------------------------------------------------

$cpuH = Join-Path $C2000Ware 'driverlib\f28p55x\driverlib\cpu.h'

if (-not (Test-Path $cpuH)) {

    Say "1. IDLE (cpu.h)" "ОШИБКА" "файл не найден"

    $script:Failed++

} elseif ((Read-Text $cpuH) -match '_DUAL_HEADERS') {

    Say "1. IDLE (cpu.h)" "OK" "guard уже в поставке TI"

} else {

    # Версия старее 26.01 - guard отсутствует, правится вручную.

    Say "1. IDLE (cpu.h)" "НУЖЕН" "guard отсутствует, см. памятку, Patch 1"

    $script:Todo++

}



# --------------------------------------------------------------------------

# Патч 2 - SUCCESS/FAIL в stw_types.h

# --------------------------------------------------------------------------

$stw = Join-Path $C2000Ware 'driverlib\f28p55x\driverlib\inc\stw_types.h'

if (-not (Test-Path $stw)) {

    Say "2. SUCCESS/FAIL" "ОШИБКА" "stw_types.h не найден"

    $script:Failed++

} else {

    $t = Read-Text $stw

    if (($t -match '#ifndef\s+SUCCESS') -and ($t -match '#ifndef\s+FAIL')) {

        Say "2. SUCCESS/FAIL" "OK" ""

    } elseif (Need-Action) {

        $bak = Backup-Once $stw

        $t = [regex]::Replace($t, '(?m)^#define[ \t]+SUCCESS[ \t]+.*$', "#ifndef SUCCESS`r`n`$&`r`n#endif")

        $t = [regex]::Replace($t, '(?m)^#define[ \t]+FAIL[ \t]+.*$',    "#ifndef FAIL`r`n`$&`r`n#endif")

        Write-Text $stw $t

        $script:Changed++

        Say "2. SUCCESS/FAIL" "ПРИМЕНЁН" ("бэкап: " + (Split-Path -Leaf $bak))

    } else {

        Say "2. SUCCESS/FAIL" "НУЖЕН" "будет обёрнут в #ifndef"

    }

}



# --------------------------------------------------------------------------

# Патч 3 - Ramfuncs* в f28p55x_globalprototypes.h (ломает только сборку _FLASH)

# --------------------------------------------------------------------------

$gp = Join-Path $C2000Ware 'device_support\f28p55x\common\include\f28p55x_globalprototypes.h'

if (-not (Test-Path $gp)) {

    Say "3. Ramfuncs*" "ОШИБКА" "f28p55x_globalprototypes.h не найден"

    $script:Failed++

} else {

    $t = Read-Text $gp

    $block = '(?m)^extern[ \t]+Uint16[ \t]+RamfuncsLoadStart;\r?\n' +

             'extern[ \t]+Uint16[ \t]+RamfuncsLoadEnd;\r?\n'        +

             'extern[ \t]+Uint16[ \t]+RamfuncsLoadSize;\r?\n'       +

             'extern[ \t]+Uint16[ \t]+RamfuncsRunStart;\r?\n'       +

             'extern[ \t]+Uint16[ \t]+RamfuncsRunEnd;\r?\n'         +

             'extern[ \t]+Uint16[ \t]+RamfuncsRunSize;\r?\n'

    if ($t -match '#ifndef[ \t]+_FLASH[\s\S]{0,200}?RamfuncsLoadStart') {

        Say "3. Ramfuncs*" "OK" ""

    } elseif ($t -notmatch $block) {

        Say "3. Ramfuncs*" "ОШИБКА" "блок объявлений не распознан, править вручную"

        $script:Failed++

    } elseif (Need-Action) {

        $bak = Backup-Once $gp

        Write-Text $gp ([regex]::Replace($t, $block, "#ifndef _FLASH`r`n`$&#endif`r`n"))

        $script:Changed++

        Say "3. Ramfuncs*" "ПРИМЕНЁН" ("бэкап: " + (Split-Path -Leaf $bak))

    } else {

        Say "3. Ramfuncs*" "НУЖЕН" "будет обёрнут в #ifndef _FLASH"

    }

}



# --------------------------------------------------------------------------

# Патч 4 - пользовательский конфиг clangd. Только редактор, сборку не трогает.

# Бэкслеши собираются через $bs, чтобы не плодить экранирование в исходнике.

# --------------------------------------------------------------------------

$bs      = [string][char]92

$cfgDir  = Join-Path $env:LOCALAPPDATA 'clangd'

$cfgPath = Join-Path $cfgDir 'config.yaml'

$yaml = @"

If:

  PathMatch:

    - .*[Cc]2000[Ww]are.*

    - .*[/$bs$bs][Ww]ork[/$bs$bs]K5[0-9][/$bs$bs].*

    - .*[/$bs$bs]ti[/$bs$bs]ccs.*

CompileFlags:

  Add:

    - -D__TMS320C28XX__=1

"@



if ((Test-Path $cfgPath) -and ((Read-Text $cfgPath) -match '__TMS320C28XX__')) {

    Say "4. clangd config.yaml" "OK" ""

} elseif (Need-Action) {

    if (-not (Test-Path $cfgDir)) { New-Item -ItemType Directory -Path $cfgDir -Force | Out-Null }

    if (Test-Path $cfgPath) { Backup-Once $cfgPath | Out-Null }

    Write-Text $cfgPath $yaml

    $script:Changed++

    Say "4. clangd config.yaml" "ПРИМЕНЁН" $cfgPath

} else {

    Say "4. clangd config.yaml" "НУЖЕН" $cfgPath

}



# --------------------------------------------------------------------------

# Ссылки на docs/agents-ccs-path.md в CLAUDE.md. Файлы не отслеживаются git,

# CCStudio создаёт их заново уже без ссылки.

# --------------------------------------------------------------------------

Write-Host ""

Write-Host "Прочее, что не едет с git" -ForegroundColor White



$claudeFiles = @(

    @{ Name = 'CLAUDE.md';              Path = (Join-Path $RepoRoot 'CLAUDE.md');              Link = '@docs/agents-ccs-path.md'    },

    @{ Name = 'INVERTOR_CCS/CLAUDE.md'; Path = (Join-Path $RepoRoot 'INVERTOR_CCS\CLAUDE.md'); Link = '@../docs/agents-ccs-path.md' }

)

foreach ($f in $claudeFiles) {

    if (-not (Test-Path $f.Path)) {

        Say $f.Name "НУЖЕН" "файла нет, появится после запуска CCStudio"

        $script:Todo++

    } elseif ((Read-Text $f.Path) -match [regex]::Escape($f.Link)) {

        Say $f.Name "OK" ""

    } elseif (Need-Action) {

        $add = "`r`n## Путь к CCS: сгенерированный блок выше может быть от другой машины`r`n`r`n" +

               "Этот файл сам git не отслеживает - его верхнюю часть переписывает CCStudio.`r`n" +

               "Разбор и порядок действий лежат в отслеживаемом файле:`r`n`r`n" + $f.Link + "`r`n"

        [IO.File]::AppendAllText($f.Path, $add, (New-Object System.Text.UTF8Encoding($false)))

        $script:Changed++

        Say $f.Name "ПРИМЕНЁН" "ссылка дописана"

    } else {

        Say $f.Name "НУЖЕН" "нет ссылки на agents-ccs-path.md"

    }

}



# --------------------------------------------------------------------------

# Claude Code: вход. Автоматизировать нельзя, но можно не гадать - пустое окно

# в CCStudio означает именно это.

# --------------------------------------------------------------------------

$cred = Join-Path $env:USERPROFILE '.claude\.credentials.json'

if (Test-Path $cred) {

    Say "Claude Code: вход" "OK" ""

} else {

    Say "Claude Code: вход" "НУЖЕН" "окно будет пустым, выполните 'claude' в терминале"

    $script:Todo++

}



# --------------------------------------------------------------------------

# Итог

# --------------------------------------------------------------------------

Write-Host ""

if ($script:Failed -gt 0) {

    Write-Host "Есть ошибки ($script:Failed). Смотрите строки выше и памятку:" -ForegroundColor Red

    Write-Host "  INVERTOR_CCS/my_Files/C2000Ware_local_patches.txt"

} elseif ($Check) {

    if ($script:Todo -eq 0) {

        Write-Host "Всё на месте, делать нечего." -ForegroundColor Green

    } else {

        Write-Host "Требуют действия: $script:Todo. Запустите скрипт без -Check." -ForegroundColor Yellow

    }

} else {

    if ($script:Changed -eq 0) {

        Write-Host "Всё уже было на месте, изменений нет." -ForegroundColor Green

    } else {

        Write-Host "Применено: $script:Changed." -ForegroundColor Cyan

    }

    Write-Host ""

    Write-Host "Дальше вручную, скриптом не делается:" -ForegroundColor White

    Write-Host "  - Project -> Build Configurations -> Set Active (CPU1_RAM или CPU1_FLASH)"

    Write-Host "  - пересобрать обе конфигурации и убедиться, что чисто"

}

Write-Host ""

