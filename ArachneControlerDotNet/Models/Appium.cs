using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ArachneControlerDotNet
{
    public class Appium
    {
        public int      Port { get; set; }        

        public Process  Process { get; set; }

        public string   DeviceID { get; set; }

        /// <summary>
        /// Starts the appium instance.
        /// </summary>
        /// <returns>The appium instance.</returns>
        /// <param name="chromePort">Chrome port.</param>
        /// <param name="appiumPort">Appium port.</param>
        /// <param name="udid">Udid.</param>
        public static Appium Start (int chromePort, int appiumPort, string udid)
        {
            Appium model = null;

            Core.PrintLine (string.Format ("Creating new Appium instance: UDID {0}, ChromeDriver: {1}, Appium {2}", udid, chromePort, appiumPort));

            string parameters = string.Format ("-p {0} -U {1} --chromedriver-port {2}", appiumPort, udid, chromePort);

            var proc = new Process ();

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "appium";
            proc.StartInfo.Arguments = parameters;

            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                if (e.Data != null && !e.Data.Contains ("Couldn't start Appium REST http")) {
                    Core.PrintLine ("appium: " + e.Data, ConsoleColor.Red);
                    if (!model.Process.HasExited)
                        model.Process.Kill ();
                    Core.PrintLine ("Appium error encountered - rebooting device " + udid);
                    Device.Restart (udid);
                    ApiRequest.UpdateDeviceStatus (udid, DeviceStatus.Rebooting);
                    Core.Processes.Remove (model);
                }
            };

            proc.Start ();
            proc.BeginErrorReadLine ();

            Thread.Sleep (10 * 1000);

            model = new Appium () {
                Process = proc,
                DeviceID = udid,
                Port = appiumPort
            };
            Core.Processes.Add (model);

            Core.PrintLine (udid + "Ready");

            return model;
        }

        /// <summary>
        /// Kills the appium instance.
        /// </summary>
        /// <returns>The appium instance.</returns>
        /// <param name="port">Port.</param>
        public static void Kill(int port)
        {
            var appium = Core.Processes.FirstOrDefault (p => p.Port == port);
            Core.Processes.Remove (appium);

            if (!appium.Process.HasExited) {
                appium.Process.Kill ();
                Core.PrintLine ("Killed Appium instance on port: " + port);
            }
        }

        /// <summary>
        /// Disposes the appium resources.
        /// </summary>
        public static void DisposeAppiumResources ()
        {
            var output = Core.ExecuteShell ("/bin/bash", "-c 'ps -ax | grep appium'");
            string [] lines = output.Split ('\n');

            foreach (var line in lines) {
                if (line.Contains ("grep"))
                    continue;

                var words = line.Split (' ');
                int pid = 0;
                int.TryParse (words [0].Trim (), out pid);

                if (pid == 0)
                    continue;

                Core.PrintLine ("Trying to dispose resource " + pid);
                Core.ExecuteShell ("/bin/bash", "-c 'kill " + pid + "'");
            }
        }

    }
}

