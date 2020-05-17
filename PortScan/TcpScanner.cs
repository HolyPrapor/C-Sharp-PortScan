﻿using System.Net;
using System.Threading.Tasks;

namespace PortScan
{
    public interface ITcpPortScanner
    {
        Task Scan(IPAddress[] ipAdrrs, int[] ports);
    }
}