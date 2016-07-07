using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ArachneControlerDotNet
{
    public class Device
    {
        public string _id { get; set; }

        public string platformName { get; set; }

        public string deviceName { get; set; }

        public string udid { get; set; }

        public string platformVersion { get; set; }

        public string status { get; set; }

        public string cuke { get; set; }

        public int port { get; set; }

        public int chromePort { get; set; }

        [JsonIgnore]
        public int PID { get; set; }

        [JsonIgnore]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets the status represented in Enum.
        /// </summary>
        /// <value>The get status.</value>
        public DeviceStatus GetStatus {
            get {
                if (!string.IsNullOrEmpty (status)) {
                    switch (status) {
                    case "ready":
                        return DeviceStatus.Ready;
                    case "busy":
                        return DeviceStatus.Busy;
                    case "restart":
                        return DeviceStatus.Restart;
                    }
                }
                return DeviceStatus.EMPTY;
            }
        }

        /// <summary>
        /// Sets the device status.
        /// </summary>
        /// <param name="status">Status.</param>
        public void SetStatus (DeviceStatus status)
        {
            switch (status) {
            case DeviceStatus.Busy:
                this.status = "busy";
                break;
            case DeviceStatus.Ready:
                this.status = "ready";
                break;
            case DeviceStatus.Restart:
                this.status = "restart";
                break;
            }

            ApiRequest.UpdateDeviceStatus (udid, status);
        }

        /// <summary>
        /// Restarts the device.
        /// </summary>
        public void Restart()
        {
            Core.ExecuteShell ("adb", "-s " + _id + " reboot");
        }

        public static void Restart (string udid)
        {
            Core.ExecuteShell("adb", "-s " + udid + " reboot");
        }
    }


}

