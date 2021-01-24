using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Project
{
    public enum IOMode : byte
    {
        recv = 0x01,
        send = 0x02,
        error = 0x04
    }

    public interface IEventLoop
    {
        /// <summary>
        /// Socket事件处理方法
        /// </summary>
        void EventProcess(Socket sock, IOMode mode);

        /// <summary>
        /// 定期执行的检查方法
        /// </summary>
        void PeriodicHandle();
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

    /// <summary>
    /// 基于上古时期出现的SELECT多路复用技术实现的Socket事件循环库
    /// 没有C/C++网络编程经验的人不建议使用,否则容易引发类癫痫的症状
    /// 根据各种评测,在托管的Socket文件描述符不超128个的情况下SELECT最优
    /// </summary>
    public static class EventLoop
    {
        /// <summary>
        /// fileno=>socket
        /// </summary>
        private static readonly Hashtable RecvList;

        /// <summary>
        /// fileno=>socket
        /// </summary>
        private static readonly Hashtable SendList;

        /// <summary>
        /// fileno=>socket
        /// </summary>
        private static readonly Hashtable ErrorList;

        /// <summary>
        /// socket=>event_proc
        /// </summary>
        private static readonly Dictionary<Socket, Action<Socket,IOMode>> EventCallback;

        private static readonly HashSet<Action> CheckCallback;
        private static int _timeout_interval = 2000;//毫秒
        private static int _last_time; //毫秒
        private static bool _stopped;

        /// <summary>
        /// Socket事件循环监视器,基于系统Select调用
        /// </summary>
        static EventLoop()
        {
            RecvList = new Hashtable();
            SendList = new Hashtable();
            ErrorList = new Hashtable();
            EventCallback = new Dictionary<Socket, Action<Socket,IOMode>>();
            CheckCallback = new HashSet<Action>();
        }

        /// <summary>
        /// 注册所需监视的Socket对象
        /// </summary>
        /// <param name="mode">需要监视的事件</param>
        /// <param name="sock">需要监视的Socket</param>
        /// <param name="func">监视的Socket发生监视的事件时的回调方法</param>
        public static void Register(IOMode mode, Socket sock, Action<Socket,IOMode> func)
        {
            if ((mode & IOMode.recv) > 0)
            {
                if (!RecvList.Contains(sock.Handle))
                    RecvList.Add(sock.Handle, sock);
            }
            if ((mode & IOMode.send) > 0)
            {
                if (!SendList.Contains(sock.Handle))
                    SendList.Add(sock.Handle, sock);
            }
            if ((mode & IOMode.error) > 0)
            {
                if (!ErrorList.Contains(sock.Handle))
                    ErrorList.Add(sock.Handle, sock);
            }
            try
            {
                EventCallback.Add(sock, func);
            }
            catch
            {
                EventCallback[sock] = func;
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
            if (SendList.Contains(sock.Handle))
                SendList.Remove(sock.Handle);
            if (ErrorList.Contains(sock.Handle))
                ErrorList.Remove(sock.Handle);
            if (EventCallback.ContainsKey(sock))
                EventCallback.Remove(sock);
        }

        /// <summary>
        /// 修改需要监视的Socket对应的事件和回调方法
        /// </summary>
        /// <param name="mode">需要监视的事件</param>
        /// <param name="sock">需要监视的Socket</param>
        /// <param name="func">监视的Socket发生监视的事件时的回调方法</param>
        public static void Modify(IOMode mode, Socket sock, Action<Socket,IOMode> func)
        {
            Unregister(sock);
            Register(mode, sock, func);
        }

        /// <summary>
        /// 添加需要定期执行的方法
        /// </summary>
        public static void AddCheckFunc(Action func)
        {
            if (!CheckCallback.Contains(func))
                CheckCallback.Add(func);
        }

        /// <summary>
        /// 移除需要定期执行的方法
        /// </summary>
        public static void RemoveCheckFunc(Action func)
        {
            if (CheckCallback.Contains(func))
                CheckCallback.Remove(func);
        }

        /// <summary>
        /// 循环监视已注册的Socket,等待这些Socket发生已注册的事件
        /// </summary>
        /// <param name="interval">微秒</param>
        public static void Run(long interval)
        {
            _stopped = false;
            _last_time = Environment.TickCount;
            timeval tv = new timeval(interval);
            while (true)
            {
                if (_stopped)
                {
                    RecvList.Clear();
                    SendList.Clear();
                    ErrorList.Clear();
                    EventCallback.Clear();
                    CheckCallback.Clear();
                    _last_time = Environment.TickCount;
                    return;
                }
                IntPtr[] rlist = SocketSetToIntPtrArray(RecvList);
                IntPtr[] slist = SocketSetToIntPtrArray(SendList);
                IntPtr[] elist = SocketSetToIntPtrArray(ErrorList);
                int res;
                if (interval > 0)
                    res = select(0, rlist, slist, elist, ref tv);
                else
                    res = select(0, rlist, slist, elist, IntPtr.Zero);
                if (res > 0)
                {   //rlist、wlist、xlist第一个元素表示文件描述符数量
                    bool _first = true;
                    foreach (IntPtr ptr in rlist)
                    {
                        if (!_first)
                            EventCallback[(Socket)RecvList[ptr]]((Socket)RecvList[ptr], IOMode.recv);
                        else
                            _first = false;
                    }
                    _first = true;
                    foreach (IntPtr ptr in slist)
                    {
                        if (!_first)
                            EventCallback[(Socket)SendList[ptr]]((Socket)SendList[ptr],IOMode.send);
                        else
                            _first = false;
                    }
                    _first = true;
                    foreach (IntPtr ptr in elist)
                    {
                        if (!_first)
                            EventCallback[(Socket)ErrorList[ptr]]((Socket)ErrorList[ptr],IOMode.error);
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
                //定时触发需要定期执行的方法
                if (Environment.TickCount - _last_time >= _timeout_interval)
                {
                    foreach (Action func in CheckCallback)
                        func();
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
                return describe.Remove(len);
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
