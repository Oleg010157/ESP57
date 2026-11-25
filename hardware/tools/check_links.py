# Сверка разъёмов между платами K57: на каждом контакте ответных разъёмов - одна и та же цепь.
# Запуск: python -I hardware/tools/check_links.py hardware
import sys, os, subprocess, tempfile, re
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sch_lib import parse, find

HW = sys.argv[1]
CLI = r"C:\Program Files\KiCad\10.0\bin\kicad-cli.exe"
# (плата, разъём) <-> (плата, разъём)
PAIRS = [(("k57-power", "J601"), ("k57-control", "J501")),
         (("k57-power", "J602"), ("k57-control", "J502"))]

def pin_nets(board):
    out = os.path.join(tempfile.gettempdir(), f"k57_{board}.net")
    subprocess.run([CLI, "sch", "export", "netlist", "--format", "kicadsexpr", "-o", out,
                    os.path.join(HW, board, board + ".kicad_sch")], check=True, capture_output=True)
    t = parse(open(out, encoding="utf-8").read())[0]
    res = {}
    for n in find(find(t, "nets")[0], "net"):
        name = str(find(n, "name")[0][1])
        if name.startswith("unconnected-"):
            name = "(не подключено)"
        name = re.sub(r"^/", "", name)          # цепь верхнего листа: /PWM1A -> PWM1A
        for x in find(n, "node"):
            res[(str(find(x, "ref")[0][1]), str(find(x, "pin")[0][1]))] = name
    return res

cache = {}
bad = 0
for (b1, j1), (b2, j2) in PAIRS:
    for b in (b1, b2):
        if b not in cache:
            cache[b] = pin_nets(b)
    p1 = {pin: net for (ref, pin), net in cache[b1].items() if ref == j1}
    p2 = {pin: net for (ref, pin), net in cache[b2].items() if ref == j2}
    if not p1 or not p2:
        print(f"{b1}:{j1} <-> {b2}:{j2}: разъём не найден"); bad += 1; continue
    diff = [(pin, p1.get(pin), p2.get(pin)) for pin in sorted(set(p1) | set(p2), key=lambda s: int(s) if s.isdigit() else 0)
            if p1.get(pin) != p2.get(pin)]
    if diff:
        bad += 1
        print(f"РАСХОЖДЕНИЕ {b1}:{j1} <-> {b2}:{j2}:")
        for pin, a, c in diff:
            print(f"   контакт {pin}: {a}  <->  {c}")
    else:
        print(f"совпадает: {b1}:{j1} <-> {b2}:{j2} ({len(p1)} контактов)")
sys.exit(1 if bad else 0)
