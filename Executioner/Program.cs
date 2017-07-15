using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Executioner
{
    enum Command
    {
        ShowMessage,
        LockWorkstation,
        Shutdown,
        TakeScreenshot
    }

    static class Program
    {
        private static string SCREENSHOT_UPLOAD_URL = @"https://us-central1-floryt-88029.cloudfunctions.net/upload_screenshot";
        private static string WORKING_DIR = @"C:\Program Files\Floryt\";
        private static string SCREENSHOT_FOLDER = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + 
            @"\Floryt\Screenshots\");

        [DllImport("user32")]
        public static extern void LockWorkStation();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 2) return;
            if (!args.Contains("-c")) return;
            string command = args.ElementAt(Array.IndexOf(args, "-c") + 1);
            string message = string.Empty;
            if (command == "ShowMessage")
            {
                if (!args.Contains("-m")) return;
                message = args.ElementAt(Array.IndexOf(args, "-m") + 1);
            }

            object commandEnum;
            try
            {
                commandEnum = Enum.Parse(typeof(Command), command);
            }
            catch (Exception e)
            {
                return;
            }

            switch (commandEnum)
            {
                case Command.ShowMessage:
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new Message(message));
                    break;
                case Command.LockWorkstation:
                    LockWorkStation();
                    break;
                case Command.Shutdown:
                    var psi = new ProcessStartInfo("shutdown", "/s /t 0");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                    break;
                case Command.TakeScreenshot:
                    string screenshotPath = CreateScreenshot();
                    string screenshotData = ReadFileAsBase64(screenshotPath);
                    SendDataToServer(screenshotData);
                    break;
                default:
                    return;
            }
        }

        public static string CreateScreenshot()
        {
            //TODO: Support multiple screens
            Bitmap bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                Screen.PrimaryScreen.Bounds.Height,
                PixelFormat.Format32bppArgb);
            Graphics gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                Screen.PrimaryScreen.Bounds.Y,
                0,
                0,
                Screen.PrimaryScreen.Bounds.Size,
                CopyPixelOperation.SourceCopy);
            long currentEpoch =
                (long) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            string screenshotPath = SCREENSHOT_FOLDER + currentEpoch + ".png";
            if (!Directory.Exists(SCREENSHOT_FOLDER)) Directory.CreateDirectory(SCREENSHOT_FOLDER);
            using (FileStream fileStream = File.Create(screenshotPath))
            {
                bmpScreenshot.Save(fileStream, ImageFormat.Png);
            }
            return screenshotPath;
        }

        public static string ReadFileAsBase64(string imageLocation)
        {
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;
            FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            byte[] imageData = br.ReadBytes((int) imageFileLength);
            return Convert.ToBase64String(imageData);
        }

        private static void SendDataToServer(string screenshotData)
        {
            Process pProcess = new Process
            {
                StartInfo =
                {
                    FileName = WORKING_DIR + "ComputerUidGenerator.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            pProcess.Start();
            string computerUid = pProcess.StandardOutput.ReadToEnd();
            computerUid = computerUid.Replace("\r\n", string.Empty);
            pProcess.WaitForExit();
            
            Dictionary<string, string> metadata =
                new Dictionary<string, string> {{"computerUid", computerUid}, {"image", screenshotData}};
            string postData = JsonConvert.SerializeObject(metadata);

            WebRequest request = WebRequest.Create(SCREENSHOT_UPLOAD_URL);
            request.Method = "POST";
            request.ContentType = "application/json";
            
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            request.ContentLength = byteArray.Length;
            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();
            WebResponse response = request.GetResponse();
        }
    }
}
