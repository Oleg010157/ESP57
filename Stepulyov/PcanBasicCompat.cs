using System;
using System.Reflection;
using Peak.Can.Basic;

namespace ICDIBasic.Stepulyov
{
    // Малый адаптер-фаcade чтобы минимизировать правки в кодовой базе.
    // Если в сборке PCANBasic присутствуют методы InitializeFD / WriteFD — будут использованы.
    // Иначе используется старый функционал через PCANBasic.
    public static class PCANBasicCompat
    {
        // Кэш рефлексии
        private static readonly Type PcanType = typeof(PCANBasic);
        private static readonly MethodInfo InitializeFdMethod = PcanType.GetMethod("InitializeFD", BindingFlags.Public | BindingFlags.Static);
        private static readonly MethodInfo WriteFdMethod = PcanType.GetMethod("WriteFD", BindingFlags.Public | BindingFlags.Static);
        private static readonly Type MsgFdType = typeof(PCANBasic).Assembly.GetType("Peak.Can.Basic.TPCANMsgFD");

        // Инициализация (classic). Если нужен FD — установите useFd = true.
        public static TPCANStatus Initialize(byte channel, TPCANBaudrate baudrate, bool useFd = false)
        {
            if (useFd && InitializeFdMethod != null)
            {
                try
                {
                    // Вызов InitializeFD(channel, baudrate, ...) — сигнатуры в разных версиях могут отличаться.
                    // Здесь вызываем перегрузку с тремя аргументами, при необходимости адаптируйте параметры.
                    var result = InitializeFdMethod.Invoke(null, new object[] { channel, baudrate, 0u });
                    return (TPCANStatus)result;
                }
                catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
            }

            // fallback — классическая инициализация
            return PCANBasic.Initialize(channel, baudrate);
        }

        public static TPCANStatus Uninitialize(byte channel)
        {
            return PCANBasic.Uninitialize(channel);
        }

        public static TPCANStatus GetValue(byte channel, TPCANParameter prm, out uint value, uint bufferSize)
        {
            return PCANBasic.GetValue(channel, prm, out value, bufferSize);
        }

        public static TPCANStatus SetValue(byte channel, TPCANParameter prm, ref uint value, uint bufferSize)
        {
            return PCANBasic.SetValue(channel, prm, ref value, bufferSize);
        }

        // Классическая запись
        public static TPCANStatus Write(byte channel, ref TPCANMsg msg)
        {
            return PCANBasic.Write(channel, ref msg);
        }

        // Попытка отправки CAN-FD кадра. Если в подключённой PCANBasic нет WriteFD — вернёт NotSupported
        public static TPCANStatus WriteFD(byte channel, uint id, byte[] data, byte dlc, TPCANMessageType msgType)
        {
            if (WriteFdMethod == null || MsgFdType == null)
            {
                return (TPCANStatus)Enum.Parse(typeof(TPCANStatus), "PcanErrorNotSupported", false);
            }

            // Создаём экземпляр TPCANMsgFD через рефлексию и заполняем поля.
            var msgFd = Activator.CreateInstance(MsgFdType);
            // Попробуем установить поля по наиболее вероятным именам — при несоответствии адаптируйте.
            var IdField = MsgFdType.GetField("ID") ?? MsgFdType.GetField("Id");
            var lenField = MsgFdType.GetField("LEN") ?? MsgFdType.GetField("Len") ?? MsgFdType.GetField("DLC");
            var MSGTYPEField = MsgFdType.GetField("MSGTYPE") ?? MsgFdType.GetField("MSGTYPE");
            var dataField = MsgFdType.GetField("DATA") ?? MsgFdType.GetField("Data");

            if (IdField == null || lenField == null || MSGTYPEField == null || dataField == null)
                return (TPCANStatus)Enum.Parse(typeof(TPCANStatus), "PcanErrorNotSupported", false);

            IdField.SetValue(msgFd, id);
            lenField.SetValue(msgFd, dlc);
            MSGTYPEField.SetValue(msgFd, msgType);
            // DATA — обычно массив байт длины 64
            if (data.Length > 64) throw new ArgumentException("FD payload too large (max 64 bytes).");
            // Подготовим массив нужного размера
            var fdDataArr = Array.CreateInstance(typeof(byte), 64);
            Array.Copy(data, 0, fdDataArr, 0, data.Length);
            dataField.SetValue(msgFd, fdDataArr);

            try
            {
                var result = WriteFdMethod.Invoke(null, new object[] { channel, msgFd });
                return (TPCANStatus)result;
            }
            catch (TargetInvocationException tie) { throw tie.InnerException ?? tie; }
        }
    }
}