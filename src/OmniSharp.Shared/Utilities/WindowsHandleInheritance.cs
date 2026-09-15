using System;
using System.Runtime.InteropServices;

namespace OmniSharp.Utilities
{
    public static class WindowsHandleInheritance
    {
        private const int StandardInputHandle = -10;
        private const int StandardOutputHandle = -11;
        private const int StandardErrorHandle = -12;
        private const uint HandleFlagInherit = 0x00000001;
        private static readonly IntPtr InvalidHandleValue = new(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int standardHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

        public static void DisableStandardHandleInheritance()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            DisableInheritance(StandardInputHandle);
            DisableInheritance(StandardOutputHandle);
            DisableInheritance(StandardErrorHandle);
        }

        private static void DisableInheritance(int standardHandle)
        {
            var handle = GetStdHandle(standardHandle);
            if (handle != IntPtr.Zero && handle != InvalidHandleValue)
            {
                _ = SetHandleInformation(handle, HandleFlagInherit, flags: 0);
            }
        }
    }
}
