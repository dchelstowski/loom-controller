using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ArachneControlerDotNet
{
    public class CukesModel
    {
        public string       _id { get; set; }

        public string       runId { get; set; }

        public string       command { get; set; }

        public string       status { get; set; }

        public DeviceModel  device { get; set; }

        [JsonIgnore]
        public bool         IsFinished { get; set; }

        [JsonIgnore]
        public bool         IsRunning { get; set; }

        [JsonIgnore]
        public string       FileName { get; set; }
    }
}

