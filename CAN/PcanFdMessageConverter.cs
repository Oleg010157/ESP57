using System;
using Peak.Can.Basic;

namespace ICDIBasic
{
    internal static class PcanFdMessageConverter
    {
        internal static TPCANMsgFD ToFd(TPCANMsg message)
        {
            var payload = new byte[64];
            var source = message.DATA ?? Array.Empty<byte>();
            var length = Math.Min(source.Length, payload.Length);

            Array.Copy(source, payload, length);

            return new TPCANMsgFD
            {
                ID = message.ID,
                MSGTYPE = message.MSGTYPE | TPCANMessageType.PCAN_MESSAGE_FD,
                DLC = LengthToDlc(message.LEN),
                DATA = payload
            };
        }

        internal static TPCANMsg ToClassic(TPCANMsgFD message)
        {
            var payloadLength = Math.Min((int)DlcToLength(message.DLC), 8);
            var payload = new byte[payloadLength];
            var source = message.DATA ?? Array.Empty<byte>();
            var length = Math.Min(source.Length, payload.Length);

            Array.Copy(source, payload, length);

            return new TPCANMsg
            {
                ID = message.ID,
                MSGTYPE = message.MSGTYPE,
                LEN = (byte)payloadLength,
                DATA = payload
            };
        }

        internal static byte LengthToDlc(byte length)
        {
            switch (length)
            {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 8: return length;
                case 9: case 10: case 11: case 12: return 9;
                case 13: case 14: case 15: case 16: return 10;
                case 17: case 18: case 19: case 20: return 11;
                case 21: case 22: case 23: case 24: return 12;
                case 25: case 26: case 27: case 28: case 29: case 30: case 31: case 32: return 13;
                case 33: case 34: case 35: case 36: case 37: case 38: case 39: case 40: case 41: case 42: case 43: case 44: case 45: case 46: case 47: case 48: return 14;
                default: return 15;
            }
        }

        internal static byte DlcToLength(byte dlc)
        {
            switch (dlc)
            {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 8: return dlc;
                case 9: return 12;
                case 10: return 16;
                case 11: return 20;
                case 12: return 24;
                case 13: return 32;
                case 14: return 48;
                default: return 64;
            }
        }
    }
}