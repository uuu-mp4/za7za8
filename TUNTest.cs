using System;
using System.IO;
using Microsoft.Win32;
using System.Threading;
using System.Runtime.InteropServices;

namespace TestTun
{
    class TunTap
    {
        private const uint METHOD_BUFFERED = 0;
        private const uint FILE_ANY_ACCESS = 0;
        private const uint FILE_DEVICE_UNKNOWN = 0x00000022;
        private const int FILE_ATTRIBUTE_SYSTEM = 0x4;
        private const int FILE_FLAG_OVERLAPPED = 0x40000000;

        static FileStream Tap;
        static EventWaitHandle WaitObject, WaitObject2;
        static int BytesRead;

        static void Main(string[] args)
        {
            const string DeviceSpace = "\\\\.\\Global\\";
            string devGuid = GetDeviceGuid();
            IntPtr ptr = CreateFile(DeviceSpace + devGuid + ".tap", FileAccess.ReadWrite, FileShare.ReadWrite,
                0, FileMode.Open, FILE_ATTRIBUTE_SYSTEM | FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            int len;
            IntPtr pstatus = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(pstatus, 1);
            DeviceIoControl(ptr, TAP_CONTROL_CODE(6, METHOD_BUFFERED)/*TAP_IOCTL_SET_MEDIA_STATUS*/, pstatus, 4, pstatus, 4, out len, IntPtr.Zero);
            IntPtr ptun = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(ptun, 0, 0x0100030a);
            Marshal.WriteInt32(ptun, 4, 0x0000030a);
            Marshal.WriteInt32(ptun, 8, unchecked((int)0x00ffffff));
            DeviceIoControl(ptr, TAP_CONTROL_CODE(10, METHOD_BUFFERED)/*TAP_IOCTL_CONFIG_TUN*/, ptun, 12, ptun, 12, out len, IntPtr.Zero);
            Tap = new FileStream(ptr, FileAccess.ReadWrite, true, 10000, true);
            byte[] buf = new byte[10000];
            object state = new int();
            object state2 = new int();
            WaitObject = new EventWaitHandle(false, EventResetMode.AutoReset);
            WaitObject2 = new EventWaitHandle(false, EventResetMode.AutoReset);
            AsyncCallback readCallback = new AsyncCallback(ReadDataCallback);
            AsyncCallback writeCallback = new AsyncCallback(WriteDataCallback);
            IAsyncResult res, res2;
            while (true)
            {
                res = Tap.BeginRead(buf, 0, 10000, readCallback, state);
                WaitObject.WaitOne();
                for (int i = 0; i < 4; ++i)
                {
                    byte tmp = buf[12 + i];
                    buf[12 + i] = buf[16 + i];
                    buf[16 + i] = tmp;
                }
                res2 = Tap.BeginWrite(buf, 0, BytesRead, writeCallback, state2);
                WaitObject2.WaitOne();
            }
        }

        public static void WriteDataCallback(IAsyncResult asyncResult)
        {
            Tap.EndWrite(asyncResult);
            WaitObject2.Set();
        }

        public static void ReadDataCallback(IAsyncResult asyncResult)
        {
            BytesRead = Tap.EndRead(asyncResult);
            WaitObject.Set();
        }

        static string GetDeviceGuid()
        {
            const string AdapterKey = "SYSTEM\\CurrentControlSet\\Control\\Class\\{4D36E972-E325-11CE-BFC1-08002BE10318}";
            RegistryKey regAdapters = Registry.LocalMachine.OpenSubKey(AdapterKey, true);
            string[] keyNames = regAdapters.GetSubKeyNames();
            string devGuid = "";
            foreach (string x in keyNames)
            {
                RegistryKey regAdapter = regAdapters.OpenSubKey(x);
                object id = regAdapter.GetValue("ComponentId");
                if (id != null && id.ToString() == "tap0801") devGuid = regAdapter.GetValue("NetCfgInstanceId").ToString();
            }
            return devGuid;
        }

        static uint TAP_CONTROL_CODE(uint request, uint method)
        {
            return CTL_CODE(FILE_DEVICE_UNKNOWN, request, method, FILE_ANY_ACCESS);
        }

        private static uint CTL_CODE(uint DeviceType, uint Function, uint Method, uint Access)
        {
            return ((DeviceType << 16) | (Access << 14) | (Function << 2) | Method);
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(
            string filename,
            [MarshalAs(UnmanagedType.U4)]FileAccess fileaccess,
            [MarshalAs(UnmanagedType.U4)]FileShare fileshare,
            int securityattributes,
            [MarshalAs(UnmanagedType.U4)]FileMode creationdisposition,
            int flags,
            IntPtr template
            );

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped
            );
    }
}