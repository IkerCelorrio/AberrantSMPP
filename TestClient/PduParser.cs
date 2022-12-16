using AberrantSMPP.Packet;
using AberrantSMPP.Packet.Request;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestClient
{
	internal class PduParser
	{
		public Pdu Parse(string byteArray)
		{
			byte[] packet = StringToByteArray(byteArray);

			Pdu command = ParseByCommandId(packet);
			return command;
		}

		private Pdu ParseByCommandId(byte[] packet)
		{ 
			var commandId = Pdu.DecodeCommandId(packet);

			switch (commandId)
			{
				case CommandId.deliver_sm:
					return new SmppDeliverSm(packet);
				default:
					break;
			}
			return null;
		}

		private static byte[] StringToByteArray(string hex)
		{
			return Enumerable.Range(0, hex.Length)
							 .Where(x => x % 2 == 0)
							 .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
							 .ToArray();
		}
	}
}
