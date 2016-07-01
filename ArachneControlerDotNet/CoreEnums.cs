using System;

namespace ArachneControlerDotNet
{
    public enum ApiRequestType { Features, Cukes, Devices, Branches, Fetch, Empty, Reports }

    public enum RequestMethod { Post, Delete, Get }

    public enum DeviceType { Android, iOS }

    public enum Branch { Master, Redesign }
}

