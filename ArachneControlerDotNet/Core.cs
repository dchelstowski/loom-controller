using System;
using System.Collections.Generic;
using System.Threading;
using RestSharp;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace ArachneControlerDotNet
{
    public abstract class Core
    {

        protected bool _initRun { get; set; }

        public static string FunctionalTestsPath { get; set; }

        protected FeaturesParser FeaturesParser { get; set; }

        public static ExecutionFactory Executions { get; set; }

        public static List<AppiumModel> Processes { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ArachneControlerDotNet.Core"/> class.
        /// </summary>
        /// <param name="testsPath">Path to directory with Functional Tests.</param>
        public Core (string testsPath)
        {
            Console.Clear ();

            Executions = new ExecutionFactory ();

            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => {
                PrintLine ("CLOSING SERVER...", ConsoleColor.Red);
                foreach (var proc in Processes) {
                    if (proc.Port != 9000) {
                        KillAppiumInstance (proc.Port);
                    }
                }
            };

            _initRun = true;

            Processes = new List<AppiumModel> ()
            {
                new AppiumModel()
                {
                    DeviceID = "12345",
                    Process = null,
                    Port = 9000
                }
            };

            FunctionalTestsPath = testsPath;
            FeaturesParser = new FeaturesParser (FunctionalTestsPath);
            LoadFeatures ();
        }

        /// <summary>
        /// Splashs the screen.
        /// </summary>
        /// <returns>The screen.</returns>
        protected void SplashScreen ()
        {
            PrintLine ("");
            PrintLine ("");
            PrintLine ("\t>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> ", ConsoleColor.DarkYellow);
            PrintLine ("\t>>                                                      >> ");
            PrintLine ("\t>>                    LOOM TEST SERVER                  >> ");
            PrintLine ("\t>>         github.com/dchelstowski/loom-controller      >> ");
            PrintLine ("\t>>                                                      >> ");
            PrintLine ("\t>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> ", ConsoleColor.Green);
            PrintLine ("");
            PrintLine ("");
        }

        /// <summary>
        /// Executes the shell.
        /// </summary>
        /// <returns>The shell.</returns>
        /// <param name="cmd">Cmd.</param>
        /// <param name="parameters">Parameters.</param>
        /// <param name="cuke">Cuke.</param>
        /// <param name="shellId">Shell identifier.</param>
        /// <param name="device">Device.</param>
        protected static string ExecuteShell (string cmd, string parameters, CukesModel cuke = null, string shellId = null, DeviceModel device = null)
        {
            var proc = new Process ();

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                if (e.Data != null) {
                    PrintLine (e.Data);
                }
            };

            proc.StartInfo.FileName = cmd;
            proc.StartInfo.Arguments = parameters;
            proc.Start ();

            proc.BeginErrorReadLine ();

            if (cmd.Contains ("cucumber") || cmd.Contains ("appium")) {
                PrintLine ("PID: " + proc.Id);
                return proc.Id.ToString ();
            }

            string output = null;

            try {
                using (StreamReader sr = proc.StandardOutput) {
                    output = sr.ReadToEnd ();
                }
            } catch (Exception e) {
                PrintLine ("Couldn't read stream!");
                PrintLine (e.Message);
            }

            return output;
        }

        /// <summary>
        /// Restarts the device.
        /// </summary>
        /// <returns>The device.</returns>
        /// <param name="deviceId">Device identifier.</param>
        protected static void RestartDevice (string deviceId)
        {
            ExecuteShell ("adb", "-s " + deviceId + " reboot");
        }

        /// <summary>
        /// Starts the appium instance.
        /// </summary>
        /// <returns>The appium instance.</returns>
        /// <param name="chromePort">Chrome port.</param>
        /// <param name="appiumPort">Appium port.</param>
        /// <param name="udid">Udid.</param>
        public static AppiumModel StartAppiumInstance (int chromePort, int appiumPort, string udid)
        {
            string parameters = string.Format ("-p {0} -U {1} --chromedriver-port {2}", appiumPort, udid, chromePort);

            Process proc = new Process ();
            AppiumModel model = null;

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "appium";
            proc.StartInfo.Arguments = parameters;

            PrintLine (string.Format ("Creating new Appium instance: UDID {0}, ChromeDriver: {1}, Appium {2}", udid, chromePort, appiumPort));
            Thread.Sleep (10 * 1000);
            PrintLine ("Ready");


            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                if (e.Data != null && !e.Data.Contains ("Couldn't start Appium REST http")) {
                    PrintLine ("appium: " + e.Data, ConsoleColor.Red);
                    if (!model.Process.HasExited)
                        model.Process.Kill ();
                    PrintLine ("Appium error encountered - rebooting device " + udid);
                    RestartDevice (udid);
                    ApiRequest.UpdateDeviceStatus (udid, DeviceStatus.Rebooting);
                    Processes.Remove (model);
                }
            };

            proc.Start ();
            proc.BeginErrorReadLine ();

            model = new AppiumModel () {
                Process = proc,
                DeviceID = udid,
                Port = appiumPort
            };
            Processes.Add (model);

            return model;
        }

        /// <summary>
        /// Kills the appium instance.
        /// </summary>
        /// <returns>The appium instance.</returns>
        /// <param name="port">Port.</param>
        public static void KillAppiumInstance (int port)
        {
            var appium = Processes.FirstOrDefault (p => p.Port == port);
            Processes.Remove (appium);

            if (!appium.Process.HasExited) {
                appium.Process.Kill ();
                PrintLine ("Killed Appium instance on port: " + port);
            }
        }

        /// <summary>
        /// Sends the file.
        /// </summary>
        /// <returns>The file.</returns>
        /// <param name="path">Path.</param>
        private bool SendFile (string path)
        {
            var request = ApiRequest.CreateRequest (ApiRequestType.Cukes, Method.POST, "results", null, path);
            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// Loads the features.
        /// </summary>
        /// <returns>The features.</returns>
        public void LoadFeatures ()
        {
            FeaturesParser.GetPayload ();
        }

        /// <summary>
        /// Print the specified value without a newline.
        /// </summary>
        /// <param name="value">Value.</param>
        public static void Print (string value)
        {
            Console.Write (value);
        }

        /// <summary>
        /// Print the specified value in specifed color.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="color">Color.</param>
        public static void Print (string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write (value);
            Console.ForegroundColor = lastColor;
        }

        /// <summary>
        /// Prints the line with specified value.
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="value">Value.</param>
        public static void PrintLine (string value)
        {
            var lastColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write (DateTime.Now.ToString () + " >> ");
            Console.ForegroundColor = lastColor;

            Console.Write (value + Environment.NewLine);
            Console.WriteLine ("");
        }

        /// <summary>
        /// Prints the line in specified font color.
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="value">Value.</param>
        /// <param name="color">Color.</param>
        public static void PrintLine (string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write (DateTime.Now.ToString () + " >> ");

            Console.ForegroundColor = color;
            Console.WriteLine (value);
            Console.ForegroundColor = lastColor;
            Console.WriteLine ("");
        }
    }
}

