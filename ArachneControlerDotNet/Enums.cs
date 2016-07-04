using System;

namespace ArachneControlerDotNet
{
    public enum ApiRequestType { Features, Cukes, Devices, Branches, Fetch, Empty, Reports }

    public enum RequestMethod { Post, Delete, Get }

    public enum DeviceType { Android, iOS }

    public enum Branch { Master, Redesign }

    public enum DeviceStatus { Ready, Busy, Restart, Rebooting, EMPTY }

    public enum CukeStatus { Pending, Error, Running, Stop, Queued, Restart, Stopped, Done, EMPTY }
}

