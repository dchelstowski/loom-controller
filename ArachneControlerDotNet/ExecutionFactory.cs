using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArachneControlerDotNet
{
    public class ExecutionFactory
    {
        public Queue<CukesModel> Pending;
        public Queue<CukesModel> Errors;
        public Queue<CukesModel> Running;
        public Queue<CukesModel> Stop;
        public Queue<CukesModel> Queued;
        public Queue<CukesModel> Restart;
        public Queue<CukesModel> Stopped;
        public List<DeviceModel> Devices;

        public ExecutionFactory ()
        {
            Pending     = new Queue<CukesModel> ();
            Errors      = new Queue<CukesModel> ();
            Running     = new Queue<CukesModel> ();
            Stop        = new Queue<CukesModel> ();
            Queued      = new Queue<CukesModel> ();
            Restart     = new Queue<CukesModel> ();
            Stopped     = new Queue<CukesModel> ();
            Devices     = new List<DeviceModel> ();
        }

        public int Count ()
        {
            return (Pending.Count + Errors.Count + Running.Count + Stop.Count + Queued.Count + Restart.Count + Stopped.Count);
        }

    }
}

