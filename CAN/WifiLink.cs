/*
 Канал к прибору через WiFi-мост ESP32 (ESP57/ESP32_BRIDGE) вместо PCAN.

 Мост переносит кадры CAN как есть, поэтому наружу отсюда выходят те же
 TPCANMsgFD, что отдаёт PCANBasic.ReadFD, и разбирает их тот же switch в
 CANA.cs. Формат по сети - docs/esp32-spi-protocol-RU.md, раздел «Сеть»;
 образец клиента - ESP32_BRIDGE/tools/net_test.py.

   TCP 5520 - команды к прибору и ACK/ошибки от него, надёжно и по порядку.
   UDP 5521 - телеметрия 103 и 140. Чтобы она пошла, раз в секунду шлём
              мосту "K57U" со своего UDP-сокета: брандмауэр Windows пропускает
              ответ на исходящую датаграмму без прав администратора.
*/

using Peak.Can.Basic;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ICDIBasic
{
    internal sealed class WifiLink : IDisposable
    {
        public const string DefaultHost = "192.168.4.1"; // своя точка доступа моста, сеть K57
        public const int TcpPort = 5520;
        public const int UdpPort = 5521;

        private const byte Sync = 0xA5;
        private const int RecHdr = 4; // метка, длина, ID (2 байта)

        // Служебная запись моста (net_proto.h, K57_INFO_ID): в какой он сети.
        // Кадром прибора она не является и в разбор CANA.cs не идёт.
        private const uint InfoId = 0x7F0;

        /// <summary>Имя сети моста; null, пока мост не сообщил.</summary>
        public string NetName { get; private set; }

        /// <summary>true - мост поднял свою точку доступа, false - он в общей сети.</summary>
        public bool NetIsOwnAp { get; private set; }

        /// <summary>Мост сообщил, в какой он сети. Зовётся из потока приёма.</summary>
        public event Action NetInfo;

        private readonly ConcurrentQueue<TPCANMsgFD> _rx = new();
        private readonly AutoResetEvent _rxEvent;
        private readonly object _txLock = new();

        private TcpClient _tcp;
        private NetworkStream _stream;
        private UdpClient _udp;
        private IPEndPoint _bridgeUdp;
        private Timer _hello;
        private volatile bool _run;

        private int _lastSeq = -1;

        public string Host { get; }

        /// <summary>Датаграмм телеметрии потеряно в сети - по разрывам номера.</summary>
        public int UdpGaps { get; private set; }

        /// <summary>Кадров, не влезших в посылку SPI на приборе (из датаграмм).</summary>
        public int F28Drop { get; private set; }

        public bool Connected => _run;

        /// <param name="rxEvent">Тот же _re, что PCAN будит по приёму: поток приёма
        /// в CANA.cs ждёт на нём и тут же разбирает очередь.</param>
        public WifiLink(string host, AutoResetEvent rxEvent)
        {
            Host = host;
            _rxEvent = rxEvent;
        }

        /// <summary>Подключиться. Блокирует не дольше timeoutMs.</summary>
        public bool Connect(int timeoutMs = 2000)
        {
            try
            {
                var tcp = new TcpClient { NoDelay = true };
                var ar = tcp.BeginConnect(Host, TcpPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    tcp.Close();
                    return false;
                }
                tcp.EndConnect(ar);

                _tcp = tcp;
                _stream = tcp.GetStream();
                var addr = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address;
                _bridgeUdp = new IPEndPoint(addr, UdpPort);
                _udp = new UdpClient(0);

                _run = true;
                new Thread(TcpLoop) { IsBackground = true, Name = "WifiLink TCP" }.Start();
                new Thread(UdpLoop) { IsBackground = true, Name = "WifiLink UDP" }.Start();
                _hello = new Timer(_ => SendHello(), null, 0, 1000);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WifiLink] {Host}: {ex.Message}");
                Dispose();
                return false;
            }
        }

        public bool TryRead(out TPCANMsgFD msg) => _rx.TryDequeue(out msg);

        /// <summary>Кадр к прибору. Длина - по DLC, как у PCAN.</summary>
        public bool Send(TPCANMsgFD msg)
        {
            if (!_run) return false;

            int len = PcanFdMessageConverter.DlcToLength(msg.DLC);
            var rec = new byte[RecHdr + len];
            rec[0] = Sync;
            rec[1] = (byte)len;
            rec[2] = (byte)msg.ID;
            rec[3] = (byte)(msg.ID >> 8);
            Array.Copy(msg.DATA, 0, rec, RecHdr, len);

            try
            {
                lock (_txLock) _stream.Write(rec, 0, rec.Length);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WifiLink] отправка: {ex.Message}");
                Stop();
                return false;
            }
        }

        private void SendHello()
        {
            try { if (_run) _udp.Send(new[] { (byte)'K', (byte)'5', (byte)'7', (byte)'U' }, 4, _bridgeUdp); }
            catch { /* следующая попытка через секунду */ }
        }

        private void Push(byte[] buf, int pos)
        {
            int len = buf[pos + 1];
            uint id = (uint)(buf[pos + 2] | (buf[pos + 3] << 8));

            if (id == InfoId)
            {
                // "STA <сеть>" или "AP <сеть>"
                var text = System.Text.Encoding.ASCII.GetString(buf, pos + RecHdr, len);
                var sp = text.IndexOf(' ');
                NetIsOwnAp = sp > 0 && text.Substring(0, sp) == "AP";
                NetName = sp > 0 ? text.Substring(sp + 1) : text;
                NetInfo?.Invoke();
                return;
            }

            var msg = new TPCANMsgFD
            {
                ID = id,
                DLC = PcanFdMessageConverter.LengthToDlc((byte)len),
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_FD,
                DATA = new byte[64]
            };
            Array.Copy(buf, pos + RecHdr, msg.DATA, 0, len);
            _rx.Enqueue(msg);
        }

        /// <summary>Записи подряд; возвращает, сколько байт разобрано.</summary>
        private int Parse(byte[] buf, int start, int end)
        {
            int pos = start;
            while (end - pos >= RecHdr)
            {
                if (buf[pos] != Sync || buf[pos + 1] > 64) { pos++; continue; } // ресинхронизация
                if (end - pos < RecHdr + buf[pos + 1]) break;                 // ждём остаток
                Push(buf, pos);
                pos += RecHdr + buf[pos + 1];
            }
            return pos - start;
        }

        private void TcpLoop()
        {
            var buf = new byte[4096];
            int have = 0;
            try
            {
                while (_run)
                {
                    int n = _stream.Read(buf, have, buf.Length - have);
                    if (n <= 0) break; // мост закрыл соединение
                    have += n;
                    int used = Parse(buf, 0, have);
                    Array.Copy(buf, used, buf, 0, have - used);
                    have -= used;
                    if (have == buf.Length) have = 0; // мусор без меток
                    _rxEvent.Set();
                }
            }
            catch (Exception ex)
            {
                if (_run) Debug.WriteLine($"[WifiLink] приём TCP: {ex.Message}");
            }
            Stop();
        }

        private void UdpLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                while (_run)
                {
                    var dg = _udp.Receive(ref any);
                    if (dg.Length < 8 || dg[0] != 'K' || dg[1] != '5' || dg[2] != '7' || dg[3] != 'F') continue;

                    int seq = dg[4] | (dg[5] << 8);
                    if (_lastSeq >= 0 && seq != ((_lastSeq + 1) & 0xFFFF)) UdpGaps++;
                    _lastSeq = seq;
                    F28Drop += dg[6] | (dg[7] << 8);

                    Parse(dg, 8, dg.Length);
                    _rxEvent.Set();
                }
            }
            catch (Exception ex)
            {
                if (_run) Debug.WriteLine($"[WifiLink] приём UDP: {ex.Message}");
            }
        }

        private void Stop()
        {
            if (!_run) return;
            _run = false;
            _rxEvent.Set(); // разбудить поток приёма: связь кончилась
        }

        public void Dispose()
        {
            _run = false;
            _hello?.Dispose();
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            try { _udp?.Close(); } catch { }
        }
    }
}
