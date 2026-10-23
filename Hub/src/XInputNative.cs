using System;
using System.Runtime.InteropServices;

namespace GameHub
{
    internal static class XInputNative
    {
        public const int MaxControllers = 4;
        public const ushort GuideButton = 0x0400;
        public const int Success = 0;

        public const byte BatteryDevTypeGamepad = 0;
        public const byte BatteryTypeDisconnected = 0;
        public const byte BatteryTypeWired = 1;
        public const byte BatteryTypeUnknown = 0xFF;

        [StructLayout(LayoutKind.Sequential)]
        public struct GamepadState
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short LeftThumbX;
            public short LeftThumbY;
            public short RightThumbX;
            public short RightThumbY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BatteryInformation
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern int XInputGetStateEx14(int userIndex, out GamepadState state);

        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        private static extern int XInputGetStateEx13(int userIndex, out GamepadState state);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
        private static extern int XInputGetBatteryInformation14(int userIndex, byte deviceType, out BatteryInformation information);

        private static int library = 14;

        public static bool TryGetState(int index, out GamepadState state)
        {
            state = default;
            try
            {
                return (library == 14 ? XInputGetStateEx14(index, out state) : XInputGetStateEx13(index, out state)) == Success;
            }
            catch (DllNotFoundException) when (library == 14)
            {
                library = 13;
                return TryGetState(index, out state);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool TryGetBattery(int index, out BatteryInformation information)
        {
            information = default;
            if (library != 14) return false;
            try
            {
                return XInputGetBatteryInformation14(index, BatteryDevTypeGamepad, out information) == Success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string BatteryLabel(BatteryInformation information)
        {
            if (information.BatteryType == BatteryTypeWired) return "wired";
            if (information.BatteryType == BatteryTypeDisconnected || information.BatteryType == BatteryTypeUnknown) return "unknown";
            return information.BatteryLevel switch
            {
                0 => "empty",
                1 => "low",
                2 => "medium",
                _ => "full",
            };
        }
    }
}
