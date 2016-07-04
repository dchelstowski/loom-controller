using System;
using System.IO;
using System.Collections.Generic;

namespace ArachneControlerDotNet
{
    class MainClass
    {
        public static void Main (string [] args)
        {
            TestServer Server = null;
            Console.CursorVisible = false;
            string testFrameworkPath;
            string webappUrl;

            int argsLength = args.Length;

            switch (argsLength) {
            case 0:
                testFrameworkPath = "/Users/regressiontests/mobile-functional/appium-tests";
                Server = new TestServer (testFrameworkPath);
                break;
            case 1:
                testFrameworkPath = args [0];
                Server = new TestServer (testFrameworkPath);
                break;
            case 2:
                testFrameworkPath = args [0];
                webappUrl = args [1];
                Server = new TestServer (webappUrl, testFrameworkPath);
                break;
            }

            Server.Start ();
        }
    }
}
