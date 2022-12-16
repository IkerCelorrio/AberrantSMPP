using System;
using System.Security.Authentication;

namespace TestClient.Facilities
{
	internal class SMPPClientFactory : ISmppClientFactory
	{
		public ISmppClientAdapter CreateClient(
			string name, string password, string host, ushort port,
			SslProtocols supportedSslProtocols = SslProtocols.None, bool disableSslRevocationChecking = false)
		{
			Console.WriteLine("SMPPClient: remoteAddress:{0}:{1} (SslProtocols:{2}, DisableSslRevocationChecking:{3})",
				host, port, supportedSslProtocols, disableSslRevocationChecking);

			return new SMPPClientAdapter(name, password, host, port, supportedSslProtocols, disableSslRevocationChecking);
		}
	}
}
