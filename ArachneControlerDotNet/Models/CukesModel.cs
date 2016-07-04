using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RestSharp;

namespace ArachneControlerDotNet
{
    public class CukesModel
    {
        public string _id { get; set; }

        public string runId { get; set; }

        public string command { get; set; }

        public string status { get; set; }

        public DeviceModel device { get; set; }

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
    }
}


