using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;

namespace ArachneControlerDotNet
{
    class MainClass
    {
        public static ConfigFile ConfigFile { get; set; }

        public static void Main (string [] args)
        {
            int argsLength = args.Length;

            TestServer Server = null;

            if (args.Length > 0) {
                if (args [0] == "--config") {
                    MakeConfigFile ();
                }
            }

            ConfigFile = LoadConfigFile ();

            if (ConfigFile != null) {
                if (ConfigFile.Url == null)
                    Server = new TestServer (ConfigFile.Path);
                else
                    Server = new TestServer (ConfigFile.Url, ConfigFile.Path);
            } else { MakeConfigFile (); }

            Console.CursorVisible = false;
            Server.Start ();
        }

        public static void MakeConfigFile ()
        {
            Console.WriteLine ("It looks like you need to have a config file, do you want to create one? (Y/N)");
            ConsoleKeyInfo input = Console.ReadKey ();

            if (input.Key == ConsoleKey.Y) {
                ConfigFile config = new ConfigFile ();
                Console.WriteLine ("Please paste/write the path to your test framework:");
                config.Path = Console.ReadLine ();

                Console.WriteLine ("Do you want to set different server than https://bby-loom.herokuapp.com?");
                Console.WriteLine ("Paste/write the url to your own server, otherwise leave it empty:");
                string urlInput = Console.ReadLine ();

                if (!string.IsNullOrEmpty (urlInput)) {
                    config.Url = urlInput;
                }

                var configData = JsonConvert.SerializeObject (config);
                StreamWriter streamWriter = File.CreateText ("config.dat");
                streamWriter.Write (configData);
                streamWriter.Close ();

                Console.WriteLine ("Config file created! (config.dat)");
                Thread.Sleep (3000);
            } else { Environment.Exit (0); }
        }

        public static ConfigFile LoadConfigFile ()
        {
            if (File.Exists ("config.dat")) {
                var configData = File.ReadAllText ("config.dat");
                var config = JsonConvert.DeserializeObject<ConfigFile> (configData);
                return config;
            }

            Console.WriteLine ("There's no config file!");
            return null;
        }
    }

    public class ConfigFile
    {
        public string Path { get; set; }

        public string Url { get; set; }
    }
}
