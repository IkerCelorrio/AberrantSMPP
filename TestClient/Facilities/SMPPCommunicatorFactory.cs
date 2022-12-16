using System;
using System.Security.Authentication;

namespace TestClient.Facilities
{
	internal class SMPPCommunicatorFactory : ISmppClientFactory
	{
		public ISmppClientAdapter CreateClient(
			string name, string password, string host, ushort port,
			SslProtocols supportedSslProtocols = SslProtocols.None, bool disableSslRevocationChecking = false)
		{
			Console.WriteLine("SMPPCommunicator: remoteAddress:{0}:{1} (SslProtocols:{2}, DisableSslRevocationChecking:{3})",
				host, port, supportedSslProtocols, disableSslRevocationChecking);
			ISmppClientAdapter client = new SMPPCommunicatorAdapter()
			{
				Host = host,
				Port = port,
				SystemId = name,
				Password = password,
				SupportedSslProtocols = supportedSslProtocols,
				DisableSslRevocationChecking = disableSslRevocationChecking,
			};

			return client;
		}

	}
}
