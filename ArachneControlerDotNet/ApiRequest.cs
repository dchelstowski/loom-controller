using System;
using System.Linq;
using RestSharp;

namespace ArachneControlerDotNet
{
    public class ApiRequest
    {
        public static Uri ServerPath { get; set; }

        /// <summary>
        /// Gets the URI.
        /// </summary>
        /// <returns>The URI.</returns>
        /// <param name="type">Type.</param>
        /// <param name="extraRoute">Extra route.</param>
        private static Uri GetUri (ApiRequestType type, string extraRoute)
        {
            Uri finalUri;

            switch (type) {
            case ApiRequestType.Branches:
                if (extraRoute == null)
                    finalUri = new Uri (ServerPath, "api/branches/");
                else
                    finalUri = new Uri (ServerPath, "api/branches/" + extraRoute);
                break;
            case ApiRequestType.Cukes:
                if (extraRoute == null)
                    finalUri = new Uri (ServerPath, "api/cukes/");
                else
                    finalUri = new Uri (ServerPath, "api/cukes/" + extraRoute);
                break;
            case ApiRequestType.Devices:
                if (extraRoute == null)
                    finalUri = new Uri (ServerPath, "api/devices/");
                else
                    finalUri = new Uri (ServerPath, "api/devices/" + extraRoute);
                break;
            case ApiRequestType.Features:
                if (extraRoute == null)
                    finalUri = new Uri (ServerPath, "api/features/");
                else
                    finalUri = new Uri (ServerPath, "api/features/" + extraRoute);
                break;
            case ApiRequestType.Fetch:
                if (extraRoute == null)
                    finalUri = new Uri (ServerPath, "api/fetch/");
                else
                    finalUri = new Uri (ServerPath, "api/fetch/" + extraRoute);
                break;
            case ApiRequestType.Reports:
                finalUri = new Uri (ServerPath, "api/" + extraRoute + "/reports");
                break;
            default:
                if (extraRoute == null)
                    finalUri = ServerPath;
                else
                    finalUri = new Uri (ServerPath, "api/" + extraRoute);
                break;
            }

            return finalUri;
        }

        /// <summary>
        /// Updates the device status.
        /// </summary>
        /// <param name="deviceId">Device identifier.</param>
        /// <param name="status">Status.</param>
        public static bool UpdateDeviceStatus (string deviceId, DeviceStatus status)
        {
            string query = string.Format ("{0}/{1}", deviceId, "status");

            switch (status) {
            case DeviceStatus.Busy:
                query = string.Format ("{0}/{1}", deviceId, "busy");
                break;
            case DeviceStatus.Ready:
                query = string.Format ("{0}/{1}", deviceId, "ready");
                break;
            case DeviceStatus.Restart:
                query = string.Format ("{0}/{1}", deviceId, "restart");
                break;
            case DeviceStatus.Rebooting:
                query = string.Format ("{0}/{1}", deviceId, "rebooting");
                break;
            }


            var request = CreateRequest (ApiRequestType.Devices, Method.POST, query, null);
            if (request.StatusCode == System.Net.HttpStatusCode.OK) {
                Core.PrintLine (string.Format ("Updated device {0} status to {1}", deviceId, status), ConsoleColor.Gray);
                return true;
            }
            Core.PrintLine ("Couldn't update device status.");
            return false;
        }

        /// <summary>
        /// Sets the cuke status.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <param name="commandId">Command identifier.</param>
        public static bool SetCukeStatus (CukeStatus status, string commandId)
        {
            string extraRoute = null;

            switch (status) {
            case CukeStatus.Error:
                extraRoute = string.Format ("{0}/{1}", commandId, "error");
                break;
            case CukeStatus.Done:
                extraRoute = string.Format ("{0}/{1}", commandId, "done");
                break;
            case CukeStatus.Pending:
                extraRoute = string.Format ("{0}/{1}", commandId, "pending");
                break;
            case CukeStatus.Queued:
                extraRoute = string.Format ("{0}/{1}", commandId, "queued");
                break;
            case CukeStatus.Restart:
                extraRoute = string.Format ("{0}/{1}", commandId, "restart");
                break;
            case CukeStatus.Running:
                extraRoute = string.Format ("{0}/{1}", commandId, "running");
                break;
            case CukeStatus.Stop:
                extraRoute = string.Format ("{0}/{1}", commandId, "stop");
                break;
            case CukeStatus.Stopped:
                extraRoute = string.Format ("{0}/{1}", commandId, "stopped");
                break;
            }

            var request = ApiRequest.CreateRequest (ApiRequestType.Cukes, Method.POST, extraRoute, null);
            if (request.StatusCode == System.Net.HttpStatusCode.OK) {
                Core.PrintLine (string.Format ("Updated cuke {0} status to {1}", commandId, status), ConsoleColor.Gray);
                return true;
            }
            Core.PrintLine ("Couldn't update cuke status.");
            return false;
        }


        /// <summary>
        /// Creates the request.
        /// </summary>
        /// <returns>The serialized T object.</returns>
        /// <param name="requestType">Request type.</param>
        /// <param name="extraRoute">Extra route.</param>
        /// <typeparam name="T">The 1st type parameter.</typeparam>
        public static T CreateRequest<T> (ApiRequestType requestType, string extraRoute) where T : new()
        {
            var client = new RestClient (GetUri (requestType, extraRoute));
            var request = new RestRequest (Method.GET);
            request.RequestFormat = DataFormat.Json;

            try {
                var response = client.Execute<T> (request);
                return (T)response.Data;
            } catch (Exception ex) {
                Core.PrintLine ("Error: " + ex.Message, ConsoleColor.Red);
            }
            return default (T);
        }

        /// <summary>
        /// Creates the request.
        /// </summary>
        /// <returns>The API response.</returns>
        /// <param name="requestType">Request type.</param>
        /// <param name="method">Method.</param>
        /// <param name="extraRoute">Extra route.</param>
        /// <param name="payload">Payload.</param>
        /// <param name="filePath">File path.</param>
        public static IRestResponse CreateRequest (ApiRequestType requestType, Method method, string extraRoute, object payload, string filePath = null)
        {

            var client = new RestClient (GetUri (requestType, extraRoute));
            var request = new RestRequest (method);
            request.RequestFormat = DataFormat.Json;
            request.AddHeader ("Content-Type", "application/json");
            IRestResponse response = null;

            try {
                switch (method) {
                case Method.POST:
                    if (payload != null) {
                        if (payload is string) {
                            request.AddParameter ("Application/Json", payload, ParameterType.RequestBody);
                        } else {
                            request.AddJsonBody (payload);
                        }
                    }
                    if (filePath != null)
                        request.AddFile (filePath.Split ('/').Last (), filePath);
                    response = client.Execute (request);
                    return response;

                case Method.DELETE:
                    response = client.Execute (request);
                    return response;

                case Method.GET:
                    response = client.Execute (request);
                    return response;
                }
            } catch (Exception ex) {
                Core.PrintLine ("Error: " + ex.Message, ConsoleColor.Red);
            }

            return response;
        }
    }
}

