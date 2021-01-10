using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace YourNamespace
{
    public enum IOMode : byte
    {
        recv = 0x01,
        write = 0x02,
        error = 0x04,
        timeout = 0x08
    }

    internal struct timeval
    {
        public int tv_sec;
        public int tv_usec;

        public timeval(long ms)
        {
            tv_sec = (int)(ms / 1000000L);
            tv_usec = (int)(ms % 1000000L);
        }
    }

    public interface IEventLoop
    {
        void EventProcess(IOMode mode);
    }

    public static class EventLoop
    {
        //fileno=>socket
        private static Hashtable RecvList;
        private static Hashtable WriteList;
        private static Hashtable ErrorList;
        private static readonly Dictionary<Socket, Action<IOMode>> CallbackDict;
        private static int _timeout_interval = 2000;
        private static int _last_time;
        private static bool _stop;

        /// <summary>
        /// Socket事件循环监视器,基于Select API
        /// </summary>
        static EventLoop()
        {
            RecvList = new Hashtable();
            WriteList = new Hashtable();
            ErrorList = new Hashtable();
            CallbackDict = new Dictionary<Socket, Action<IOMode>>();
        }

        /// <summary>
        /// 注册所需监视的Socket对象
        /// </summary>
        /// <param name="mode">需要监视的事件</param>
        /// <param name="sock">需要监视的Socket</param>
        /// <param name="func">监视的Socket发生监视的事件时的回调方法</param>
        public static void Register(IOMode mode, Socket sock, Action<IOMode> func)
        {
            if ((mode & IOMode.recv) > 0)
            {
                if (!RecvList.Contains(sock.Handle))
                    RecvList.Add(sock.Handle, sock);
            }
            if ((mode & IOMode.write) > 0)
            {
                if (!WriteList.Contains(sock.Handle))
                    WriteList.Add(sock.Handle, sock);
            }
            if ((mode & IOMode.error) > 0)
            {
                if (!ErrorList.Contains(sock.Handle))
                    ErrorList.Add(sock.Handle, sock);
            }
            try
            {
                CallbackDict.Add(sock, func);
            }
            catch
            {
                CallbackDict[sock] = func;
            }
        }

        /// <summary>
        /// 注销所需监视的Socket
        /// </summary>
        /// <param name="sock">需要注销的Socket</param>
        public static void Unregister(Socket sock)
        {
            if (RecvList.Contains(sock.Handle))
                RecvList.Remove(sock.Handle);
            if (WriteList.Contains(sock.Handle))
                WriteList.Remove(sock.Handle);
            if (ErrorList.Contains(sock.Handle))
                ErrorList.Remove(sock.Handle);
            if (CallbackDict.ContainsKey(sock))
                CallbackDict.Remove(sock);
        }

        /// <summary>
        /// 修改需要监视的Socket对应的事件和回调方法
        /// </summary>
        /// <param name="mode">需要监视的事件</param>
        /// <param name="sock">需要监视的Socket</param>
        /// <param name="func">监视的Socket发生监视的事件时的回调方法</param>
        public static void Modify(IOMode mode, Socket sock, Action<IOMode> func)
        {
            Unregister(sock);
            Register(mode, sock, func);
        }

        /// <summary>
        /// 循环监视已注册的Socket,等待这些Socket发生已注册的事件
        /// </summary>
        /// <param name="interval">微秒</param>
        public static void Run(long interval)
        {
            _stop = false;
            _last_time = Environment.TickCount;
            while (true)
            {
                if (_stop)
                    return;
                IntPtr[] rlist = SocketSetToIntPtrArray(RecvList);
                IntPtr[] wlist = SocketSetToIntPtrArray(WriteList);
                IntPtr[] xlist = SocketSetToIntPtrArray(ErrorList);
                timeval tv = new timeval(interval);
                int res;
                if (interval > 0)
                    res = select(0, rlist, wlist, xlist, ref tv);
                else
                    res = select(0, rlist, wlist, xlist, IntPtr.Zero);
                if (res > 0)
                {   //rlist、wlist、xlist第一个元素表示文件描述符数量
                    bool _first = true;
                    foreach (IntPtr ptr in rlist)
                    {
                        if (!_first)
                            CallbackDict[(Socket)RecvList[ptr]](IOMode.recv);
                        else
                            _first = false;
                    }
                    _first = true;
                    foreach (IntPtr ptr in wlist)
                    {
                        if (!_first)
                            CallbackDict[(Socket)WriteList[ptr]](IOMode.write);
                        else
                            _first = false;
                    }
                    _first = true;
                    foreach (IntPtr ptr in xlist)
                    {
                        if (!_first)
                            CallbackDict[(Socket)ErrorList[ptr]](IOMode.error);
                        else
                            _first = false;
                    }
                }
                else if (res == 0)
                {
                    //Logging.Error("SELECT调用超时");
                }
                else
                {
                    //Logging.Error($"SELECT调用错误:{GetErrorMsg(GetLastError())}");
                    continue;
                }
                //定时对所有回调方法触发超时事件,具体有没有超时需自行处理
                if (Environment.TickCount - _last_time > _timeout_interval)
                {
                    foreach (Action<IOMode> func in CallbackDict.Values)
                        func(IOMode.timeout);
                    _last_time = Environment.TickCount;
                }
            }
        }

        internal static IntPtr[] SocketSetToIntPtrArray(Hashtable SocketList)
        {
            if (SocketList == null || SocketList.Count < 1)
                return null;
            IntPtr[] array = new IntPtr[SocketList.Count + 1];
            array[0] = (IntPtr)SocketList.Count;
            int i = 0;
            foreach (IntPtr ptr in SocketList.Keys)
            {
                array[i + 1] = ptr;
                i += 1;
            }
            return array;
        }

        internal const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        internal const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        internal const int FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;

        internal static string GetErrorMsg(int code)
        {
            int len = FormatMessage(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_ALLOCATE_BUFFER, IntPtr.Zero, code, 0, out string describe, 255, IntPtr.Zero);
            if (len > 0)
                return describe;
            return null;
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern int select([In] int ignoredParam, [In] [Out] IntPtr[] readfds, [In] [Out] IntPtr[] writefds, [In] [Out] IntPtr[] exceptfds, [In] ref timeval timeout);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern int select([In] int ignoredParam, [In] [Out] IntPtr[] readfds, [In] [Out] IntPtr[] writefds, [In] [Out] IntPtr[] exceptfds, [In] IntPtr nullTimeout);

        /// <summary>
        /// 获取因调用API产生的错误代码
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern int GetLastError();

        /// <summary>
        /// 根据错误代码返回错误描述信息
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern int FormatMessage(int flag, IntPtr source, int msgid, int langid, out string buf, int size, IntPtr args);
    }
}
