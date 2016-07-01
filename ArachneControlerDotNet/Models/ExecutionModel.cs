using System;

namespace ArachneControlerDotNet
{
    public class ExecutionModel
    {
        public AppiumModel Appium {get; set; }

        public string CommandId { get; set;}

        public string Command { get; set; }

        public string Device { get; set; }

        public ExecutionModel(string command, string commandId, string device)
        {
            Appium = new AppiumModel();
            Command = command;
            CommandId = commandId;
            Device = device;
        }
    }
}

