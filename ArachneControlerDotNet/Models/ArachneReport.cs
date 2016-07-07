using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ArachneControlerDotNet
{
    public class ArachneReport
    {
        [JsonProperty("device")]
        public Device Device { get; set; }

        [JsonProperty("cuke")]
        public string Cuke { get; set; }

        [JsonProperty("environment")]
        public string Environment { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("report")]
        public List<Feature> Features { get; set; }
    }

    public class Feature
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("scenarios")]
        public List<Scenario> Scenarios { get; set; }
    }

    public class Scenario
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("steps")]
        public List<ScenarioStep> Steps { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }
    }

    public class ScenarioStep
    {
        [JsonProperty("error")]
        public string ErrorMessage { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}

