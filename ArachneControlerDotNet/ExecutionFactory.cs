using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArachneControlerDotNet
{
    public class ExecutionFactory
    {
        public List<CukesModel> Pending;
        public List<CukesModel> Errors;
        public List<CukesModel> Running;
        public List<CukesModel> Stop;
        public List<CukesModel> Queued;
        public List<CukesModel> Restart;
        public List<CukesModel> Stopped;
        public List<DeviceModel> Devices;

        public ExecutionFactory ()
        {
            Pending     = new List<CukesModel> ();
            Errors      = new List<CukesModel> ();
            Running     = new List<CukesModel> ();
            Stop        = new List<CukesModel> ();
            Queued      = new List<CukesModel> ();
            Restart     = new List<CukesModel> ();
            Stopped     = new List<CukesModel> ();
            Devices     = new List<DeviceModel> ();
        }

        public int Count ()
        {
            return (Pending.Count + Errors.Count + Running.Count + Stop.Count + Queued.Count + Restart.Count + Stopped.Count);
        }

    }
}

