#!/bin/bash
# Папка для заказчика: только исполняемые файлы и ini, без исходников и без
# осциллографа, Wscope и записи в файл. Рабочий каталог не трогает: всё делается
# во временном клоне.
#
#   bash Tools/customer-drop/make-drop.sh --fw <прошивка.out> <куда> [ветка, по умолчанию main]
#
# Прошивка ложится рядом с README уже очищенной. Обычный strip2000, не -p: тот
# убирает и _c_int00, без которого программатор файл не примет. Собирать её из
# исходников режима --src. Без --fw папка соберётся без прошивки.
#
# Очищенные исходники, из которых собирать прошивку для прибора (заказчику не
# отдавать; собирать копией проекта в CCS, рядом положить bin\*.h):
#
#   bash Tools/customer-drop/make-drop.sh --src <куда> [ветка]
#
# exclude.txt - что удалить; strip.patch - правки оставшегося кода. Если main ушёл
# так далеко, что патч не ложится, git apply остановит сборку и назовёт файл:
# поправить этот файл во временном клоне и пересоздать патч (git diff --binary).
set -euo pipefail

MODE=bin
FW=
if [ "${1:-}" = "--src" ]; then MODE=src; shift; fi
if [ "${1:-}" = "--fw" ]; then FW=$(cd "$(dirname "$2")" && pwd)/$(basename "$2"); shift 2; [ -f "$FW" ] || { echo "Нет файла прошивки: $FW"; exit 1; }; fi
[ $# -ge 1 ] || { echo "Использование: $0 [--src | --fw <прошивка.out>] <куда> [ветка]"; exit 2; }
OUT=$1
REF=${2:-main}
[ ! -e "$OUT" ] || { echo "Уже существует: $OUT"; exit 1; }

HERE=$(cd "$(dirname "$0")" && pwd)
REPO=$(git -C "$HERE" rev-parse --show-toplevel)
MSBUILD="/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

git clone -q --no-hardlinks -b "$REF" "$REPO" "$TMP/src"
cd "$TMP/src"
COMMIT=$(git rev-parse --short HEAD)

tr -d '\r' < "$HERE/exclude.txt" | grep -vE '^(#|$)' | while IFS= read -r p; do
    git rm -rqf --ignore-unmatch -- "$p"
done
git apply --whitespace=nowarn "$HERE/strip.patch"
git add -A
git -c user.name=drop -c user.email=drop@localhost commit -qm "drop $COMMIT"

mkdir -p "$TMP/tree"
git archive HEAD | tar -x -C "$TMP/tree"
cd "$TMP/tree"

if [ "$MODE" = src ]; then
    mkdir -p "$OUT"
    cp -r . "$OUT"
    echo "Готово, исходники: $OUT (из $REF, коммит $COMMIT). ЗАКАЗЧИКУ НЕ ОТДАВАТЬ."
    exit 0
fi

# Сборка из очищенных исходников. DebugType=none: иначе в exe попал бы путь к pdb,
# а с ним и путь к временному каталогу на этой машине.
MSYS2_ARG_CONV_EXCL='*' "$MSBUILD" "$(cygpath -w "$PWD/Motor_Control.csproj")" \
    /t:Build /p:Configuration=Debug /p:DebugType=none /p:DebugSymbols=false /v:minimal /nologo

# Раскладка важна: программа ищет ErrorCodes.h уровнем выше своей папки.
P="$OUT/Program"
mkdir -p "$P/CanTxFiles"
cp bin/Debug/ICDIBasic.exe bin/Debug/ICDIBasic.exe.config bin/Debug/*.dll "$P/"
cp PCANBasic.dll "$P/"
cp -r bin/Debug/Resources "$P/"

# Ini: только сами параметры. Строки-комментарии - это записи разработчика со
# ссылками на исходники; программа их и так пропускает (TxDataFrm.ReadIniFile).
for f in bin/Debug/CanTxFiles/*.ini; do
    sed -b -E '/^[ \t]*\/\//d' "$f" > "$P/CanTxFiles/$(basename "$f")"
done

# Эти три папки программа при старте читает и без них падает (RefreshL). Наборы
# TEST_* - от прежних приборов, сюда они не относятся. Пустая папка архивацию не
# переживает, поэтому файл-заглушка.
for d in CanRxFiles TEST_RX TEST_TX; do
    mkdir -p "$P/$d"
    : > "$P/$d/.keep"
done

# Коды аварий без истории их нумерации: программе нужны только строки #define.
sed -b -E '/^#define/!d' bin/ErrorCodes.h > "$OUT/ErrorCodes.h"
sed 's/$/\r/' "$HERE/README-customer.txt" > "$OUT/README.txt"

if [ -n "$FW" ]; then
    STRIP=$(ls -d /c/ti/ccs*/ccs/tools/compiler/ti-cgt-c2000_*/bin/strip2000.exe | tail -1)
    "$STRIP" "$FW" -o "$OUT/invertor.out"
    if LC_ALL=C grep -aqiE 'c:.users' "$OUT/invertor.out"; then echo "ЛИЧНЫЕ ПУТИ В ПРОШИВКЕ"; exit 1; fi
fi

# config_switch.h не кладётся намеренно: в нём отладочные ключи прошивки. Без него
# окно версии просто не подпишет биты KUK (Rkf молча выходит).

# Контроль: сборка падает, если что-то просочилось.
cd "$OUT"
FORBID='wscope|scopeform|scopeview|scopecontrol|tsmscope|tapestart|f2_kadr|tooth_f9|spl_start|живо[йг][а-я]* осциллограф|claude|anthropic|codex|openrouter|c:.users.(olegp|olegw|m4800|user|olegs)|grid_tie\.|my_files|ksp/|plant_sim\.'
bad=0
if grep -rIilE "$FORBID" . ; then echo "ЗАПРЕЩЁННЫЕ СЛОВА В ТЕКСТАХ (файлы выше)"; bad=1; fi
if find . \( -name '*.c' -o -name '*.cs' -o -name '*.pdb' -o -name '*.csproj' -o -name '*.sln' -o -name '.git*' -o -name 'config_switch.h' \) | grep . ; then
    echo "ИСХОДНИКИ В ПОСТАВКЕ (файлы выше)"; bad=1
fi
# Строки C# в exe лежат в UTF-16, а имена в UTF-8, поэтому grep тут не годится.
CHECK='wscope|scopeform|scopeview|tsmscope|tapestart|tooth_f9|my_files|grid_tie\.|ksp/|olegp|\.pdb'
HIT=$(powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$HERE/exe-strings.ps1")" \
    "$(cygpath -w "$P/ICDIBasic.exe")" "$CHECK")
if [ -n "$HIT" ]; then echo "$HIT"; echo "ЗАПРЕЩЁННЫЕ СТРОКИ В EXE (выше)"; bad=1; fi
[ "$bad" = 0 ] || { echo "ПАПКУ ОТДАВАТЬ НЕЛЬЗЯ: $OUT"; exit 1; }

echo "Готово: $OUT (из $REF, коммит $COMMIT)"
