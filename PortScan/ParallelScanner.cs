using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PortScan
{
    public class ParallelScanner
    {
	    public virtual Task Scan(IPAddress[] ipAddrs, int[] ports)
	    {
		    var pairs = ipAddrs.SelectMany(ip => ports.Select(port => Tuple.Create(ip, port)));
		    return Task.WhenAll(pairs.Select(pair => Task.FromResult(pair)
			    .ContinueWith(ipPort =>
			    {
				    if (PingAddr(ipPort.Result.Item1) == IPStatus.Success)
					    CheckPort(ipPort.Result.Item1, ipPort.Result.Item2);
			    })));
        }

	    private static IPStatus PingAddr(IPAddress ipAddr, int timeout = 3000)
        {
        	Console.WriteLine($"Pinging {ipAddr}");
        	using(var ping = new Ping())
        	{
        		var status = ping.Send(ipAddr, timeout).Status;
        		Console.WriteLine($"Pinged {ipAddr}: {status}");
        		return status;
        	}
        }

	    private static void CheckPort(IPAddress ipAddr, int port, int timeout = 3000)
        {
        	using(var tcpClient = new TcpClient())
        	{
        		Console.WriteLine($"Checking {ipAddr}:{port}");

        		var connectTask = tcpClient.Connect(ipAddr, port, timeout);
        		PortStatus portStatus;
        		switch(connectTask.Status)
        		{
        			case TaskStatus.RanToCompletion:
        				portStatus = PortStatus.OPEN;
        				break;
        			case TaskStatus.Faulted:
        				portStatus = PortStatus.CLOSED;
        				break;
        			default:
        				portStatus = PortStatus.FILTERED;
        				break;
        		}
        		Console.WriteLine($"Checked {ipAddr}:{port} - {portStatus}");
        	}
        }
    }
}