using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace FlorytService
{
    //declare the ServiceState values
    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007
    }

    // a structure for the status
    [StructLayout(LayoutKind.Sequential)] //rewriting from another language (c for example) to c# (?) forcing to be arranged differntly in memory
    public struct ServiceStatus
    {
        public long dwServiceType;
        public ServiceState dwCurrentState;
        public long dwControlsAccepted;
        public long dwWin32ExitCode;
        public long dwServiceSpecificExitCode;
        public long dwCheckPoint;
        public long dwWaitHint;
    };

    public partial class FlorytService : ServiceBase
    {
        private const string SERVER_URL = "https://us-central1-floryt-88029.cloudfunctions.net/service";
        private EventLog eventLog; // a declaration for the eventLog
        private SessionChangeReason state;

        //calling a function from a DLL
        //(SetServiceStatus function -> Updates the service control manager's status information for the calling service)
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        public FlorytService()
        {
            InitializeComponent();
            CanHandleSessionChangeEvent = true;

            eventLog = new EventLog();

            if (!EventLog.SourceExists("MySource"))
            {
                EventLog.CreateEventSource(
                    "Floryt Service", "Floryt");
            }

            eventLog.Source = "Floryt Service";
            eventLog.Log = "Floryt";


            // Set up a timer to trigger every half a minute.
            eventLog.WriteEntry("Created timer", EventLogEntryType.Information);
            System.Timers.Timer timer = new System.Timers.Timer { Interval = 5000 };
            timer.Elapsed += OnTimer;
            timer.Start();
        }

        protected override void OnStart(string[] args)
        {
            //--------Update the service state to Start Pending. (first - pending, then run)
            //creating the status
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                dwWaitHint = 100000
            };
            //informing the SCM the status
            SetServiceStatus(ServiceHandle, ref serviceStatus);


            eventLog.WriteEntry("Started service"); //writes to log


            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("Stopped service");
        }


        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            eventLog.WriteEntry("Tick", EventLogEntryType.Information);
            try
            {
                SendStatus();
                //ProcessUtil processUtil = new ProcessUtil();
                //processUtil.createProcess();

            }
            catch (Exception ex)
            {
                eventLog.WriteEntry("exception in Tick " + ex.Message + ex.StackTrace, EventLogEntryType.Error);
            }
            eventLog.WriteEntry("Tick3 ended", EventLogEntryType.Information);
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {

            EventLog.WriteEntry("SimpleService.OnSessionChange", DateTime.Now.ToLongTimeString() +
                " - Session change notice received: " +
                changeDescription.Reason + "  Session ID: " +
                changeDescription.SessionId);

            //global variable to save the current status. every 5 seconds we will check it and determine what to do.
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    eventLog.WriteEntry("logged in", EventLogEntryType.Information);
                    state = SessionChangeReason.SessionLogon;
                    break;

                case SessionChangeReason.SessionUnlock:
                    eventLog.WriteEntry("unlocked", EventLogEntryType.Information);
                    state = SessionChangeReason.SessionUnlock;
                    break;

                case SessionChangeReason.SessionLogoff:
                    eventLog.WriteEntry("logged out", EventLogEntryType.Information);
                    state = SessionChangeReason.SessionLogoff;
                    break;

                case SessionChangeReason.SessionLock:
                    eventLog.WriteEntry("locked", EventLogEntryType.Information);
                    state = SessionChangeReason.SessionLock;
                    break;
                case SessionChangeReason.ConsoleConnect:
                    break;
                case SessionChangeReason.ConsoleDisconnect:
                    break;
                case SessionChangeReason.RemoteConnect:
                    break;
                case SessionChangeReason.RemoteDisconnect:
                    break;
                case SessionChangeReason.SessionRemoteControl:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string GetCurrentUser()
        {
            const string filePath = @"C:\Program Files\Floryt\current_user.txt";
            string readText;
            try
            {
                readText = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                readText = "no user";
            }

            return readText;
        }

        /// <summary>
        /// Method which sends the session status to the server. </summary>
        /// <returns>
        /// Return result from the server which represents a command to execute on the session.</returns>
        public void SendStatus()
        {
            string computerUid = CalculateComputerUid();
            WebRequest request = WebRequest.Create(SERVER_URL);
            request.Method = "POST";
            string status = GetStatus();
            string email = GetCurrentUser();
            Dictionary<string, string> metadata =
                new Dictionary<string, string> { { "computerUid", computerUid }, { "status", status }, { "user", email } };

            string postData = "{" + $"{metadata.Select(data => $"{data.Key}: {data.Value}")}" + "}";
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();
            // Display the status.

            if (((HttpWebResponse)response).StatusDescription != "OK") return;
            // Get the stream containing content returned by the server.
            dataStream = response.GetResponseStream();
            // Open the stream using a StreamReader for easy access.
            if (dataStream == null) return;
            StreamReader reader = new StreamReader(dataStream);
            // Read the content.
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            dataStream.Close();
            response.Close();

            eventLog.WriteEntry("response: " + responseFromServer, EventLogEntryType.Information);

            //dynamic array = JsonConvert.DeserializeObject(responseFromServer);

            //switch (array.command)
            //{
            //    case "lock":
            //        if (state != SessionChangeReason.SessionLogoff || state != SessionChangeReason.SessionLock)
            //        {
            //        }
            //        break;
            //    case "shutdown":
            //        break;
            //    case "present_message":
            //        break;
            //    case "take_screenshot":
            //        break;
            //}
        }

        private string GetStatus()
        {
            switch (state)
            {
                case SessionChangeReason.ConsoleConnect:
                    return "logged in";
                case SessionChangeReason.ConsoleDisconnect:
                    return "logged out";
                case SessionChangeReason.SessionLogon:
                    break;
                case SessionChangeReason.SessionLogoff:
                    break;
                case SessionChangeReason.SessionLock:
                    break;
                case SessionChangeReason.SessionUnlock:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return "unknown";
        }

        private static string CalculateComputerUid()
        {
            //Create process
            Process pProcess =
                new Process
                {
                    StartInfo =
                    {
                        FileName = @"C:\Users\User\Floryt\find_id.exe", //TODO: check if it can be dll or resource
                        Arguments = null, //TODO: check if statment is redundant
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
            pProcess.Start();

            string strUid = pProcess.StandardOutput.ReadToEnd();
            strUid = strUid.Remove(strUid.Length - 2);
            pProcess.WaitForExit(); //TODO: check if statment is redundant
            /////////////////////////////////
            byte[] myResBytes;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("UidCalculator.exe"))
            {
                byte[] buffer = new byte[1024];
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        int read = s.Read(buffer, 0, buffer.Length);
                        if (read <= 0)
                        {
                            myResBytes = ms.ToArray();
                            break;
                        }
                        ms.Write(buffer, 0, read);
                    }
                }
            }
            Assembly asm = Assembly.Load(myResBytes);
            // search for the Entry Point
            MethodInfo method = asm.EntryPoint;
            if (method == null) throw new NotSupportedException();
            // create an instance of the Startup form Main method
            object o = asm.CreateInstance(method.Name);
            // invoke the application starting point
            method.Invoke(o, null);

            return strUid;
        }


        protected override void OnShutdown()
        {
            //TODO: update status
        }
    }
}
