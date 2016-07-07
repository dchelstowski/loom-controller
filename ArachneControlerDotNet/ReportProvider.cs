using System;
using RestSharp;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ArachneControlerDotNet
{
    public class ReportProvider
    {
        public List<Report> Report { get; set; }

        public ReportProvider(string path, string functionalTestsPath)
        {
            if (File.Exists (Path.Combine (functionalTestsPath, path)))
            {
                var resultFile = File.OpenText (Path.Combine (functionalTestsPath, path));
                var resultJson = resultFile.ReadToEnd ();

                if (Report == null)
                    Report = JsonConvert.DeserializeObject<List<Report>> (resultJson);
            } else
                Console.WriteLine ("DID NOT FIND REPORT FILE.\n" + path);
        }

        public ArachneReport GetFullReport(Device device, string environment, string runId)
        {
            if (Report != null)
            {
                ArachneReport reportModel = new ArachneReport();
                SummaryReport summaryReport = GetSummaryModel();

                reportModel.Device = device;
                reportModel.Cuke = runId;
                reportModel.Environment = environment;
                reportModel.Date = DateTime.Now;
                reportModel.Result = string.Format("Passed: {0} / Skipped: {1} / Failed: {2} / Overall: {3}%", summaryReport.Passed, summaryReport.Skipped, summaryReport.Failed, summaryReport.Percentage);
                reportModel.Features = new List<Feature>();

                foreach (var feature in Report)
                {
                    if (feature.keyword == "Feature")
                    {                    
                        Feature featureObject = new Feature();
                        featureObject.Name = feature.name;                    
                        featureObject.Scenarios = new List<Scenario>();
                        featureObject.Tags = new List<string>();

                        if (feature.tags != null)
                        {
                            foreach (var featureTag in feature.tags)
                            {
                                featureObject.Tags.Add(featureTag.name);
                            }
                        }

                        foreach (var element in feature.elements)
                        {                        
                            Scenario scenario = new Scenario();
                            scenario.Result = "passed";
                            scenario.Name = element.name;
                            scenario.Steps = new List<ScenarioStep>();
                            scenario.Tags = new List<string>();

                            if (element.tags != null)
                            {
                                foreach (var tag in element.tags)
                                {
                                    scenario.Tags.Add(tag.name);
                                }
                            }

                            foreach (var stepElem in element.steps)
                            {
                                ScenarioStep step = new ScenarioStep();                            

                                if (stepElem.result.error_message != null)
                                    step.ErrorMessage = stepElem.result.error_message;

                                step.Name = stepElem.name;
                                step.Result = stepElem.result.status;
                                if (stepElem.result.status == "failed")
                                    scenario.Result = "failed";
                                scenario.Steps.Add(step);
                            }

                            featureObject.Scenarios.Add(scenario);
                        }
                        reportModel.Features.Add(featureObject);
                    }
                }

                return reportModel;
            }
            return null;

        }

        public SummaryReport GetSummaryModel()
        {
            SummaryReport summary = new SummaryReport();
            foreach (var report in Report)
            {                
                for (int i = 0; i < report.elements.Count; i++)
                {
                    foreach (var step in report.elements[i].steps)
                    {
                        switch (step.result.status)
                        {
                            case "passed":
                                summary.Passed++;
                                break;
                            case "skipped":
                                summary.Skipped++;
                                break;
                            case "failed":
                                summary.Failed++;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            summary.Total = summary.Passed + summary.Skipped + summary.Failed;
            summary.Percentage = Math.Round(summary.CountPercent(summary.Passed, summary.Skipped, summary.Failed), 2);

            return summary;
        }
    }
}