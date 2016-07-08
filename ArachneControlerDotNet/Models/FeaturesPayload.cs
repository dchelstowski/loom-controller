using System;
using System.Collections.Generic;

namespace ArachneControlerDotNet
{
    public class TagModel
    {
        public string tag { get; set; }
        public bool feature { get; set; }

        public TagModel (string tagName, bool isFeature)
        {
            this.tag = tagName;
            this.feature = isFeature;
        }
    }

    public class ScenarioModel
    {
        public string scenario { get; set; }
        public int lineNum { get; set; }
        public IList<string> steps { get; set; }

        public ScenarioModel (string scenarioName, int lineNumber)
        {
            this.scenario = scenarioName;
            this.lineNum = lineNumber;
        }
    }

    public class FeatureModel
    {
        public FeatureModel ()
        {
            tags = new List<TagModel> ();
            scenarios = new List<ScenarioModel> ();
        }

        public string path { get; set; }
        public string feature { get; set; }
        public IList<TagModel> tags { get; set; }
        public IList<ScenarioModel> scenarios { get; set; }
    }

    public class FeaturesPayload
    {
        public FeaturesPayload ()
        {
            features = new List<FeatureModel> ();
        }

        public IList<FeatureModel> features { get; set; }
    }
}
