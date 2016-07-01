using System;

namespace ArachneControlerDotNet
{
    public class TagModel
    {
        public bool feature { get; set; }

        public string tag { get; set; }

        public TagModel(bool isFeature, string tagName)
        {
            feature = isFeature;
            tag = tagName;
        }
    }
}



