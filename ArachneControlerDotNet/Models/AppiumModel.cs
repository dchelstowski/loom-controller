using System;
using System.Diagnostics;

namespace ArachneControlerDotNet
{
    public class AppiumModel
    {
        public int      Port { get; set; }        

        public Process  Process { get; set; }

        public string   DeviceID { get; set; }
    }
}

