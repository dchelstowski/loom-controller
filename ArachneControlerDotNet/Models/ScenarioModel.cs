using System;
using System.Collections.Generic;

namespace ArachneControlerDotNet
{
    public class ScenarioModel
    {
        public string scenario { get; set; }

        public int lineNum { get; set; }

        public IList<string> steps { get; set; }

        public ScenarioModel(string scenarioName, int lineNumber)
        {
            scenario = scenarioName;
            lineNum = lineNumber;
        }
    }
}

