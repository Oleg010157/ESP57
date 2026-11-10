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
        public const string DefaultApSsid = "K57";       // её имя (K57_AP_SSID в мосте)
        public const int TcpPort = 5520;
        public const int UdpPort = 5521;

        private const byte Sync = 0xA5;
        private const int RecHdr = 4; // метка, длина, ID (2 байта)

        // Служебная запись моста (net_proto.h, K57_INFO_ID): в какой он сети.
        // Кадром прибора она не является и в разбор CANA.cs не идёт.
        private const uint InfoId = 0x7F0;

        /// <summary>
        /// Записи 0x7E0..0x7EF - чёрный ящик моста (ESP32_BRIDGE/main/bbox.h):
        /// ответы на вопросы пульта, а не кадры прибора. В разбор CANA.cs не идут.
        /// Аргументы - номер и данные записи. Зовётся из потока приёма.
        /// </summary>
        public event Action<uint, byte[]> BridgeRecord;

        public static bool IsBridgeId(uint id) => (id & 0xFFF0) == 0x7E0;

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
        private volatile bool _alive; // объект в работе: не закрыт Dispose
        private volatile bool _up;    // TCP-соединение есть прямо сейчас

        private int _lastSeq = -1;

        // Пауза между попытками переподключиться. Мост в режиме своей точки
        // встаёт после перезагрузки меньше чем за секунду, а сторож связи на
        // приборе ждёт команду 336 пять секунд: успеваем без потери сеанса.
        private const int ReconnectMs = 1000;

        public string Host { get; }

        /// <summary>Датаграмм телеметрии потеряно в сети - по разрывам номера.</summary>
        public int UdpGaps { get; private set; }

        /// <summary>Кадров, не влезших в посылку SPI на приборе (из датаграмм).</summary>
        public int F28Drop { get; private set; }

        /// <summary>Связь с мостом есть прямо сейчас.</summary>
        public bool Connected => _up;

        /// <summary>Связь пропала или вернулась. Зовётся из своего потока.</summary>
        public event Action LinkChanged;

        /// <summary>
        /// Зовётся, когда переподключиться не выходит несколько секунд: окно
        /// просит Windows снова войти в сеть моста (CANA.cs, JoinBridgeAp).
        /// Из своего потока, можно блокировать.
        /// </summary>
        public Action Rejoin;

        // После скольких неудачных попыток подряд звать Rejoin и как часто
        // повторять: 3 - мост успевает встать сам, 10 - не долбить netsh.
        private const int RejoinFirst = 3;
        private const int RejoinEvery = 10;

        /// <param name="rxEvent">Тот же _re, что PCAN будит по приёму: поток приёма
        /// в CANA.cs ждёт на нём и тут же разбирает очередь.</param>
        public WifiLink(string host, AutoResetEvent rxEvent)
        {
            Host = host;
            _rxEvent = rxEvent;
        }

        /// <summary>
        /// Подключиться. Блокирует не дольше timeoutMs. Не вышло - false, и
        /// объект можно выбросить. Вышло - дальше связь держится сама: пропала
        /// (мост перезагрузили, WiFi моргнул) - раз в секунду пробуем снова,
        /// очередь кадров и поток приёма в CANA.cs остаются прежними.
        /// </summary>
        public bool Connect(int timeoutMs = 2000)
        {
            if (!TryOpen(timeoutMs))
            {
                Dispose();
                return false;
            }

            try
            {
                _udp = new UdpClient(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WifiLink] UDP: {ex.Message}");
                Dispose();
                return false;
            }

            _alive = true;
            new Thread(Supervise) { IsBackground = true, Name = "WifiLink TCP" }.Start();
            new Thread(UdpLoop) { IsBackground = true, Name = "WifiLink UDP" }.Start();
            _hello = new Timer(_ => SendHello(), null, 0, 1000);
            return true;
        }

        private bool TryOpen(int timeoutMs)
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

                var addr = ((IPEndPoint)tcp.Client.RemoteEndPoint).Address;
                lock (_txLock)
                {
                    _tcp = tcp;
                    _stream = tcp.GetStream();
                    _bridgeUdp = new IPEndPoint(addr, UdpPort);
                }
                _up = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WifiLink] {Host}: {ex.Message}");
                return false;
            }
        }

        // Держит связь: пока есть - читает TCP, пропала - переподключается.
        private void Supervise()
        {
            int fails = 0;
            while (_alive)
            {
                if (_up)
                {
                    TcpLoop(); // вернётся, когда соединение оборвётся
                    if (!_alive) break;

                    _up = false;
                    CloseTcp();
                    Debug.WriteLine("[WifiLink] связь пропала, переподключаюсь");
                    LinkChanged?.Invoke();
                    _rxEvent.Set();
                    continue;
                }

                Thread.Sleep(ReconnectMs);
                if (_alive && TryOpen(2000))
                {
                    Debug.WriteLine("[WifiLink] связь восстановлена");
                    fails = 0;
                    SendHello(); // телеметрия - сразу, не через секунду
                    LinkChanged?.Invoke();
                    continue;
                }

                // Мост встал бы за секунду; раз не отвечает дольше - скорее
                // всего, Windows не вернулась в его сеть сама.
                fails++;
                if (fails == RejoinFirst || (fails > RejoinFirst && (fails - RejoinFirst) % RejoinEvery == 0))
                    Rejoin?.Invoke();
            }
        }

        private void CloseTcp()
        {
            lock (_txLock)
            {
                try { _stream?.Close(); } catch { }
                try { _tcp?.Close(); } catch { }
                _stream = null;
                _tcp = null;
            }
        }

        public bool TryRead(out TPCANMsgFD msg) => _rx.TryDequeue(out msg);

        /// <summary>Кадр к прибору. Длина - по DLC, как у PCAN.</summary>
        public bool Send(TPCANMsgFD msg)
        {
            if (!_up) return false;

            int len = PcanFdMessageConverter.DlcToLength(msg.DLC);
            var rec = new byte[RecHdr + len];
            rec[0] = Sync;
            rec[1] = (byte)len;
            rec[2] = (byte)msg.ID;
            rec[3] = (byte)(msg.ID >> 8);
            Array.Copy(msg.DATA, 0, rec, RecHdr, len);

            try
            {
                lock (_txLock)
                {
                    if (_stream == null) return false;
                    _stream.Write(rec, 0, rec.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                // Закрыть сокет: чтение в Supervise оборвётся, и он
                // переподключится сам.
                Debug.WriteLine($"[WifiLink] отправка: {ex.Message}");
                CloseTcp();
                return false;
            }
        }

        /// <summary>Вопрос самому мосту (чёрный ящик): запись с любой длиной до 64.</summary>
        public bool SendBridge(uint id, byte[] data)
        {
            if (!_up) return false;

            int len = data?.Length ?? 0;
            var rec = new byte[RecHdr + len];
            rec[0] = Sync;
            rec[1] = (byte)len;
            rec[2] = (byte)id;
            rec[3] = (byte)(id >> 8);
            if (len > 0) Array.Copy(data, 0, rec, RecHdr, len);

            try
            {
                lock (_txLock)
                {
                    if (_stream == null) return false;
                    _stream.Write(rec, 0, rec.Length);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WifiLink] отправка мосту: {ex.Message}");
                CloseTcp();
                return false;
            }
        }

        private void SendHello()
        {
            try
            {
                IPEndPoint to;
                lock (_txLock) to = _bridgeUdp;
                if (_alive && to != null) _udp.Send(new[] { (byte)'K', (byte)'5', (byte)'7', (byte)'U' }, 4, to);
            }
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

            if (IsBridgeId(id))
            {
                var data = new byte[len];
                Array.Copy(buf, pos + RecHdr, data, 0, len);
                BridgeRecord?.Invoke(id, data);
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

        // Читает, пока соединение живо, и возвращается при обрыве.
        private void TcpLoop()
        {
            var buf = new byte[4096];
            int have = 0;
            NetworkStream stream;
            lock (_txLock) stream = _stream;
            if (stream == null) return;
            try
            {
                while (_alive)
                {
                    int n = stream.Read(buf, have, buf.Length - have);
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
                if (_alive) Debug.WriteLine($"[WifiLink] приём TCP: {ex.Message}");
            }
        }

        private void UdpLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_alive)
            {
                byte[] dg;
                try
                {
                    dg = _udp.Receive(ref any);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Windows: K57U ушла на закрытый порт (мост перезагружается),
                    // пришёл ICMP «порт недоступен», и следующий Receive его
                    // выбрасывает. Сокет при этом жив - читаем дальше.
                    continue;
                }
                catch (Exception ex)
                {
                    if (_alive) Debug.WriteLine($"[WifiLink] приём UDP: {ex.Message}");
                    return; // сокет закрыт - Dispose
                }

                if (dg.Length < 8 || dg[0] != 'K' || dg[1] != '5' || dg[2] != '7' || dg[3] != 'F') continue;

                int seq = dg[4] | (dg[5] << 8);
                if (_lastSeq >= 0 && seq != ((_lastSeq + 1) & 0xFFFF)) UdpGaps++;
                _lastSeq = seq;
                F28Drop += dg[6] | (dg[7] << 8);

                Parse(dg, 8, dg.Length);
                _rxEvent.Set();
            }
        }

        public void Dispose()
        {
            _alive = false;
            _up = false;
            _hello?.Dispose();
            CloseTcp();
            try { _udp?.Close(); } catch { }
            _rxEvent.Set(); // разбудить поток приёма
        }
    }
}
