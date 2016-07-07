using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArachneControlerDotNet
{
    public class ExecutionFactory
    {
        public Queue<Cuke> Pending;
        public Queue<Cuke> Errors;
        public Queue<Cuke> Running;
        public Queue<Cuke> Stop;
        public Queue<Cuke> Queued;
        public Queue<Cuke> Restart;
        public Queue<Cuke> Stopped;
        public List<Device> Devices;

        public ExecutionFactory ()
        {
            Pending     = new Queue<Cuke> ();
            Errors      = new Queue<Cuke> ();
            Running     = new Queue<Cuke> ();
            Stop        = new Queue<Cuke> ();
            Queued      = new Queue<Cuke> ();
            Restart     = new Queue<Cuke> ();
            Stopped     = new Queue<Cuke> ();
            Devices     = new List<Device> ();
        }

        public int Count ()
        {
            return (Pending.Count + Errors.Count + Running.Count + Stop.Count + Queued.Count + Restart.Count + Stopped.Count);
        }

    }
}

