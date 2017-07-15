using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using Newtonsoft.Json;
using ProcessUtil;

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
    }

    public partial class FlorytService : ServiceBase
    {
        // TODO: Use pubsub
        private const string SERVER_URL = "https://us-central1-floryt-88029.cloudfunctions.net/service";
        private const string WORKING_DIR = @"C:\Program Files\Floryt\";
        private const string UID_GENARETOR_EXECUTABLE_NAME = "ComputerUidGenerator.exe";
        private const string WORKER_EXECUTABLE_NAME = "Executioner.exe";
        private string state;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        public FlorytService()
        {
            InitializeComponent();
            CanHandleSessionChangeEvent = true;

            eventLog = new EventLog();

            if (!EventLog.SourceExists("Floryt"))
            {
                EventLog.CreateEventSource(
                    "Floryt", "Floryt");
            }

            eventLog.Source = "Floryt";
            eventLog.Log = "Floryt";


            // Set up a timer to trigger every half a minute.
            eventLog.WriteEntry("Created timer", EventLogEntryType.Information);
            System.Timers.Timer timer = new System.Timers.Timer { Interval = 5000 };
            timer.Elapsed += SendStatus;
            timer.Start();
        }
        
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {

            eventLog.WriteEntry("SimpleService.OnSessionChange - " +
                                $"Session change notice received: {changeDescription.Reason}\n" +
                                $"Session ID: {changeDescription.SessionId}", EventLogEntryType.Information);

            //global variable to save the current status. every 5 seconds we will check it and determine what to do.
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    eventLog.WriteEntry("logged in", EventLogEntryType.Information);
                    state = "logged in";
                    break;
                case SessionChangeReason.SessionUnlock:
                    eventLog.WriteEntry("unlocked", EventLogEntryType.Information);
                    state = "logged in";
                    break;
                case SessionChangeReason.SessionLogoff:
                    eventLog.WriteEntry("logged out", EventLogEntryType.Information);
                    state = "logged out";
                    break;
                case SessionChangeReason.SessionLock:
                    eventLog.WriteEntry("locked", EventLogEntryType.Information);
                    state = "locked";
                    break;
                case SessionChangeReason.ConsoleConnect:
                    eventLog.WriteEntry("Console connected", EventLogEntryType.Information);
                    state = "logged in";
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

        protected override void OnStart(string[] args)
        {
            // Update the service state to Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                dwWaitHint = 100000
            };
            //informing the SCM the status
            SetServiceStatus(ServiceHandle, ref serviceStatus);
            eventLog.WriteEntry("Service started"); //writes to log
            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(ServiceHandle, ref serviceStatus);
            
            
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("Service stopped");
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            //TODO: add support for power status
            switch (powerStatus)
            {
                case PowerBroadcastStatus.BatteryLow:
                    break;
                case PowerBroadcastStatus.OemEvent:
                    break;
                case PowerBroadcastStatus.PowerStatusChange:
                    break;
                case PowerBroadcastStatus.QuerySuspend:
                    break;
                case PowerBroadcastStatus.QuerySuspendFailed:
                    break;
                case PowerBroadcastStatus.ResumeAutomatic:
                    break;
                case PowerBroadcastStatus.ResumeCritical:
                    break;
                case PowerBroadcastStatus.ResumeSuspend:
                    break;
                case PowerBroadcastStatus.Suspend:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(powerStatus), powerStatus, null);
            }
            return base.OnPowerEvent(powerStatus);
        }

        public void SendStatus(object sender, System.Timers.ElapsedEventArgs args)
        {
            eventLog.WriteEntry("Tick started", EventLogEntryType.Information);
            try {
                dynamic result = SendStatus();
                if (result == null) return;
                if (result.command == null) return;
                
                string message = result.message != null ? $"-m {result.message}" : "";
                
                eventLog.WriteEntry($"Starting process for {result.command}\n-c {result.command} {message}");
                
                ProcessCreator processCreator = new ProcessCreator();
                processCreator.createProcess(WORKING_DIR + WORKER_EXECUTABLE_NAME, $"-c {result.command} {message}");
            } catch (Exception ex) {
                eventLog.WriteEntry($"Tick failed: {ex.Message} {ex.StackTrace}", EventLogEntryType.Error);
            }
            eventLog.WriteEntry("Tick finished", EventLogEntryType.Information);
        }


        private string GetCurrentUser()
        {
            const string filePath = WORKING_DIR + @"current_user.txt"; //TODO: read from registry
            string readText;
            try
            {
                readText = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                eventLog.WriteEntry($"Failed to read user: {ex.Message}", EventLogEntryType.FailureAudit);
                readText = "no user";
            }
            return readText;
        }

        /// <summary>
        /// Method which sends the session status to the server. </summary>
        /// <returns>
        /// Return result from the server which represents a command to execute on the session.</returns>
        public dynamic SendStatus()
        {
            string computerUid = CalculateComputerUid();
            string email = GetCurrentUser();
            Dictionary<string, string> metadata =
                new Dictionary<string, string> { { "computerUid", computerUid }, { "status", state }, { "user", email } };
            string postData = JsonConvert.SerializeObject(metadata);
            eventLog.WriteEntry($"Created metadata: {postData}", EventLogEntryType.Information);

            WebRequest request = WebRequest.Create(SERVER_URL);
            request.Method = "POST";
            request.ContentType = "application/json";
            
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse response;
            try
            {
                response = request.GetResponse();
            }
            catch (Exception e)
            {
                eventLog.WriteEntry($"Failed to get response: {e.Message}", EventLogEntryType.FailureAudit);
                return null;
            }

            if (((HttpWebResponse)response).StatusDescription != "OK") return null;
            // Get the stream containing content returned by the server.
            dataStream = response.GetResponseStream();
            // Open the stream using a StreamReader for easy access.
            if (dataStream == null) return null;
            StreamReader reader = new StreamReader(dataStream);
            // Read the content.
            string responseFromServer = reader.ReadToEnd();
            reader.Close();
            dataStream.Close();
            response.Close();

            eventLog.WriteEntry("response: " + (responseFromServer.Length == 0 ? "TTML" : responseFromServer), EventLogEntryType.Information);

            dynamic result = JsonConvert.DeserializeObject(responseFromServer);
            return result;
        }

        private static string CalculateComputerUid()
        {
            Process pProcess =
                new Process
                {
                    StartInfo =
                    {
                        FileName = WORKING_DIR + UID_GENARETOR_EXECUTABLE_NAME,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
            pProcess.Start();

            string strUid = pProcess.StandardOutput.ReadToEnd();
            strUid = strUid.Replace("\r\n", string.Empty);
            
            return strUid;
        }


        protected override void OnShutdown()
        {
            state = "shutdown";
            string computerUid = CalculateComputerUid();
            string email = GetCurrentUser();
            Dictionary<string, string> metadata =
                new Dictionary<string, string> { { "computerUid", computerUid }, { "status", state }, { "user", email } };
            string postData = JsonConvert.SerializeObject(metadata);
            eventLog.WriteEntry($"Created metadata: {postData}", EventLogEntryType.Information);

            WebRequest request = WebRequest.Create(SERVER_URL);
            request.Method = "POST";
            request.ContentType = "application/json";
            
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
        }
    }
}
