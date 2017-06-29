using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProcessUtil
{
    class ProcessUtil
    {
        [DllImport("wtsapi32.dll")]
        static extern Int32 WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] Int32 Reserved,
            [MarshalAs(UnmanagedType.U4)] Int32 Version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref Int32 pCount);

        [DllImport("wtsapi32.dll")]
        static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] String pServerName);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private struct WTS_SESSION_INFO
        {
            public Int32 SessionID;

            [MarshalAs(UnmanagedType.LPStr)] public String pWinStationName;

            public WTS_CONNECTSTATE_CLASS State;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public Int32 dwProcessID;
            public Int32 dwThreadID;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public Int32 Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        public enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        public enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }



        [
            DllImport("advapi32.dll",
                EntryPoint = "CreateProcessAsUser", SetLastError = true,
                CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)
        ]
        public static extern bool
            CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine,
                ref SECURITY_ATTRIBUTES lpProcessAttributes, ref SECURITY_ATTRIBUTES lpThreadAttributes,
                bool bInheritHandle, Int32 dwCreationFlags, IntPtr lpEnvrionment,
                string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
                ref PROCESS_INFORMATION lpProcessInformation);

        public static IntPtr OpenServer(String Name)
        {
            IntPtr server = WTSOpenServer(Name);
            return server;
        }

        public void createProcess()
        {
            IntPtr server = IntPtr.Zero;
            IntPtr token = IntPtr.Zero;

            server = OpenServer("localhost");

            IntPtr ppSessionInfo = IntPtr.Zero;

            Int32 count = 0;
            //gets the session ID
            Int32 retval = WTSEnumerateSessions(server, 0, 1, ref ppSessionInfo, ref count);
            SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            STARTUPINFO si = new STARTUPINFO();

            foreach (UInt32 sessionID in ListSessions())
            {
                bool gotToken = WTSQueryUserToken(sessionID, out token);
                CreateProcessAsUser(token, @"C:\windows\notepad.exe", String.Empty, ref sa, ref sa,
                    false, 0, IntPtr.Zero, @"c:\", ref si, ref pi);
            }
        }



        public static List<UInt32> ListSessions()
        {
            IntPtr server = IntPtr.Zero;
            List<UInt32> ret = new List<UInt32>();
            server = OpenServer("localhost");

            try
            {
                IntPtr ppSessionInfo = IntPtr.Zero;

                Int32 count = 0;
                Int32 retval = WTSEnumerateSessions(server, 0, 1, ref ppSessionInfo, ref count);
                Int32 dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

                Int32 current = (int)ppSessionInfo;

                if (retval != 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        WTS_SESSION_INFO si =
                            (WTS_SESSION_INFO)Marshal.PtrToStructure((System.IntPtr)current,
                                typeof(WTS_SESSION_INFO));
                        current += dataSize;
                        if (si.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                        {
                            ret.Add((UInt32)si.SessionID);

                        }

                    }
                }
            }
            return ret;
        }

    }
}
