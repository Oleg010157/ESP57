"""
Проверка моста K57 по сети без Motor_Control.

Делает то же, что будет делать программа на ПК: подключается по TCP,
просит телеметрию по UDP (датаграмма K57U), шлёт команды и считает, что
пришло. Формат - ESP32_BRIDGE/main/net_proto.h и docs/esp32-spi-protocol-RU.md.

    python net_test.py                     мост на 192.168.4.1 (своя точка K57)
    python net_test.py 192.168.88.50       мост в общей сети
    python net_test.py k57.local 333 0     адрес и команды для отправки

Команды по умолчанию: 0 (прибор отвечает 55 55 и флагами) и 333 (строка
с версией прошивки, 79 ACK). Работает 10 секунд, раз в секунду печатает,
сколько кадров каждого номера пришло.
"""
import socket
import struct
import sys
import time

TCP_PORT = 5520
UDP_PORT = 5521
SYNC = 0xA5


def rec(frame_id, data):
    return bytes([SYNC, len(data)]) + struct.pack("<H", frame_id) + data


def cmd_frame(cmd, d1=0, d2=0, d3=0):
    # Раскладка Motor_Control (MainWindow.cs): cmd, d3, d2, d1, младшим вперёд.
    return rec(2, struct.pack("<HHHH", cmd & 0xFFFF, d3 & 0xFFFF, d2 & 0xFFFF, d1 & 0xFFFF))


def parse_records(buf):
    """Кадры из потока; возвращает (кадры, остаток)."""
    out, pos = [], 0
    while len(buf) - pos >= 4:
        if buf[pos] != SYNC:
            pos += 1
            continue
        n = buf[pos + 1]
        if len(buf) - pos < 4 + n:
            break
        fid = buf[pos + 2] | (buf[pos + 3] << 8)
        out.append((fid, buf[pos + 4:pos + 4 + n]))
        pos += 4 + n
    return out, buf[pos:]


def main():
    host = sys.argv[1] if len(sys.argv) > 1 else "192.168.4.1"
    cmds = [int(x) for x in sys.argv[2:]] or [0, 333]

    tcp = socket.create_connection((host, TCP_PORT), timeout=5)
    tcp.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    tcp.setblocking(False)
    udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    udp.bind(("", 0))
    udp.setblocking(False)
    peer = (socket.gethostbyname(host), UDP_PORT)
    print(f"TCP {host}:{TCP_PORT} подключено, UDP с порта {udp.getsockname()[1]}")

    count = {}
    acks = []
    tcp_buf = b""
    udp_seq = None
    udp_gaps = 0
    f28_drop = 0
    t0 = time.time()
    last_hello = 0
    last_print = t0
    sent = False

    while time.time() - t0 < 10:
        now = time.time()
        if now - last_hello >= 1:
            udp.sendto(b"K57U", peer)
            last_hello = now
        if not sent and now - t0 > 1:
            for c in cmds:
                tcp.sendall(cmd_frame(c))
                print(f"-> команда {c}")
            sent = True

        try:
            data = tcp.recv(4096)
            if not data:
                print("мост закрыл соединение")
                return
            frames, tcp_buf = parse_records(tcp_buf + data)
            for fid, d in frames:
                count[fid] = count.get(fid, 0) + 1
                if fid == 1 and len(d) == 8:
                    acks.append(struct.unpack("<Hhhh", d))
        except BlockingIOError:
            pass

        try:
            while True:
                dg, _ = udp.recvfrom(2048)
                if dg[:4] != b"K57F":
                    continue
                seq, drop = struct.unpack("<HH", dg[4:8])
                if udp_seq is not None and seq != (udp_seq + 1) & 0xFFFF:
                    udp_gaps += 1
                udp_seq = seq
                f28_drop += drop
                count["udp"] = count.get("udp", 0) + 1
                frames, _ = parse_records(dg[8:])
                for fid, _d in frames:
                    count[fid] = count.get(fid, 0) + 1
        except BlockingIOError:
            pass

        if now - last_print >= 1:
            line = ", ".join(f"{k}: {v}" for k, v in sorted(count.items(), key=str))
            print(f"за 1 с: {line or 'ничего'} | разрывов UDP {udp_gaps}, не влезло на приборе {f28_drop}")
            for a in acks[:5]:
                print(f"   <- ACK {a[0]}: {a[1]} {a[2]} {a[3]}")
            if len(acks) > 5:
                print(f"   ... и ещё {len(acks) - 5} ACK")
            count, acks = {}, []
            last_print = now
        time.sleep(0.002)


if __name__ == "__main__":
    main()
