using System;
using System.IO;
using System.Collections.Generic;

namespace ArachneControlerDotNet
{
    class MainClass
    {
        public static void Main (string [] args)
        {
            int argsLength = args.Length;

            string testFrameworkPath;
            string webappUrl;

            TestServer Server = null;
            Console.CursorVisible = false;


            switch (argsLength) {
            case 0:
                testFrameworkPath = "/Users/regressiontests/mobile-functional/appium-tests";
                //testFrameworkPath = "/Users/dchelstowski/RubymineProjects/mobile-functional/appium-tests";
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
