using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ArachneControlerDotNet
{
    public class FeaturesParser
    {
        public string FrameworkPath { get; set; }

        public string[] FeatureFiles { get; set; }

        public FeaturesParser(string path)
        {
            if (path == null)
                Console.WriteLine("##! PLEASE PROVIDE PATH TO TEST PACKAGE !##", Console.ForegroundColor = ConsoleColor.Red);            

            FrameworkPath = path;
            FeatureFiles = loadFeatureFiles();
        }

        /// <summary>
        /// Loads the feature files.
        /// </summary>
        /// <returns>The feature files.</returns>
        private string[] loadFeatureFiles()
        {

            var features = Directory.GetFiles(Path.Combine(FrameworkPath, "features"), "*.feature", SearchOption.AllDirectories);
            return features;
        }

        /// <summary>
        /// Gets the features payload.
        /// </summary>
        /// <returns>The payloads.</returns>
        public FeaturesPayloadModel GetPayload()
        {
            var payloads = new FeaturesPayloadModel();

            foreach (var file in FeatureFiles)
            {                
                var featureName = file.Split('/').Last().Split('.').First();
                var tags = LoadTags(file);
                var scenarios = LoadScenarios(file);
                payloads.features.Add(PreparePayload(featureName, (List<TagModel>)tags, scenarios, file));
            }

            return payloads;
        }

        /// <summary>
        /// Prepares the payload.
        /// </summary>
        /// <returns>The payload.</returns>
        /// <param name="featureName">Feature name.</param>
        /// <param name="tags">Tags.</param>
        /// <param name="scenarios">Scenarios.</param>
        /// <param name="path">Path.</param>
        private FeatureModel PreparePayload(string featureName, List<TagModel> tags, List<ScenarioModel> scenarios, string path)
        {
            return new FeatureModel()
            {
                path = path,
                feature = featureName,
                tags = tags,
                scenarios = scenarios
            };
        }

        /// <summary>
        /// Loads the scenarios.
        /// </summary>
        /// <returns>The scenarios.</returns>
        /// <param name="file">File.</param>
        private List<ScenarioModel> LoadScenarios(string file)
        {
            List<ScenarioModel> scenarios = new List<ScenarioModel>();
            var ftr = File.ReadAllLines(file);
            for (int i = 0; i < ftr.Length; i++)
            {
                if (ftr[i].Contains("Scenario"))
                    scenarios.Add(new ScenarioModel(ftr[i], i + 1));
            }

            return scenarios;
        }

        /// <summary>
        /// Loads the tags.
        /// </summary>
        /// <returns>The tags.</returns>
        /// <param name="file">File.</param>
        private List<TagModel> LoadTags(string file)
        {
            int ct = 0;
            var tags = new List<TagModel>();

            string[] feature = File.ReadAllLines(file);
            foreach (string line in feature)
            {
                var words = line.Split(' ');
                foreach (var word in words)
                {

                    if (word.StartsWith("@"))
                    {
                        if (ct < 4)
                        {
                            var tag = new TagModel(word, true);
                            tags.Add(tag);
                        }
                        else
                        {
                            var tag = new TagModel(word, false);
                            tags.Add(tag);
                        }
                    }
                }
                ct += 1;
            }
            return tags.Distinct().ToList();
        }
    }
}

