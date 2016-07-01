using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ArachneControlerDotNet
{
    public class DeviceModel
    {        
        public string   _id { get; set; }

        public string   platformName { get; set; }

        public string   deviceName { get; set; }

        public string   udid { get; set; }

        public string   platformVersion { get; set; }

        public string   status { get; set; }

        public string   cuke { get; set; }

        public int      port { get; set; }

        public int      chromePort { get; set; }

        [JsonIgnore]
        public int      PID { get; set; }

        [JsonIgnore]
        public bool     IsAvailable { get; set; }
    }
}

