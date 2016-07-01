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

        //====================CONSTRUCTORS======================

        public TestServer(string serverPath, string frameworkPath)
            : base(frameworkPath)
        {
            base.ServerPath = new Uri(serverPath);
        }

        public TestServer(string frameworkPath)
            : base(frameworkPath)
        {
            base.ServerPath = new Uri("https://bby-loom.herokuapp.com/");
        }

        //=====================METHODS=========================

        protected void ResetDevicesStatus(List<DeviceModel> devices)
        {
            foreach (DeviceModel device in devices)
            {
                UpdateDeviceStatus(device.udid, "ready");
            }
        }

        protected async void CallAsyncExecutionMethod(CukesModel cuke, CancellationToken cancellationToken)
        {
            PrintLine(string.Format("TASK STARTED [device: {0}] [cuke: {1}]", cuke.device, cuke._id), ConsoleColor.Green);
            Task<bool> execution = HandleExecution(cuke, cancellationToken);
            await Task.Delay(5 * 1000);

            bool result = await execution;

            if (result)
            {
                cuke.IsFinished = true;
                cuke.IsRunning = false;
                var device = Executions.Devices.Find(d => d.udid == cuke.device.udid);
                var reportProvider = new ReportProvider("reports/" + cuke.FileName, FunctionalTestsPath);
                var report = reportProvider.GetFullReport(device, "QA", cuke._id); // hardcoded environment

                if (report != null)
                {
                    PrintLine(report.Result, ConsoleColor.Green);

                    var jsonPayload = JsonConvert.SerializeObject(report);

                    var request = CreateRequest(
                                      ApiRequestType.Reports,
                                      Method.POST,
                                      string.Format("{0}", cuke._id),
                                      jsonPayload,
                                      null);

                    if (request.StatusCode == System.Net.HttpStatusCode.OK)
                        PrintLine("Execution report sent!", ConsoleColor.Green);
                    else
                        PrintLine("Execution report upload failed!", ConsoleColor.Green);

                    PrintLine(string.Format("EXECUTION FINISHED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id), ConsoleColor.Green);
                }
                else
                {
                    PrintLine(string.Format("EXECUTION CANCELED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id));
                    SetCukeStatus("error", cuke._id);
                    UpdateDeviceStatus(cuke.device.udid, "ready");
                    return;
                }

                KillAppiumInstance(cuke.device.port);
                Thread.Sleep(1000);
                StartAppiumInstance(cuke.device.chromePort, cuke.device.port, cuke.device.udid);
                UpdateDeviceStatus(cuke.device.udid, "ready");
            }
        }

        protected void RestartAdbServer()
        {
            PrintLine("Restarting ADB server");
            ExecuteShell("adb", "kill-server");
            Thread.Sleep(1000);
            ExecuteShell("adb", "start-server");
            Thread.Sleep(1000);
        }

        private bool RemoveCuke(string cukeId)
        {
            var request = CreateRequest(ApiRequestType.Cukes, Method.DELETE, cukeId, null, null);
            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }

        protected Task<bool> HandleExecution(CukesModel cuke, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            Directory.SetCurrentDirectory(FunctionalTestsPath);

            var command = cuke.command.Split(new string [] { "cucumber" }, StringSplitOptions.RemoveEmptyEntries)[0];

            PrintLine(string.Format("[NEW COMMAND] CUKE: {0} / DEVICE: {1}", cuke._id, cuke.device), ConsoleColor.Magenta);

            string jsonResultFile = string.Format("{0}ArachneExecutionReport_{1}.txt", DateTime.Now.ToString("yyMMdd-hhmm"), cuke._id);
            cuke.FileName = jsonResultFile;

            var proc = new Process();

            proc.EnableRaisingEvents = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "cucumber";
            proc.StartInfo.Arguments = command + " -f json -o reports/" + jsonResultFile;

            PrintLine("cucumber" + command + " -f json -o reports/" + jsonResultFile);

            proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                PrintLine("[" + cuke.device + "]" + " Selenium output: " + e.Data);

                if (_cancelationToken.IsCancellationRequested)
                {
                    tcs.SetResult(false);
                    RunningCukes.Remove(cuke._id);
                    tcs.SetCanceled();
                    UpdateDeviceStatus(cuke.device.udid, "ready");
                    cuke.IsFinished = true;
                    cuke.IsRunning = false;
                    Executions.Devices.Find(d => d.udid == cuke.device.udid).IsAvailable = true;
                    Executions.Running.Remove(cuke);
                    return;
                }
            };



            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null && e.Data.Contains("Selenium::WebDriver::Error"))
                {
                    PrintLine(e.Data);
                    //proc.Close();
                    tcs.SetResult(false);
                    //RunningCukes.Remove(cuke._id);
                    //updateDeviceStatus(cuke.device.udid, "ready");
                    //cuke.IsFinished = true;
                    //cuke.IsRunning = false;
                    //ExecutionFactory.Devices.Find(d => d.udid == cuke.device.udid).IsAvailable = true;
                    //ExecutionFactory.Running.Remove(cuke);
                }
            };

            proc.Exited += (object sender, EventArgs e) =>
            {
                tcs.SetResult(true);
                RunningCukes.Remove(cuke._id);
                SetCukeStatus("done", cuke._id);
                cuke.IsFinished = true;
                cuke.IsRunning = false;                
                Executions.Running.Remove(cuke);                    
                Executions.Devices.Find(d => d.udid == cuke.device.udid).IsAvailable = true;
                Task.Delay(5000);
                proc.Dispose();
            };

            //var appiumProcess = Processes.Where(x => x.DeviceID == cuke.device._id).FirstOrDefault();

            //if (appiumProcess.Process.HasExited)
            //    StartAppiumInstance(cuke.device.chromePort, cuke.device.port, cuke.device.udid);

            proc.Start();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            return tcs.Task;
        }

        protected void RebootLoop()
        {
            Task.Factory.StartNew(() =>
                {
                    while (true)
                    {
                        foreach (var device in GetDevices ())
                        {
                            if (device.status == "restart")
                            {
                                RestartDevice(device.udid);
                                UpdateDeviceStatus(device.udid, "rebooting");
                            }
                        }
                    }
                });
        }

        public void Start()
        {
            RunningCukes = new List<string>();
            int lastKnownCukesCount = 0;
            DisposeAppiumResources();
            RestartAdbServer();
            SendFeatures(Branch.Master);
            CreateRequest(ApiRequestType.Devices, Method.DELETE, null, null);
            UpdateDevices(GetDevices(), LoadAllUSBDevices());
            ResetDevicesStatus(GetDevices());
            SplashScreen();
            _tokenSource = new CancellationTokenSource();
            _cancelationToken = _tokenSource.Token;

            RebootLoop();

            while (true)
            {
                try
                {
                    Cukes = GetCommands();
                    if (Cukes.Count > lastKnownCukesCount)
                    {
                        lastKnownCukesCount = Cukes.Count;
                        AssignCukes();
                    }
                }
                catch (Exception e)
                {
                    PrintLine("Error: Couldn't receive commands! " + e.Message);
                }

                if (Executions.Queued.Count > 0)
                {
                    foreach (var cuke in Executions.Queued.ToList ())
                    {
                        ExecuteCuke(cuke);
                    }
                }

                if (Executions.Running.Count > 0)
                {

                    foreach (var cuke in Executions.Running.ToList ())
                    {
                        if (!cuke.IsRunning && !RunningCukes.Contains(cuke._id))
                        {
                            Executions.Running.Remove(cuke);
                            Executions.Errors.Add(cuke);
                            SetCukeStatus("error", cuke._id);
                        }
                    }
                }

                if (Executions.Pending.Count > 0)
                {
                    foreach (var cuke in Executions.Pending.ToList ())
                    {
                        ExecuteCuke(cuke);
                    }
                }

                if (Executions.Stop.Count > 0)
                {
                    foreach (var cuke in Executions.Stop.ToList())
                    {
                        // TODO: ?
                    }
                }

                UpdateDevices(GetDevices(), LoadAllUSBDevices());
            }
        }

        private void ExecuteCuke(CukesModel cuke)
        {
            if (!cuke.IsRunning && !cuke.IsFinished)
            {
                var device = Executions.Devices.Find(d => d.udid == cuke.device.udid);

                if (device != null && device.IsAvailable)
                {
                    if (cuke.status == "queued" && cuke.status == "pending")
                    {
                        Executions.Queued.Remove(cuke);
                        Executions.Running.Add(cuke);
                    }
                    UpdateDeviceStatus(cuke.device.udid, "busy");
                    SetCukeStatus("running", cuke._id);
                    cuke.IsRunning = true;
                    device.IsAvailable = false;
                    RunningCukes.Add(cuke._id);
                    Task.Factory.StartNew(() => CallAsyncExecutionMethod(cuke, _cancelationToken));
                }
                else
                {
                    if (cuke.status != "queued")
                    {
                        SetCukeStatus("queued", cuke._id);
                        cuke.status = "queued";
                    }
                }
            }
        }

        private void AssignCukes()
        {
            foreach (CukesModel cuke in Cukes)
            {
                switch (cuke.status)
                {
                    case "running":                        
                        break;
                    case "pending":
                        if (Executions.Pending.FirstOrDefault(m => m._id == cuke._id) == null)
                        {
                            Executions.Pending.Add(cuke);
                            Executions.Pending.Distinct();
                        }
                        break;
                    case "stop":
                        if (Executions.Stop.FirstOrDefault(m => m._id == cuke._id) == null)
                        {
                            Executions.Stop.Add(cuke);
                            Executions.Stop.Distinct();
                        }
                        break;
                    case "queued":
                        if (Executions.Queued.FirstOrDefault(m => m._id == cuke._id) == null)
                        {
                            Executions.Queued.Add(cuke);
                            Executions.Queued.Distinct();
                        }
                        break;
                }
            }
        }

        protected List<DeviceModel> LoadAllUSBDevices()
        {
            return GetAndroidDevices().Concat(GetAppleDevices()).ToList();
        }

        protected List<DeviceModel> GetDevices()
        {
            var request = CreateRequest<List<DeviceModel>>(ApiRequestType.Devices, null);
            return request;
        }

        protected List<DeviceModel> GetSpecificDevices(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Android:
                    return GetAndroidDevices();
                case DeviceType.iOS:
                    return GetAppleDevices();
                default:
                    return null;
            }
        }

        protected void DisposeAppiumResources()
        {
            var output = ExecuteShell("/bin/bash", "-c 'ps -ax | grep appium'");
            string[] lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("grep"))
                    continue;

                var words = line.Split(' ');
                int pid = 0;
                int.TryParse(words[0].Trim(), out pid);

                if (pid == 0)
                    continue;

                PrintLine("Trying to dispose resource " + pid);
                ExecuteShell("/bin/bash", "-c 'kill " + pid + "'");
            }
        }

        protected List<DeviceModel> GetAndroidDevices()
        {
            var output = ExecuteShell("adb", "devices -l");

            string[] devices = output.Split('\n');

            List<DeviceModel> androidPayload = new List<DeviceModel>();
            List<string> androidDevices = new List<string>();

            foreach (string line in devices)
            {
                if (line.Contains("usb"))
                {
                    androidDevices.Add(line.Split(' ')[0]);
                }
            }

            foreach (string device in androidDevices)
            {
                AppiumModel appiumProcessModel = Processes.Where(p => p.DeviceID == device).FirstOrDefault();

                int appiumPort = Processes.OrderBy(o => o.Port).ToList().Last().Port + 1;
                int chromePort = appiumPort + 500;

                if (appiumProcessModel == null)
                {
                    appiumProcessModel = StartAppiumInstance(chromePort, appiumPort, device);
                }
                else
                {
                    appiumProcessModel = Processes.Where(p => p.DeviceID == device).FirstOrDefault();
                    appiumPort = appiumProcessModel.Port;
                    chromePort = appiumPort + 500;
                }

                DeviceModel dev = new DeviceModel()
                {
                    platformName = GetAndroidDetails("net.bt.name", device).Replace("\n", string.Empty).Replace("\r", string.Empty),
                    platformVersion = GetAndroidDetails("ro.build.version.release", device).Replace("\n", string.Empty).Replace("\r", string.Empty),
                    deviceName = GetAndroidDetails("ro.product.model", device).Replace("\n", string.Empty).Replace("\r", string.Empty),
                    udid = device,
                    IsAvailable = true,
                    chromePort = chromePort,
                    port = appiumPort,
                    status = "ready",
                    PID = appiumProcessModel.Process.Id
                };

                androidPayload.Add(dev);
            }

            return androidPayload;
        }


        protected string GetAndroidDetails(string type, string deviceId)
        {
            var output = ExecuteShell("adb", string.Format("-s {0} shell getprop {1}", deviceId, type), null, MethodInfo.GetCurrentMethod().Name);
            return output;
        }

        protected List<DeviceModel> GetAppleDevices()
        {
            var output = ExecuteShell("instruments", "-s devices", null, MethodInfo.GetCurrentMethod().Name);
            string[] instruments = output.Split('\n');
            var instrumentsList = instruments.Where(val => val != instruments.First()).ToList();

            var devices = new List<DeviceModel>();

            foreach (string ios in instrumentsList)
            {
                if (!string.IsNullOrEmpty(ios))
                {
                    var dev = new DeviceModel()
                    {
                        deviceName = ios.Split('(')[0],
                        platformName = "iOS",
                        platformVersion = ios.Contains(")") ? ios.Split('(')[1].Split(')')[0] : "NON-IOS DEVICE",

                        udid = ios.Split('[')[1].Split(']')[0],
                        status = "ready"
                    };

                    if (!dev.udid.Contains('-') && !dev.deviceName.Contains("instruments"))
                        devices.Add(dev);
                }
            }

            return devices;
        }

        private bool SetDevices(List<DeviceModel> devices)
        {
            bool flag = false;

            foreach (var device in devices)
            {
                var request = CreateRequest(ApiRequestType.Devices, Method.POST, null, device);
                flag = request.StatusCode == System.Net.HttpStatusCode.OK;
                if (flag)
                {
                    AppiumModel appiumProcessModel = Processes.Where(p => p.DeviceID == device.udid).FirstOrDefault();

                    int appiumPort = Processes.OrderBy(o => o.Port).ToList().Last().Port + 1;
                    int chromePort = appiumPort + 500;

                    if (appiumProcessModel == null)
                    {
                        appiumProcessModel = StartAppiumInstance(chromePort, appiumPort, device.udid);
                    }
                    else
                        appiumProcessModel = Processes.Where(p => p.DeviceID == device.udid).FirstOrDefault();

                    PrintLine(string.Format("Added device {0}", device.deviceName), ConsoleColor.Green);
                }

            }

            return flag;
        }

        protected void SendFeatures(Branch branch)
        {
            PrintLine(string.Format("Preparing FEATURES payload to send.. {0}", branch.ToString()), ConsoleColor.Yellow);

            var features = FeaturesParser.GetPayloads();

            if (CleanFeatures())
            {
                try
                {
                    var request = CreateRequest(ApiRequestType.Features, Method.POST, null, features);
                    PrintLine(string.Format("Features payload sent [{0}]", request.StatusCode), ConsoleColor.Green);
                }
                catch (Exception e)
                {
                    PrintLine(string.Format("Features payload FAILED: {0}", e.Message), ConsoleColor.Red);
                }
            }
            else
            {
                PrintLine("Couldn't clean existing FEATURES.", ConsoleColor.Red);
            }
        }

        protected bool CleanFeatures()
        {
            var request = CreateRequest(ApiRequestType.Features, Method.DELETE, null, null);
            return (request.StatusCode == System.Net.HttpStatusCode.OK);
        }



        protected bool SetCukeStatus(string status, string commandId)
        {
            var request = CreateRequest(ApiRequestType.Cukes, Method.POST, string.Format("{0}/{1}", commandId, status), null);
            if (request.StatusCode == System.Net.HttpStatusCode.OK)
            {
                PrintLine(string.Format("Updated cuke {0} status to {1}", commandId, status), ConsoleColor.Gray);
                return true;
            }
            PrintLine("Couldn't update cuke status.");
            return false;
        }

        protected string PrepareCommand(string command)
        {
            if (command != null)
            {
                var commandWithPath = new List<string>();

                var cmd = command.Split(' ');

                for (int i = 0; i < cmd.Length; i++)
                {
                    if (cmd[i].Contains("cucumber"))
                    {
                        var cmdPath = string.Format("cucumber -r {0}", FunctionalTestsPath);
                        commandWithPath.Add(cmdPath);
                    }
                    else
                    {
                        commandWithPath.Add(cmd[i]);
                    }
                }

                return string.Join(" ", commandWithPath);
            }

            return null;
        }



        protected bool RemoveDevice(List<DeviceModel> oldDevices)
        {
            bool flag = false;

            foreach (var oldDevice in oldDevices)
            {
                var request = CreateRequest(ApiRequestType.Devices, Method.DELETE, oldDevice._id, null);
                flag = request.StatusCode == System.Net.HttpStatusCode.OK;
                if (flag && !_initRun)
                {
                    PrintLine(string.Format("Removed device {0}", oldDevice.deviceName), ConsoleColor.Cyan);
                    KillAppiumInstance(oldDevice.port);
                }
            }
            return flag;
        }

        private int CheckCukesAmount()
        {
            var request = CreateRequest<List<CukesModel>>(ApiRequestType.Cukes, null);
            return request.Count();
        }

        protected List<CukesModel> GetCommands()
        {
            return CreateRequest<List<CukesModel>>(ApiRequestType.Cukes, null);
        }

        private void UpdateDevices(List<DeviceModel> devicesDB, List<DeviceModel> devicesUSB)
        {
            try
            {
                var newUSB = new List<DeviceModel>();
                var oldDB = new List<DeviceModel>();

                var dbDevices = new List<string>();
                var usbDevices = new List<string>();

                foreach (DeviceModel db in devicesDB)
                {
                    dbDevices.Add(db.udid);
                }

                foreach (DeviceModel usb in devicesUSB)
                {
                    usbDevices.Add(usb.udid);
                }

                var newDevices = usbDevices.Except(dbDevices).ToList();
                var oldDevices = dbDevices.Except(usbDevices).ToList();

                foreach (string udid in newDevices)
                {
                    foreach (var usb in devicesUSB)
                    {
                        if (usb.udid == udid)
                            newUSB.Add(usb);
                    }

                }

                if (newUSB.Count > 0)
                {
                    Executions.Devices.AddRange(newUSB);
                    SetDevices(newUSB);
                }

                foreach (var udid in oldDevices)
                {
                    foreach (var db in devicesDB)
                    {
                        if (db.udid == udid)
                        {
                            oldDB.Add(db);
                        }
                    }
                }

                foreach (var oldDevice in oldDB)
                {
                    Executions.Devices.RemoveAll(d => d.udid == oldDevice.udid);
                }

                RemoveDevice(oldDB);
                _initRun = false;

                CreateDevicesJsonFile();
            }
            catch (Exception ex)
            {
                PrintLine(ex.Message);
            }
        }

        private bool CreateDevicesJsonFile()
        {
            string filePath = ".config/devices.json";
            var fullPath = Path.Combine(FunctionalTestsPath, filePath);

            var request = CreateRequest(ApiRequestType.Devices, Method.GET, null, null);
            string response = request.Content;

            File.WriteAllText(fullPath, response);

            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }
    }
}