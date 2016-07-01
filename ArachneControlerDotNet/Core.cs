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
        protected Uri ServerPath { get; set; }

        protected bool _initRun { get; set; }

        protected string FunctionalTestsPath { get; set; }

        protected FeaturesParser FeaturesParser { get; set; }

        protected ExecutionFactory Executions { get; set; }

        protected List<AppiumModel> Processes { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:ArachneControlerDotNet.Core"/> class.
        /// </summary>
        /// <param name="testsPath">Path to directory with Functional Tests.</param>
        public Core(string testsPath)
        {
            Executions = new ExecutionFactory();

            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
            {
                PrintLine("CLOSING SERVER...", ConsoleColor.Red);
                foreach (var proc in Processes)
                {
                    if (proc.Port != 9000)
                    {
                        KillAppiumInstance(proc.Port);
                    }
                }
            };

            _initRun = true;
            Console.Clear();

            Processes = new List<AppiumModel>()
            {
                new AppiumModel()
                {
                    DeviceID = "12345",
                    Process = null,
                    Port = 9000
                }
            };

            FunctionalTestsPath = testsPath;
            FeaturesParser = new FeaturesParser(FunctionalTestsPath);
            LoadFeatures();
        }

        protected void DisposeAppiumResources()
        {

        }

        /// <summary>
        /// Splash screen.
        /// </summary>
        /// <returns>The screen.</returns>
        protected void SplashScreen()
        {
            PrintLine("");
            PrintLine("");
            PrintLine("\t>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> ", ConsoleColor.DarkYellow);
            PrintLine("\t>>                                                      >> ");
            PrintLine("\t>>                    LOOM TEST SERVER                  >> ");
            PrintLine("\t>>                                                      >> ");
            PrintLine("\t>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>> ", ConsoleColor.Green);
            PrintLine("");
            PrintLine("");
        }

        /// <summary>
        /// Creates full URI.
        /// </summary>
        /// <returns>The URI.</returns>
        /// <param name="type">Type.</param>
        /// <param name="extraRoute">Extra route.</param>
        public Uri GetUri(ApiRequestType type, string extraRoute)
        {
            Uri finalUri;

            switch (type)
            {
                case ApiRequestType.Branches:
                    if (extraRoute == null)
                        finalUri = new Uri(ServerPath, "api/branches/");
                    else
                        finalUri = new Uri(ServerPath, "api/branches/" + extraRoute);
                    break;
                case ApiRequestType.Cukes:
                    if (extraRoute == null)
                        finalUri = new Uri(ServerPath, "api/cukes/");
                    else
                        finalUri = new Uri(ServerPath, "api/cukes/" + extraRoute);
                    break;
                case ApiRequestType.Devices:
                    if (extraRoute == null)
                        finalUri = new Uri(ServerPath, "api/devices/");
                    else
                        finalUri = new Uri(ServerPath, "api/devices/" + extraRoute);
                    break;
                case ApiRequestType.Features:
                    if (extraRoute == null)
                        finalUri = new Uri(ServerPath, "api/features/");
                    else
                        finalUri = new Uri(ServerPath, "api/features/" + extraRoute);
                    break;
                case ApiRequestType.Fetch:
                    if (extraRoute == null)
                        finalUri = new Uri(ServerPath, "api/fetch/");
                    else
                        finalUri = new Uri(ServerPath, "api/fetch/" + extraRoute);
                    break;
                case ApiRequestType.Reports:
                    finalUri = new Uri(ServerPath, "api/" + extraRoute + "/reports");
                    break;
                default:
                    if (extraRoute == null)
                        finalUri = ServerPath;
                    else
                        finalUri = new Uri(ServerPath, "api/" + extraRoute);
                    break;
            }

            return finalUri;
        }

        protected string ExecuteShell(string cmd, string parameters, CukesModel cuke = null, string shellId = null, DeviceModel device = null)
        {
            var proc = new Process();

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;

            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null)
                {
                    PrintLine(e.Data);
                }
            };

            proc.StartInfo.FileName = cmd;
            proc.StartInfo.Arguments = parameters;
            proc.Start();

            proc.BeginErrorReadLine();

            if (cmd.Contains("cucumber") || cmd.Contains("appium"))
            {
                PrintLine("PID: " + proc.Id);
                return proc.Id.ToString();
            }

            string output = null;

            try
            {
                using (StreamReader sr = proc.StandardOutput)
                {
                    output = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                PrintLine("Couldn't read stream!");
                PrintLine(e.Message);
            }

            return output;
        }

        protected bool UpdateDeviceStatus(string deviceId, string status)
        {
            var request = CreateRequest(ApiRequestType.Devices, Method.POST, string.Format("{0}/{1}", deviceId, status), null);
            if (request.StatusCode == System.Net.HttpStatusCode.OK)
            {
                PrintLine(string.Format("Updated device {0} status to {1}", deviceId, status), ConsoleColor.Gray);
                return true;
            }
            PrintLine("Couldn't update device status.");
            return false;
        }

        protected void RestartDevice(string deviceId)
        {
            ExecuteShell("adb", "-s " + deviceId + " reboot");
        }

        protected AppiumModel StartAppiumInstance(int chromePort, int appiumPort, string udid)
        {
            string parameters = string.Format("-p {0} -U {1} --chromedriver-port {2}", appiumPort, udid, chromePort);

            Process proc = new Process();
            AppiumModel model = null;

            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.FileName = "appium";
            proc.StartInfo.Arguments = parameters;

            PrintLine(string.Format("Creating new Appium instance: UDID {0}, ChromeDriver: {1}, Appium {2}", udid, chromePort, appiumPort));
            Thread.Sleep(10 * 1000);
            PrintLine("Ready");


            proc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null && !e.Data.Contains("Couldn't start Appium REST http"))
                {
                    PrintLine("appium: " + e.Data, ConsoleColor.Red);
                    if (!model.Process.HasExited)
                        model.Process.Kill();
                    PrintLine("Appium error encountered - rebooting device " + udid);
                    RestartDevice(udid);
                    UpdateDeviceStatus(udid, "rebooting");                    
                    Processes.Remove(model);       
                }
            };

            proc.Start();
            proc.BeginErrorReadLine();

            model = new AppiumModel()
            {
                Process = proc,
                DeviceID = udid,
                Port = appiumPort
            };
            Processes.Add(model);

            return model;
        }

        protected void KillAppiumInstance(int port)
        {
            var appium = Processes.Where(p => p.Port == port).FirstOrDefault();
            Processes.Remove(appium);

            if (!appium.Process.HasExited)
            {
                appium.Process.Kill();
                PrintLine("Killed Appium instance on port: " + port);
            }
        }

        /// <summary>
        /// Creates the request to REST API and returns T object where T is a payload model.
        /// </summary>
        /// <returns>Defined object.</returns>
        /// <param name="requestType">Request type.</param>
        /// <param name="extraRoute">Extra route.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        protected T CreateRequest<T>(ApiRequestType requestType, string extraRoute) where T : new()
        {
            var client = GetHttpClient(GetUri(requestType, extraRoute));
            var request = new RestRequest(Method.GET);
            request.RequestFormat = DataFormat.Json;

            try
            {
                var response = client.Execute<T>(request);
                return (T)response.Data;
            }
            catch (Exception ex)
            {
                PrintLine("Error: " + ex.Message, ConsoleColor.Red);
            }
            return default (T);
        }

        /// <summary>
        /// Creates the request to REST API and returns IRestResponse object.
        /// </summary>
        /// <returns>The request.</returns>
        /// <param name="requestType">Request type.</param>
        /// <param name="method">Method.</param>
        /// <param name="extraRoute">Extra route.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="filePath">File path.</param>
        protected IRestResponse CreateRequest(ApiRequestType requestType, Method method, string extraRoute, object payload, string filePath = null)
        {

            var client = GetHttpClient(GetUri(requestType, extraRoute));
            var request = new RestRequest(method);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader("Content-Type", "application/json");
            IRestResponse response = null;

            try
            {
                switch (method)
                {
                    case Method.POST:
                        if (payload != null)
                        {
                            if (payload.GetType() == typeof(string))
                            {
                                request.AddParameter("Application/Json", payload, ParameterType.RequestBody);
                            }
                            else
                            {
                                request.AddJsonBody(payload);
                            }
                        }
                        if (filePath != null)
                            request.AddFile(filePath.Split('/').Last(), filePath);
                        response = client.Execute(request);
                        return response;

                    case Method.DELETE:
                        response = client.Execute(request);
                        return response;

                    case Method.GET:
                        response = client.Execute(request);
                        return response;
                }
            }
            catch (Exception ex)
            {
                PrintLine("Error: " + ex.Message, ConsoleColor.Red);
            }

            return response;
        }

        /// <summary>
        /// Sends the file to API. (Not tested)
        /// </summary>
        /// <returns>The file.</returns>
        /// <param name="path">Path.</param>
        private bool SendFile(string path)
        {
            var request = CreateRequest(ApiRequestType.Cukes, Method.POST, "results", null, path);
            return request.StatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// Creates instance of the http client.
        /// </summary>
        /// <returns>The http client.</returns>
        /// <param name="uri">URI.</param>
        public RestClient GetHttpClient(Uri uri)
        {
            return new RestClient(uri);
        }

        /// <summary>
        /// Loads the features.
        /// </summary>
        /// <returns>The features.</returns>
        public void LoadFeatures()
        {
            FeaturesParser.GetPayloads();
        }

        /// <summary>
        /// Print the specified value without a newline.
        /// </summary>
        /// <param name="value">Value.</param>
        public void Print(string value)
        {
            Console.Write(value);
        }

        /// <summary>
        /// Print the specified value in specifed color.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="color">Color.</param>
        public void Print(string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(value);
            Console.ForegroundColor = lastColor;
        }

        /// <summary>
        /// Prints the line with specified value.
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="value">Value.</param>
        public void PrintLine(string value)
        {
            var lastColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(DateTime.Now.ToString() + " >> ");
            Console.ForegroundColor = lastColor;

            Console.Write(value + Environment.NewLine);
            Console.WriteLine("");
        }

        /// <summary>
        /// Prints the line in specified font color.
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="value">Value.</param>
        /// <param name="color">Color.</param>
        public void PrintLine(string value, ConsoleColor color)
        {
            var lastColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(DateTime.Now.ToString() + " >> ");

            Console.ForegroundColor = color;
            Console.WriteLine(value);
            Console.ForegroundColor = lastColor;
            Console.WriteLine("");
        }
    }
}

