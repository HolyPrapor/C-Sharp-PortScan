using System;
using System.Linq;
using System.Net;
using System.Net.Mime;
using CommandLine;

namespace PortScan
{
    class Program
	{
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Specify IP address");
				Console.WriteLine("Usage: ip-address -p {portStart} {portEnd}");
				Console.WriteLine("-t for TCP");
				Console.WriteLine("-u for UDP");
				return;
			}
			var ipAddr = IPAddress.Parse(args[0]);
			var tcpCheck = false;
			var udpCheck = false;
			var portStart = 1;
			var portEnd = 65535;
			for (var i = 1; i < args.Length; i++)
			{
				if (args[i] == "-t")
					tcpCheck = true;
				else if (args[i] == "-u")
					udpCheck = true;
				else if (args[i] == "-p")
				{
					portStart = int.Parse(args[++i]);
					portEnd = int.Parse(args[++i]);
				}
			}
			if (!tcpCheck && !udpCheck)
				tcpCheck = udpCheck = true;
			var scanner = new AsyncPortScanner();
			scanner.Scan(new [] {ipAddr},
				Enumerable.Range(portStart, portEnd - portStart).ToArray(), tcpCheck, udpCheck).Wait();
		}
	}
}
