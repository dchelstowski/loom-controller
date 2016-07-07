using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace ArachneControlerDotNet
{
    public class Cuke
    {
        public static CancellationToken CancelationToken { get; set; }

        public static CancellationTokenSource TokenSource { get; set; }

        public string _id { get; set; }

        public string runId { get; set; }

        public string command { get; set; }

        public string status { get; set; }

        public Device device { get; set; }

        [JsonIgnore]
        public bool IsFinished { get; set; }

        [JsonIgnore]
        public bool IsRunning { get; set; }

        [JsonIgnore]
        public string FileName { get; set; }

        [JsonIgnore]
        public CukeStatus GetStatus {
            get {
                if (!string.IsNullOrEmpty (status)) {
                    switch (status) {
                    case "running":
                        return CukeStatus.Running;
                    case "pending":
                        return CukeStatus.Pending;
                    case "error":
                        return CukeStatus.Error;
                    case "stop":
                        return CukeStatus.Stop;
                    case "queued":
                        return CukeStatus.Queued;
                    case "restart":
                        return CukeStatus.Restart;
                    case "stopped":
                        return CukeStatus.Stopped;
                    }
                }
                return CukeStatus.EMPTY;
            }
        }

        public void Assign ()
        {
            switch (GetStatus) 
            {
            case CukeStatus.Pending:
                if (Core.Executions.Pending.FirstOrDefault (m => m._id == _id) == null) 
                {
                    Core.Executions.Pending.Enqueue (this);
                    Core.Executions.Pending.Distinct ();
                }
                break;
            case CukeStatus.Stop:
                if (Core.Executions.Stop.FirstOrDefault (m => m._id == _id) == null) 
                {
                    Core.Executions.Stop.Enqueue (this);
                    Core.Executions.Stop.Distinct ();
                }
                break;
            case CukeStatus.Queued:
                if (Core.Executions.Queued.FirstOrDefault (m => m._id == _id) == null) 
                {
                    Core.Executions.Queued.Enqueue (this);
                    Core.Executions.Queued.Distinct ();
                }
                break;
            }
        }

        /// <summary>
        /// Sets the cuke status.
        /// </summary>
        /// <returns>The status.</returns>
        /// <param name="status">Status.</param>
        public void SetStatus (CukeStatus status)
        {
            switch (status) {
            case CukeStatus.Error:
                this.status = "error";
                break;
            case CukeStatus.Pending:
                this.status = "pending";
                break;
            case CukeStatus.Queued:
                this.status = "queued";
                break;
            case CukeStatus.Restart:
                this.status = "restart";
                break;
            case CukeStatus.Running:
                this.status = "running";
                break;
            case CukeStatus.Stop:
                this.status = "stop";
                break;
            case CukeStatus.Stopped:
                this.status = "stopped";
                break;
            }

            ApiRequest.SetCukeStatus (status, _id);
        }

        /// <summary>
        /// Removes the cuke.
        /// </summary>
        public bool Remove ()
        {
            var request = ApiRequest.CreateRequest (ApiRequestType.Cukes, Method.DELETE, _id, null, null);
            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }

        public void Stop ()
        {
            TokenSource.Cancel ();
        }

        /// <summary>
        /// Executes cuke.
        /// </summary>
        public void Execute ()
        {
            if (!IsRunning && !IsFinished) 
            {
                if (device != null && device.IsAvailable) 
                {
                    Core.Executions.Running.Enqueue (this);
                    device.SetStatus (DeviceStatus.Busy);
                    SetStatus (CukeStatus.Running);
                    IsRunning = true;
                    device.IsAvailable = false;
                    TestServer.RunningCukes.Add (_id);
                    Task.Factory.StartNew (() => CallAsyncExecutionMethod (this, CancelationToken));
                }
                else 
                {
                    if (GetStatus != CukeStatus.Queued) 
                    {
                        ApiRequest.SetCukeStatus (CukeStatus.Queued, _id);
                        SetStatus (CukeStatus.Queued);
                    }
                }
            }
        }

        /// <summary>
        /// Calls the async execution method.
        /// </summary>
        /// <param name="cuke">Cuke.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected async void CallAsyncExecutionMethod (Cuke cuke, CancellationToken cancellationToken)
        {
            Core.PrintLine (string.Format ("TASK STARTED [device: {0}] [cuke: {1}]", cuke.device, cuke._id), ConsoleColor.Green);
            Task<bool> execution = HandleExecution (cuke, cancellationToken);
            await Task.Delay (5 * 1000);

            bool result = await execution;

            if (result) 
            {
                cuke.IsFinished = true;
                cuke.IsRunning = false;
                var currentDevice = Core.Executions.Devices.Find (d => d.udid == cuke.device.udid);
                var reportProvider = new ReportProvider ("reports/" + cuke.FileName, Core.FunctionalTestsPath);
                var report = reportProvider.GetFullReport (currentDevice, "QA", cuke._id); // hardcoded environment!

                if (report != null) 
                {
                    Core.PrintLine (report.Result, ConsoleColor.Green);

                    var jsonPayload = JsonConvert.SerializeObject (report);

                    var request = ApiRequest.CreateRequest (
                                      ApiRequestType.Reports,
                                      Method.POST,
                                      string.Format ("{0}", cuke._id),
                                      jsonPayload,
                                      null);

                    if (request.StatusCode == System.Net.HttpStatusCode.OK)
                        Core.PrintLine ("Execution report sent!", ConsoleColor.Green);
                    else
                        Core.PrintLine ("Execution report upload failed!", ConsoleColor.Green);

                    Core.PrintLine (string.Format ("EXECUTION FINISHED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id), ConsoleColor.Green);
                } 
                else 
                {
                    Core.PrintLine (string.Format ("EXECUTION CANCELED [device: {0}] [cuke: {1}]", cuke.device.deviceName, cuke._id));
                    cuke.SetStatus (CukeStatus.Error);
                    cuke.device.SetStatus (DeviceStatus.Ready);
                    return;
                }

                Appium.Kill (cuke.device.port);
                Thread.Sleep (1000);
                Appium.Start (cuke.device.chromePort, cuke.device.port, cuke.device.udid);
                currentDevice.SetStatus (DeviceStatus.Ready);
            }
        }

        /// <summary>
        /// Handles the execution.
        /// </summary>
        /// <returns>The bool value representing test result.</returns>
        /// <param name="cuke">Cuke.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected Task<bool> HandleExecution (Cuke cuke, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool> ();

            Directory.SetCurrentDirectory (Core.FunctionalTestsPath);

            var cucumberCommand = cuke.command.Split (new string [] { "cucumber" }, StringSplitOptions.RemoveEmptyEntries) [0];

            Core.PrintLine (string.Format ("[NEW COMMAND] CUKE: {0} / DEVICE: {1}", cuke._id, cuke.device.deviceName), ConsoleColor.Magenta);

            string jsonResultFile = string.Format ("{0}ArachneExecutionReport_{1}.txt", DateTime.Now.ToString ("yyMMdd-hhmm"), cuke._id);
            cuke.FileName = jsonResultFile;

            var proc = new Process ();

            proc.EnableRaisingEvents = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "cucumber";
            proc.StartInfo.Arguments = cucumberCommand + " -f json -o reports/" + jsonResultFile;

            Core.PrintLine ("cucumber" + cucumberCommand + " -f json -o reports/" + jsonResultFile);

            proc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => 
            {
                Core.PrintLine ("[" + cuke.device + "]" + " Selenium output: " + e.Data);

                if (CancelationToken.IsCancellationRequested) 
                {
                    tcs.SetResult (false);
                    TestServer.RunningCukes.Remove (cuke._id);
                    tcs.SetCanceled ();
                    cuke.device.SetStatus (DeviceStatus.Ready);
                    cuke.IsFinished = true;
                    cuke.IsRunning = false;
                    Core.Executions.Devices.Find (d => d.udid == cuke.device.udid).IsAvailable = true;
                    Core.Executions.Running.FirstOrDefault(m => m._id == cuke._id).Remove();
                    cuke.SetStatus (CukeStatus.Stopped);
                    return;
                }
            };

            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
                if (e.Data != null && e.Data.Contains ("Selenium::WebDriver::Error")) {
                    Core.PrintLine (e.Data);
                    tcs.SetResult (false);
                }
            };

            proc.Exited += (object sender, EventArgs e) => {
                tcs.SetResult (true);
                TestServer.RunningCukes.Remove (cuke._id);
                cuke.SetStatus (CukeStatus.Done);
                cuke.IsFinished = true;
                cuke.IsRunning = false;
                Core.Executions.Running.FirstOrDefault(m => m._id == cuke._id).Remove();
                Core.Executions.Devices.Find (d => d.udid == cuke.device.udid).IsAvailable = true;
                Task.Delay (5000);
                proc.Dispose ();
            };

            proc.Start ();
            proc.BeginErrorReadLine ();
            proc.WaitForExit ();

            return tcs.Task;
        }
    }
}


