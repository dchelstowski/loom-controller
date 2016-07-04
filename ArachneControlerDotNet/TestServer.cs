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

        public List<string> RunningCukes { get; set; }

        private CancellationToken _cancelationToken { get; set; }

        private CancellationTokenSource _tokenSource { get; set; }

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
        /// Calls the async execution method.
        /// </summary>
        /// <param name="cuke">Cuke.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected async void CallAsyncExecutionMethod (CukesModel cuke, CancellationToken cancellationToken)
        {
            PrintLine (string.Format ("TASK STARTED [device: {0}] [cuke: {1}]", cuke.device, cuke._id), ConsoleColor.Green);
            Task<bool> execution = HandleExecution (cuke, cancellationToken);
            await Task.Delay (5 * 1000);

            bool result = await execution;

            if (result) {
                cuke.IsFinished = true;
                cuke.IsRunning = false;
                var device = Executions.Devices.Find (d => d.udid == cuke.device.udid);
                var reportProvider = new ReportProvider ("reports/" + cuke.FileName, FunctionalTestsPath);
                var report = reportProvider.GetFullReport (device, "QA", cuke._id); // hardcoded environment!

                if (report != null) {
                    PrintLine (report.Result, ConsoleColor.Green);

                    var jsonPayload = JsonConvert.SerializeObject (report);

                    var request = ApiRequest.CreateRequest (
                                      ApiRequestType.Reports,
                                      Method.POST,
                                      string.Format ("{0}", cuke._id),
                                      jsonPayload,
                                      null);

                    if (request.StatusCode == System.Net.HttpStatusCode.OK)
                        PrintLine ("Execution report sent!", ConsoleColor.Green);
                    else
                        PrintLine ("Execution report upload failed!", ConsoleColor.Green);

                    PrintLine (string.Format ("EXECUTION FINISHED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id), ConsoleColor.Green);
                } else {
                    PrintLine (string.Format ("EXECUTION CANCELED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id));
                    cuke.SetStatus (CukeStatus.Error);
                    cuke.device.SetStatus (DeviceStatus.Ready);
                    return;
                }

                KillAppiumInstance (cuke.device.port);
                Thread.Sleep (1000);
                StartAppiumInstance (cuke.device.chromePort, cuke.device.port, cuke.device.udid);
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
        /// Handles the execution (task).
        /// </summary>
        /// <returns>Bool result.</returns>
        /// <param name="cuke">Cuke.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected Task<bool> HandleExecution (CukesModel cuke, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool> ();

            Directory.SetCurrentDirectory (FunctionalTestsPath);

            var command = cuke.command.Split (new string [] { "cucumber" }, StringSplitOptions.RemoveEmptyEntries) [0];

            PrintLine (string.Format ("[NEW COMMAND] CUKE: {0} / DEVICE: {1}", cuke._id, cuke.device.deviceName), ConsoleColor.Magenta);

            string jsonResultFile = string.Format ("{0}ArachneExecutionReport_{1}.txt", DateTime.Now.ToString ("yyMMdd-hhmm"), cuke._id);
            cuke.FileName = jsonResultFile;

            var proc = new Process ();

            proc.EnableRaisingEvents = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "cucumber";
            proc.StartInfo.Arguments = command + " -f json -o reports/" + jsonResultFile;

            PrintLine ("cucumber" + command + " -f json -o reports/" + jsonResultFile);

            proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => {
                PrintLine ("[" + cuke.device + "]" + " Selenium output: " + e.Data);

                if (_cancelationToken.IsCancellationRequested) {
                    tcs.SetResult (false);
                    RunningCukes.Remove (cuke._id);
                    tcs.SetCanceled ();
                    cuke.device.SetStatus (DeviceStatus.Ready);
                    cuke.IsFinished = true;
                    cuke.IsRunning = false;
                    Executions.Devices.Find (d => d.udid == cuke.device.udid).IsAvailable = true;
                    Executions.Running.Remove (cuke);
                    return;
                }
            };

            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                if (e.Data != null && e.Data.Contains ("Selenium::WebDriver::Error")) {
                    PrintLine (e.Data);
                    tcs.SetResult (false);
                }
            };

            proc.Exited += (object sender, EventArgs e) => {
                tcs.SetResult (true);
                RunningCukes.Remove (cuke._id);
                cuke.SetStatus (CukeStatus.Done);
                cuke.IsFinished = true;
                cuke.IsRunning = false;
                Executions.Running.Remove (cuke);
                Executions.Devices.Find (d => d.udid == cuke.device.udid).IsAvailable = true;
                Task.Delay (5000);
                proc.Dispose ();
            };

            proc.Start ();
            proc.BeginErrorReadLine ();
            proc.WaitForExit ();

            return tcs.Task;
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
                            RestartDevice (device.udid);
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
            int lastKnownCukesCount = 0;
            DisposeAppiumResources ();
            RestartAdbServer ();
            SendFeatures (Branch.Master);
            ApiRequest.CreateRequest (ApiRequestType.Devices, Method.DELETE, null, null);
            UpdateDevices (GetDevices (), LoadAllUSBDevices ());
            ResetDevicesStatus (GetDevices ());
            SplashScreen ();
            _tokenSource = new CancellationTokenSource ();
            _cancelationToken = _tokenSource.Token;

            RebootLoop ();

            while (true) {
                try {
                    Cukes = GetCommands ();
                    if (Cukes.Count > lastKnownCukesCount) {
                        lastKnownCukesCount = Cukes.Count;
                        AssignCukes ();
                    }
                } catch (Exception e) {
                    PrintLine ("Error: Couldn't receive commands! " + e.Message);
                }

                if (Executions.Queued.Count > 0) {
                    foreach (var cuke in Executions.Queued.ToList ()) {
                        ExecuteCuke (cuke);
                    }
                }

                if (Executions.Running.Count > 0) {

                    foreach (var cuke in Executions.Running.ToList ()) {
                        if (!cuke.IsRunning && !RunningCukes.Contains (cuke._id)) {
                            Executions.Running.Remove (cuke);
                            Executions.Errors.Add (cuke);
                            cuke.SetStatus (CukeStatus.Error);
                        }
                    }
                }

                if (Executions.Pending.Count > 0) {
                    foreach (var cuke in Executions.Pending.ToList ()) {
                        ExecuteCuke (cuke);
                    }
                }

                if (Executions.Stop.Count > 0) {
                    foreach (var cuke in Executions.Stop.ToList ()) {
                        // TODO: ?
                    }
                }

                UpdateDevices (GetDevices (), LoadAllUSBDevices ());
            }
        }

        /// <summary>
        /// Executes the cuke.
        /// </summary>
        /// <param name="cuke">Cuke.</param>
        private void ExecuteCuke (CukesModel cuke)
        {
            if (!cuke.IsRunning && !cuke.IsFinished) {
                var device = Executions.Devices.Find (d => d.udid == cuke.device.udid);

                if (device != null && device.IsAvailable) {
                    if (cuke.GetStatus == CukeStatus.Queued && cuke.GetStatus == CukeStatus.Pending) {
                        Executions.Queued.Remove (cuke);
                        Executions.Running.Add (cuke);
                    }
                    cuke.device.SetStatus (DeviceStatus.Busy);
                    cuke.SetStatus (CukeStatus.Running);
                    cuke.IsRunning = true;
                    device.IsAvailable = false;
                    RunningCukes.Add (cuke._id);
                    Task.Factory.StartNew (() => CallAsyncExecutionMethod (cuke, _cancelationToken));
                } else {
                    if (cuke.GetStatus != CukeStatus.Queued) {
                        ApiRequest.SetCukeStatus (CukeStatus.Queued, cuke._id);
                        cuke.SetStatus (CukeStatus.Queued);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns the cukes.
        /// </summary>
        private void AssignCukes ()
        {
            foreach (CukesModel cuke in Cukes) {
                switch (cuke.GetStatus) {
                case CukeStatus.Running:
                    break;
                case CukeStatus.Pending:
                    if (Executions.Pending.FirstOrDefault (m => m._id == cuke._id) == null) {
                        Executions.Pending.Add (cuke);
                        Executions.Pending.Distinct ();
                    }
                    break;
                case CukeStatus.Stop:
                    if (Executions.Stop.FirstOrDefault (m => m._id == cuke._id) == null) {
                        Executions.Stop.Add (cuke);
                        Executions.Stop.Distinct ();
                    }
                    break;
                case CukeStatus.Queued:
                    if (Executions.Queued.FirstOrDefault (m => m._id == cuke._id) == null) {
                        Executions.Queued.Add (cuke);
                        Executions.Queued.Distinct ();
                    }
                    break;
                }
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
        /// Disposes the appium resources.
        /// </summary>
        protected void DisposeAppiumResources ()
        {
            var output = ExecuteShell ("/bin/bash", "-c 'ps -ax | grep appium'");
            string [] lines = output.Split ('\n');

            foreach (var line in lines) {
                if (line.Contains ("grep"))
                    continue;

                var words = line.Split (' ');
                int pid = 0;
                int.TryParse (words [0].Trim (), out pid);

                if (pid == 0)
                    continue;

                PrintLine ("Trying to dispose resource " + pid);
                ExecuteShell ("/bin/bash", "-c 'kill " + pid + "'");
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
                    appiumProcessModel = StartAppiumInstance (chromePort, appiumPort, device);
                } else {
                    appiumProcessModel = Processes.Where (p => p.DeviceID == device).FirstOrDefault ();
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
                    AppiumModel appiumProcessModel = Processes.Where (p => p.DeviceID == device.udid).FirstOrDefault ();

                    int appiumPort = Processes.OrderBy (o => o.Port).ToList ().Last ().Port + 1;
                    int chromePort = appiumPort + 500;

                    if (appiumProcessModel == null) {
                        appiumProcessModel = StartAppiumInstance (chromePort, appiumPort, device.udid);
                    } else
                        appiumProcessModel = Processes.Where (p => p.DeviceID == device.udid).FirstOrDefault ();

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
        /// Removes the device.
        /// </summary>
        /// <param name="oldDevices">Old devices.</param>
        protected bool RemoveDevice (List<DeviceModel> oldDevices)
        {
            bool flag = false;

            foreach (var oldDevice in oldDevices) {
                var request = ApiRequest.CreateRequest (ApiRequestType.Devices, Method.DELETE, oldDevice._id, null);
                flag = request.StatusCode == System.Net.HttpStatusCode.OK;
                if (flag && !_initRun) {
                    PrintLine (string.Format ("Removed device {0}", oldDevice.deviceName), ConsoleColor.Cyan);
                    KillAppiumInstance (oldDevice.port);
                }
            }
            return flag;
        }

        /// <summary>
        /// Checks the cukes amount.
        /// </summary>
        /// <returns>The cukes amount.</returns>
        private int CheckCukesAmount ()
        {
            var request = ApiRequest.CreateRequest<List<CukesModel>> (ApiRequestType.Cukes, null);
            return request.Count ();
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

                RemoveDevice (oldDB);
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




