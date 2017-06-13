﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DoCommand
{
    class Program
    {
        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();


        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr h, string m, string c, int type);

        static void Main(string[] args)
        {
            

            while (true)
            {
                Thread.Sleep(7000); //sleep for 7 seconds

                string file_path = @"C:\Users\User\Desktop\command.txt";//@ is for no special chars
                //TODO: check if file exists
                string readText = File.ReadAllText(file_path);

                if (readText.Length >= 4 && readText.Substring(0, 4) == "lock")
                {
                    LockWorkStation();
                }
                else if (readText.Length >= 15 && readText.Substring(0, 15) == "present_message")
                {
                    string message = readText.Substring(15, readText.Length - 15);
                    //MessageBox((IntPtr)0, , message, "Message From Admin", 0);
                    MessageBox((IntPtr)0, message, "Message From Admin", 0);
                }
                else if(readText.Length >= 8 && readText.Substring(0, 8) == "shutdown")
                {
                    var psi = new ProcessStartInfo("shutdown", "/s /t 0");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                }
                else
                {
                    Console.WriteLine("NONE");
                }



            }

        }

    }
}