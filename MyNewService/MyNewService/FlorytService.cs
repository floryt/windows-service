using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
//me: for more types of statuses to inform to SCM (Service Control Manager)
using System.Runtime.InteropServices;
using System.Net;
using System.IO;
using Newtonsoft.Json;

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
        SERVICE_PAUSED = 0x00000007,
    }

    public enum ComputerState
    {
        LOGGED_IN,
        LOGGED_OUT,
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

    //NOTE - [----] is an invoke call. This way we can call a function from a DLL or change the physical layot of a stracture or a class (see above)

    public partial class FlorytSrvs : ServiceBase
    {

        private System.ComponentModel.IContainer components;
        private System.Diagnostics.EventLog eventLog2; // a declaration for the eventLog
        private ComputerState state;

        //calling a function from a DLL
        //(SetServiceStatus function -> Updates the service control manager's status information for the calling service)
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);

        public FlorytSrvs()
        {
            InitializeComponent();
            CanHandleSessionChangeEvent = true;

            eventLog2 = new System.Diagnostics.EventLog();

            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "MySource", "MyNewLog");
            }

            eventLog2.Source = "MySource";
            eventLog2.Log = "MyNewLog";


            // Set up a timer to trigger every half a minute.
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 10000; // 10 seconds
            timer.Elapsed += new System.Timers.ElapsedEventHandler(this.OnTimer); //giving the function the timer does
            timer.Start();
        }

        protected override void OnStart(string[] args)
        {
            //--------Update the service state to Start Pending. (first - pending, then run)
            //creating the status
            ServiceStatus serviceStatus = new ServiceStatus();
            serviceStatus.dwCurrentState = ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            //informing the SCM the status
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);


            eventLog2.WriteEntry("In OnStart"); //writes to log


            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        protected override void OnStop()
        {
            eventLog2.WriteEntry("In onStop.");
        }


        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        { 

            talkToServer();
        }

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            
            EventLog.WriteEntry("SimpleService.OnSessionChange", DateTime.Now.ToLongTimeString() +
                " - Session change notice received: " +
                changeDescription.Reason.ToString() + "  Session ID: " +
                changeDescription.SessionId.ToString());

            //global variable to save the current status. every 5 seconds we will check it and determine what to do.
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    eventLog2.WriteEntry("logged in", EventLogEntryType.Information);
                    this.state = ComputerState.LOGGED_IN;
                    break;

                case SessionChangeReason.SessionLogoff:
                    eventLog2.WriteEntry("logged out", EventLogEntryType.Information);
                    this.state = ComputerState.LOGGED_OUT;
                    break;
                case SessionChangeReason.SessionLock:
                    eventLog2.WriteEntry("loecked", EventLogEntryType.Information);
                    this.state = ComputerState.LOGGED_OUT;
                    break;
                case SessionChangeReason.SessionUnlock:
                    eventLog2.WriteEntry("unlocked", EventLogEntryType.Information);
                    this.state = ComputerState.LOGGED_IN;
                    break;
            }
        }

        private string GetCurrentUser()
        {
            string file_path = @"C:\Users\User\Desktop\current_user.txt";//@ is for no special chars
                                                                    //TODO: check if file exists
            string readText = "";
            try
            {
                readText = File.ReadAllText(file_path);
            }
            catch (Exception ex)
            {
                readText = "no user";
            }

            return readText;
        }

        private void talkToServer()
        {
            string strUID = getUID();

            //---http
            // Create a request using a URL that can receive a post. 
            WebRequest request = WebRequest.Create("https://us-central1-floryt-88029.cloudfunctions.net/service");
            // Set the Method property of the request to POST.
            request.Method = "POST";
            // Create POST data and convert it to a byte array.
            string status = GetStatus();
            string email = GetCurrentUser();
            string postData = "{\"computerUid\":\"" + strUID + "\", \"status\":\"" + status + "\", \"email\":\"" + email + "\"}";
            //Console.WriteLine(postData);
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            // Set the ContentType property of the WebRequest.
            request.ContentType = "application/json";
            // Set the ContentLength property of the WebRequest.
            request.ContentLength = byteArray.Length;
            // Get the request stream.
            Stream dataStream = request.GetRequestStream();
            // Write the data to the request stream.
            dataStream.Write(byteArray, 0, byteArray.Length);
            // Close the Stream object.
            dataStream.Close();
            // Get the response.
            WebResponse response = request.GetResponse();
            // Display the status.

            if (((HttpWebResponse)response).StatusDescription == "OK")
            {
                // Get the stream containing content returned by the server.
                dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.
                string responseFromServer = reader.ReadToEnd();                                                
                reader.Close();
                dataStream.Close();
                response.Close();

                eventLog2.WriteEntry("response: " + responseFromServer, EventLogEntryType.Information);

                dynamic array = JsonConvert.DeserializeObject(responseFromServer);
                string command = array.command;
                switch (command)
                {
                    case "lock":
                        eventLog2.WriteEntry("requested to lock", EventLogEntryType.Information);
                        if (this.state != ComputerState.LOGGED_OUT)
                        {
                            eventLog2.WriteEntry("locking", EventLogEntryType.Information);
                            //LockWorkStation();
                            WriteToFile("lock");
                        }
                        break;
                    case "shutdown":
                        eventLog2.WriteEntry("requested to shutdown", EventLogEntryType.Information);
                        {
                            eventLog2.WriteEntry("shutdown", EventLogEntryType.Information);
                            //LockWorkStation();
                            WriteToFile("shutdown");
                        }
                        break;
                    case "present_message":
                        eventLog2.WriteEntry("requested to shutdown", EventLogEntryType.Information);
                        {
                            eventLog2.WriteEntry("present message", EventLogEntryType.Information);
                            //LockWorkStation();
                            WriteToFile("present_message" + array.message);
                        }
                        break;
                    case "take_screenshot":
                        eventLog2.WriteEntry("requested to screenshot", EventLogEntryType.Information);
                        {
                            eventLog2.WriteEntry("take screenshot", EventLogEntryType.Information);
                            //LockWorkStation();
                            WriteToFile("take_screenshot" + array.message);
                        }
                        break;
                    default:
                        eventLog2.WriteEntry("requested to continue", EventLogEntryType.Information);
                        break;
                }

            } //else: dont to anything.
            
        }

        private string GetStatus()
        {
            if (this.state == ComputerState.LOGGED_IN)
            {
                return "logged in";
            }
            else if(this.state == ComputerState.LOGGED_OUT)
            {
                return "logged out";
            }
            return "unknown";
        }

        private string getUID()
        {
            //---get uid
            //Create process
            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();

            //strCommand is path and file name of command to run
            pProcess.StartInfo.FileName = "C:\\Users\\User\\Desktop\\find_id.exe";

            //strCommandParameters are parameters to pass to program
            pProcess.StartInfo.Arguments = null;

            pProcess.StartInfo.UseShellExecute = false;

            //Set output of program to be written to process output stream
            pProcess.StartInfo.RedirectStandardOutput = true;

            pProcess.StartInfo.CreateNoWindow = true;

            //Start the process
            pProcess.Start();

            //Get program output
            string strUID = pProcess.StandardOutput.ReadToEnd();

            strUID = strUID.Remove(strUID.Length - 2);
            //Console.WriteLine(strUID);
            //Wait for process to finish
            pProcess.WaitForExit();

            return strUID;
        }


        protected override void OnShutdown()
        {
            string strUID = getUID();

            //---http
            // Create a request using a URL that can receive a post. 
            WebRequest request = WebRequest.Create("https://us-central1-floryt-88029.cloudfunctions.net/service");
            // Set the Method property of the request to POST.
            request.Method = "POST";
            // Create POST data and convert it to a byte array.

            string email = GetCurrentUser();
            string postData = "{\"computerUid\":\"" + strUID + "\", \"status\":\"shutdown\", \"email\":\""+email+"\"}";
            //Console.WriteLine(postData);
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            // Set the ContentType property of the WebRequest.
            request.ContentType = "application/json";
            // Set the ContentLength property of the WebRequest.
            request.ContentLength = byteArray.Length;
            // Get the request stream.
            Stream dataStream = request.GetRequestStream();
            // Write the data to the request stream.
            dataStream.Write(byteArray, 0, byteArray.Length);
            // Close the Stream object.
            dataStream.Close();

            eventLog2.WriteEntry("detected shutdown, informed the server", EventLogEntryType.Information);

            //// Get the response.
            //WebResponse response = request.GetResponse();
        }


        private void WriteToFile(string command)
        {
            string file_path = @"C:\Users\User\Desktop\command.txt";//@ is for no special chars

            using (FileStream fs = File.Create(file_path)) //using because file is an unmanaged resource (=memory) so you make sure Dispose function will work for them (and for file, the 'Close' method as well)
            {
                //the file is created
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(file_path, true))
            {
                file.WriteLine(command);
            }

            eventLog2.WriteEntry("wrote to file", EventLogEntryType.Information);
        }

    }
}
