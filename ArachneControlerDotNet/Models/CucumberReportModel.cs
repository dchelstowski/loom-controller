using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using ArachneControlerDotNet;


namespace ArachneControlerDotNet
{
    public class Tag
    {
        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("line")]
        public int line { get; set; }
    }

    public class Match
    {
        [JsonProperty("location")]
        public string location { get; set; }
    }

    public class Result
    {
        [JsonProperty("status")]
        public string status { get; set; }

        [JsonProperty("error_message")]
        public string error_message { get; set; }

        [JsonProperty("duration")]
        public long duration { get; set; }
    }

    public class Before
    {
        [JsonProperty("match")]
        public Match match { get; set; }

        [JsonProperty("result")]
        public Result result { get; set; }
    }

    public class Step
    {
        [JsonProperty("keyword")]
        public string keyword { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("line")]
        public int line { get; set; }

        [JsonProperty("match")]
        public Match match { get; set; }

        [JsonProperty("result")]
        public Result result { get; set; }
    }

    public class Embedding
    {
        [JsonProperty("mime_type")]
        public string mime_type { get; set; }

        [JsonProperty("data")]
        public string data { get; set; }
    }

    public class After
    {
        [JsonProperty("match")]
        public Match match { get; set; }

        [JsonProperty("result")]
        public Result result { get; set; }

        [JsonProperty("output")]
        public IList<string> output { get; set; }

        [JsonProperty("embeddings")]
        public IList<Embedding> embeddings { get; set; }
    }

    public class Element
    {
        [JsonProperty("keyword")]
        public string keyword { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("description")]
        public string description { get; set; }

        [JsonProperty("line")]
        public int line { get; set; }

        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("before")]
        public IList<Before> before { get; set; }

        [JsonProperty("steps")]
        public IList<Step> steps { get; set; }

        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("tags")]
        public IList<Tag> tags { get; set; }

        [JsonProperty("after")]
        public IList<After> after { get; set; }

        [JsonProperty("comments")]
        public IList<Comment> comments { get; set; }
    }

    public class Comment
    {
        [JsonProperty("value")]
        public string value { get; set; }

        [JsonProperty("line")]
        public int line { get; set; }
    }

    public class CucumberReportModel
    {
        [JsonProperty("uri")]
        public string uri { get; set; }

        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("keyword")]
        public string keyword { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("description")]
        public string description { get; set; }

        [JsonProperty("line")]
        public int line { get; set; }

        [JsonProperty("tags")]
        public IList<Tag> tags { get; set; }

        [JsonProperty("elements")]
        public IList<Element> elements { get; set; }

        [JsonProperty("comments")]
        public IList<Comment> comments { get; set; }
    }

}