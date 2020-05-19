﻿using System.Net;
using System.Threading.Tasks;

namespace PortScan
{
    public interface IPortScanner
    {
        Task Scan(IPAddress[] ipAdrrs, int[] ports, bool scanTcp, bool scanUdp);
    }
}