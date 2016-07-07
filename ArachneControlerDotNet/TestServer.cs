using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using RestSharp;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace ArachneControlerDotNet
{
    public class TestServer : Core
    {
        public List<CukesModel> Cukes { get; set; }

        public static List<string> RunningCukes { get; set; }

        /*-------------------------------------------------------------------*/

        public TestServer (string serverPath, string frameworkPath)
            : base (frameworkPath)
        {
            ApiRequest.ServerPath = new Uri (serverPath);
        }

        /*======================================================================================================*/

        public TestServer (string frameworkPath)
            : base (frameworkPath)
        {
            ApiRequest.ServerPath = new Uri ("https://bby-loom.herokuapp.com/");
        }

        /// <summary>
        /// Resets the devices status.
        /// </summary>
        /// <param name="devices">Devices.</param>
        protected void ResetDevicesStatus (List<DeviceModel> devices)
        {
            foreach (DeviceModel device in devices) {
                device.SetStatus (DeviceStatus.Ready);
            }
        }

        /// <summary>
        /// Restarts the adb server.
        /// </summary>
        protected void RestartAdbServer ()
        {
            PrintLine ("Restarting ADB server");
            ExecuteShell ("adb", "kill-server");
            Thread.Sleep (1000);
            ExecuteShell ("adb", "start-server");
            Thread.Sleep (1000);
        }

        /// <summary>
        /// Starts a loop which is waiting for devices to reboot.
        /// </summary>
        protected void RebootLoop ()
        {
            Task.Factory.StartNew (() => {
                while (true) {
                    foreach (var device in GetDevices ()) {
                        if (device.GetStatus == DeviceStatus.Restart) {
                            device.Restart ();
                            device.SetStatus (DeviceStatus.Rebooting);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Start server instance.
        /// </summary>
        public void Start ()
        {
            RunningCukes = new List<string> ();
            AppiumModel.DisposeAppiumResources ();
            RestartAdbServer ();
            SendFeatures (Branch.Master);
            ApiRequest.CreateRequest (ApiRequestType.Devices, Method.DELETE, null, null);
            UpdateDevices (GetDevices (), LoadAllUSBDevices ());
            ResetDevicesStatus (GetDevices ());
            SplashScreen ();
            CukesModel.TokenSource = new CancellationTokenSource ();
            CukesModel.CancelationToken = CukesModel.TokenSource.Token;

            RebootLoop ();

            while (true) {
                try {
                    Cukes = GetCommands ();
                    AssignCukes ();

                } catch (Exception e) {
                    PrintLine ("Error: Couldn't receive commands! " + e.Message);
                }

                if (Executions.Queued.Count > 0) {
                    Executions.Queued.Dequeue ().Execute ();
                }

                if (Executions.Running.Count > 0) {
                    if (Executions.Running.Peek () != (Executions.Running.Where (m => m.IsRunning && RunningCukes.Contains (m._id)))) {
                        var cuke = Executions.Running.Dequeue ();
                        Executions.Errors.Enqueue (cuke);
                        cuke.SetStatus (CukeStatus.Error);
                    }

                    if (Executions.Pending.Count > 0) {
                        Executions.Pending.Dequeue ().Execute ();
                    }

                    if (Executions.Stop.Count > 0) {
                        Executions.Stop.Dequeue ().Stop ();
                    }

                    UpdateDevices (GetDevices (), LoadAllUSBDevices ());
                }
            }
        }


        /// <summary>
        /// Assigns the cukes.
        /// </summary>
        private void AssignCukes ()
        {
            foreach (CukesModel cuke in Cukes) {
                cuke.Assign ();    
            }
        }

        /// <summary>
        /// Loads all USB devices.
        /// </summary>
        /// <returns>The list of all USB devices.</returns>
        protected List<DeviceModel> LoadAllUSBDevices ()
        {
            return GetAndroidDevices ().Concat (GetAppleDevices ()).ToList ();
        }

        /// <summary>
        /// Gets the devices.
        /// </summary>
        /// <returns>The list of devices.</returns>
        protected List<DeviceModel> GetDevices ()
        {
            var request = ApiRequest.CreateRequest<List<DeviceModel>> (ApiRequestType.Devices, null);
            return request;
        }

        /// <summary>
        /// Gets the specific devices.
        /// </summary>
        /// <returns>The specific devices.</returns>
        /// <param name="deviceType">Device type.</param>
        protected List<DeviceModel> GetSpecificDevices (DeviceType deviceType)
        {
            switch (deviceType) {
            case DeviceType.Android:
                return GetAndroidDevices ();
            case DeviceType.iOS:
                return GetAppleDevices ();
            default:
                return null;
            }
        }



        /// <summary>
        /// Gets the android devices.
        /// </summary>
        /// <returns>The android devices.</returns>
        protected List<DeviceModel> GetAndroidDevices ()
        {
            var output = ExecuteShell ("adb", "devices -l");

            string [] devices = output.Split ('\n');

            List<DeviceModel> androidPayload = new List<DeviceModel> ();
            List<string> androidDevices = new List<string> ();

            foreach (string line in devices) {
                if (line.Contains ("usb")) {
                    androidDevices.Add (line.Split (' ') [0]);
                }
            }

            foreach (string device in androidDevices) {
                AppiumModel appiumProcessModel = Processes.Where (p => p.DeviceID == device).FirstOrDefault ();

                int appiumPort = Processes.OrderBy (o => o.Port).ToList ().Last ().Port + 1;
                int chromePort = appiumPort + 500;

                if (appiumProcessModel == null) {
                    appiumProcessModel = AppiumModel.Start(chromePort, appiumPort, device);
                } else {
                    appiumProcessModel = Processes.FirstOrDefault (p => p.DeviceID == device);
                    appiumPort = appiumProcessModel.Port;
                    chromePort = appiumPort + 500;
                }

                DeviceModel dev = new DeviceModel () {
                    platformName = GetAndroidDetails ("net.bt.name", device).Replace ("\n", string.Empty).Replace ("\r", string.Empty),
                    platformVersion = GetAndroidDetails ("ro.build.version.release", device).Replace ("\n", string.Empty).Replace ("\r", string.Empty),
                    deviceName = GetAndroidDetails ("ro.product.model", device).Replace ("\n", string.Empty).Replace ("\r", string.Empty),
                    udid = device,
                    IsAvailable = true,
                    chromePort = chromePort,
                    port = appiumPort,
                    status = "ready",
                    PID = appiumProcessModel.Process.Id
                };

                androidPayload.Add (dev);
            }

            return androidPayload;
        }

        /// <summary>
        /// Gets the android details.
        /// </summary>
        /// <returns>The android details.</returns>
        /// <param name="type">Type.</param>
        /// <param name="deviceId">Device identifier.</param>
        protected string GetAndroidDetails (string type, string deviceId)
        {
            var output = ExecuteShell ("adb", string.Format ("-s {0} shell getprop {1}", deviceId, type), null, MethodInfo.GetCurrentMethod ().Name);
            return output;
        }

        /// <summary>
        /// Gets the apple devices.
        /// </summary>
        /// <returns>The apple devices.</returns>
        protected List<DeviceModel> GetAppleDevices ()
        {
            var output = ExecuteShell ("instruments", "-s devices", null, MethodInfo.GetCurrentMethod ().Name);
            string [] instruments = output.Split ('\n');
            var instrumentsList = instruments.Where (val => val != instruments.First ()).ToList ();

            var devices = new List<DeviceModel> ();

            foreach (string ios in instrumentsList) {
                if (!string.IsNullOrEmpty (ios)) {
                    var dev = new DeviceModel () {
                        deviceName = ios.Split ('(') [0],
                        platformName = "iOS",
                        platformVersion = ios.Contains (")") ? ios.Split ('(') [1].Split (')') [0] : "NON-IOS DEVICE",

                        udid = ios.Split ('[') [1].Split (']') [0],
                        status = "ready"
                    };

                    if (!dev.udid.Contains ('-') && !dev.deviceName.Contains ("instruments"))
                        devices.Add (dev);
                }
            }

            return devices;
        }

        /// <summary>
        /// Sets the devices.
        /// </summary>
        /// <param name="devices">Devices.</param>
        private bool SetDevices (List<DeviceModel> devices)
        {
            bool flag = false;

            foreach (var device in devices) {
                var request = ApiRequest.CreateRequest (ApiRequestType.Devices, Method.POST, null, device);
                flag = request.StatusCode == System.Net.HttpStatusCode.OK;
                if (flag) {
                    AppiumModel appiumProcessModel = Processes.FirstOrDefault (p => p.DeviceID == device.udid);

                    int appiumPort = Processes.OrderBy (o => o.Port).ToList ().Last ().Port + 1;
                    int chromePort = appiumPort + 500;

                    if (appiumProcessModel == null) {
                        appiumProcessModel = AppiumModel.Start (chromePort, appiumPort, device.udid);
                    } else
                        appiumProcessModel = Processes.FirstOrDefault (p => p.DeviceID == device.udid);

                    PrintLine (string.Format ("Added device {0}", device.deviceName), ConsoleColor.Green);
                }

            }

            return flag;
        }

        /// <summary>
        /// Sends the features.
        /// </summary>
        /// <param name="branch">Branch.</param>
        protected void SendFeatures (Branch branch)
        {
            PrintLine (string.Format ("Preparing FEATURES payload to send.. {0}", branch.ToString ()), ConsoleColor.Yellow);
            var features = FeaturesParser.GetPayload ();

            if (CleanFeatures ()) {
                try {
                    var request = ApiRequest.CreateRequest (ApiRequestType.Features, Method.POST, null, features);
                    PrintLine (string.Format ("Features payload sent [{0}]", request.StatusCode), ConsoleColor.Green);
                } catch (Exception e) {
                    PrintLine (string.Format ("Features payload FAILED: {0}", e.Message), ConsoleColor.Red);
                }
            } else {
                PrintLine ("Couldn't clean existing FEATURES.", ConsoleColor.Red);
            }
        }

        protected bool CleanFeatures ()
        {
            var request = ApiRequest.CreateRequest (ApiRequestType.Features, Method.DELETE, null, null);
            return (request.StatusCode == System.Net.HttpStatusCode.OK);
        }


        /// <summary>
        /// Prepares the command.
        /// </summary>
        /// <returns>The command.</returns>
        /// <param name="command">Command.</param>
        protected string PrepareCommand (string command)
        {
            if (command != null) {
                var commandWithPath = new List<string> ();

                var cmd = command.Split (' ');

                for (int i = 0; i < cmd.Length; i++) {
                    if (cmd [i].Contains ("cucumber")) {
                        var cmdPath = string.Format ("cucumber -r {0}", FunctionalTestsPath);
                        commandWithPath.Add (cmdPath);
                    } else {
                        commandWithPath.Add (cmd [i]);
                    }
                }

                return string.Join (" ", commandWithPath);
            }

            return null;
        }

        /// <summary>
        /// Removes the devices.
        /// </summary>
        /// <param name="oldDevices">Old devices.</param>
        protected bool RemoveDevices (List<DeviceModel> oldDevices)
        {
            bool flag = false;

            foreach (var oldDevice in oldDevices) {
                var request = ApiRequest.CreateRequest (ApiRequestType.Devices, Method.DELETE, oldDevice._id, null);
                flag = request.StatusCode == System.Net.HttpStatusCode.OK;
                if (flag && !_initRun) {
                    PrintLine (string.Format ("Removed device {0}", oldDevice.deviceName), ConsoleColor.Cyan);
                    AppiumModel.Kill (oldDevice.port);
                }
            }
            return flag;
        }

        /// <summary>
        /// Gets the commands.
        /// </summary>
        /// <returns>The commands.</returns>
        protected List<CukesModel> GetCommands ()
        {
            return ApiRequest.CreateRequest<List<CukesModel>> (ApiRequestType.Cukes, null);
        }

        /// <summary>
        /// Updates the devices.
        /// </summary>
        /// <param name="devicesDB">Devices db.</param>
        /// <param name="devicesUSB">Devices usb.</param>
        private void UpdateDevices (List<DeviceModel> devicesDB, List<DeviceModel> devicesUSB)
        {
            try {
                var newUSB = new List<DeviceModel> ();
                var oldDB = new List<DeviceModel> ();

                var dbDevices = new List<string> ();
                var usbDevices = new List<string> ();

                foreach (DeviceModel db in devicesDB) {
                    dbDevices.Add (db.udid);
                }

                foreach (DeviceModel usb in devicesUSB) {
                    usbDevices.Add (usb.udid);
                }

                var newDevices = usbDevices.Except (dbDevices).ToList ();
                var oldDevices = dbDevices.Except (usbDevices).ToList ();

                foreach (string udid in newDevices) {
                    foreach (var usb in devicesUSB) {
                        if (usb.udid == udid)
                            newUSB.Add (usb);
                    }

                }

                if (newUSB.Count > 0) {
                    Executions.Devices.AddRange (newUSB);
                    SetDevices (newUSB);
                }

                foreach (var udid in oldDevices) {
                    foreach (var db in devicesDB) {
                        if (db.udid == udid) {
                            oldDB.Add (db);
                        }
                    }
                }

                foreach (var oldDevice in oldDB) {
                    Executions.Devices.RemoveAll (d => d.udid == oldDevice.udid);
                }

                RemoveDevices (oldDB);
                _initRun = false;

                CreateDevicesJsonFile ();
            } catch (Exception ex) {
                PrintLine (ex.Message);
            }
        }

        /// <summary>
        /// Creates the devices json file.
        /// </summary>
        /// <returns>The devices json file.</returns>
        private bool CreateDevicesJsonFile ()
        {
            string filePath = ".config/devices.json";
            var fullPath = Path.Combine (FunctionalTestsPath, filePath);

            var request = ApiRequest.CreateRequest (ApiRequestType.Devices, Method.GET, null, null);
            string response = request.Content;

            File.WriteAllText (fullPath, response);

            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }
    }
}




