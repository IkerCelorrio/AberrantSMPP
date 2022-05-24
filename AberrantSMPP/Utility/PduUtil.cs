﻿/* AberrantSMPP: SMPP communication library
 * Copyright (C) 2004, 2005 Christopher M. Bouzek
 * Copyright (C) 2010, 2011 Pablo Ruiz García <pruiz@crt0.net>
 *
 * This file is part of RoaminSMPP.
 *
 * RoaminSMPP is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, version 3 of the License.
 *
 * RoaminSMPP is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lessert General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with RoaminSMPP.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using AberrantSMPP.Packet;

namespace AberrantSMPP.Utility
{
	/// <summary>
	/// Holds common functionality for requests.
	/// </summary>
	public class PduUtil
	{			
		#region constants
		
		private const int SUBADDRESS_MIN = 2;
		private const int SUBADDRESS_MAX = 23;
		
		#endregion constants

		/// <summary>
		/// Do not instantiate
		/// </summary>
		private PduUtil()
		{
		}

		/// <summary>
		/// Gets the encoded representation of the specified text.
		/// </summary>
		/// <param name="coding">The coding.</param>
		/// <param name="text">The text.</param>
		/// <returns></returns>
		public static byte[] GetEncodedText(DataCoding coding, string text)
		{
			switch (coding)
			{
				case DataCoding.SMSCDefault:
					//return GSM7BitEncoding.GetBytes(text);
				case DataCoding.OctetUnspecifiedA:
				case DataCoding.OctetUnspecifiedB:
					return new GSMEncoding(true, false).GetBytes(text);
				case DataCoding.IA5_ASCII:
					return Encoding.ASCII.GetBytes(text);
				case DataCoding.Latin1:
					return Encoding.GetEncoding("iso-8859-1").GetBytes(text);
				case DataCoding.JIS:
				case DataCoding.ExtendedKanjiJIS:
					return Encoding.GetEncoding("EUC-JP").GetBytes(text);
				case DataCoding.Cyrillic:
					return Encoding.GetEncoding("iso-8859-5").GetBytes(text);
				case DataCoding.Latin_Hebrew:
					return Encoding.GetEncoding("iso-8859-8").GetBytes(text);
				case DataCoding.UCS2:
					// 1201 == Unicode Big Endian (FFFE)!
					return Encoding.GetEncoding(1201).GetBytes(text);
				case DataCoding.MusicCodes:
					return Encoding.GetEncoding("iso-2022-jp").GetBytes(text);
				case DataCoding.KS_C:
					return Encoding.GetEncoding("ks_c_5601-1987").GetBytes(text);
				default:
					throw new ArgumentException("Invalid (or unsupported) DataCoding value.");
			}
		}

		/// <summary>
		/// Gets the decoded representation of the specified data.
		/// </summary>
		/// <param name="coding">The coding.</param>
		/// <param name="data">The data.</param>
		/// <returns></returns>
		public static string GetDecodedText(DataCoding coding, byte[] data)
		{
			switch (coding)
			{
				case DataCoding.SMSCDefault:
				//return GSM7BitEncoding.GetBytes(text);
				case DataCoding.OctetUnspecifiedA:
				case DataCoding.OctetUnspecifiedB:
					return new GSMEncoding().GetString(data);
				case DataCoding.IA5_ASCII:
					return Encoding.ASCII.GetString(data);
				case DataCoding.Latin1:
					return Encoding.GetEncoding("iso-8859-1").GetString(data);
				case DataCoding.JIS:
				case DataCoding.ExtendedKanjiJIS:
					return Encoding.GetEncoding("EUC-JP").GetString(data);
				case DataCoding.Cyrillic:
					return Encoding.GetEncoding("iso-8859-5").GetString(data);
				case DataCoding.Latin_Hebrew:
					return Encoding.GetEncoding("iso-8859-8").GetString(data);
				case DataCoding.UCS2:
					// 1201 == Unicode Big Endian (FFFE)!
					return Encoding.GetEncoding(1201).GetString(data);
				case DataCoding.MusicCodes:
					return Encoding.GetEncoding("iso-2022-jp").GetString(data);
				case DataCoding.KS_C:
					return Encoding.GetEncoding("ks_c_5601-1987").GetString(data);
				default:
					throw new ArgumentException("Invalid (or unsupported) DataCoding value.");
			}
		}

		/// <summary>
		/// Inserts the short message into the PDU ArrayList.
		/// </summary>
		/// <param name="pdu">The PDU to put the short message into.</param>
		/// <param name="ShortMessage">The short message to insert.</param>
		/// <returns>The length of the short message.</returns>
		public static byte InsertShortMessage(ArrayList pdu, DataCoding coding, object ShortMessage)
		{
			byte[] msg;
			
			if(ShortMessage == null)
			{
				msg = null;
			}
			else if(ShortMessage is byte[])
			{
				msg =(byte[])ShortMessage;
			}
			else if (ShortMessage is string)
			{
				msg = GetEncodedText(coding, ShortMessage as string);
			}
			else
			{
				throw new ArgumentException("Short Message must be a string or byte array.");
			}
			//			if(msg.Length >= MessageLcd2.SHORT_MESSAGE_LIMIT)
			//				throw new ArgumentException(
			//					"Short message cannot be longer than " +
			//					MessageLcd2.SHORT_MESSAGE_LIMIT + " octets.");
			
			byte SmLength = msg == null ? (byte)0 : (byte)msg.Length;
			pdu.Add(SmLength);
			if (msg != null) pdu.AddRange(msg);
			
			return SmLength;
		}
		
		/// <summary>
		/// Takes the given PDU and inserts a receipted message ID into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="val">The value to insert.</param>
		public static void SetReceiptedMessageId(Pdu pdu, string val)
		{
			const int MAX_RECEIPTED_ID_LEN = 65;

			if (val == null || val.Length <= MAX_RECEIPTED_ID_LEN)
			{
				pdu.SetOptionalParamString(OptionalParamCodes.receipted_message_id, val, true);
			}
			else
			{
				throw new ArgumentException(
					"receipted_message_id must have length 1-" + MAX_RECEIPTED_ID_LEN);
			}
		}

		/// <summary>
		/// Takes the given PDU and inserts a network error code into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="data">The binary encoded error (see spec).</param>
		public static void SetNetworkErrorCode(Pdu pdu, byte[] data)
		{
			const int ERR_CODE_LEN = 3;
			if (data == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.network_error_code, null);
			}
			else if(data.Length != ERR_CODE_LEN)
			{
				throw new ArgumentException("network_error_code must have length " + ERR_CODE_LEN);
			}
			else
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.network_error_code, data);
			}
		}
		
		/// <summary>
		/// Takes the given PDU and inserts ITS session info into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="val">The value to insert.</param>
		public static void SetItsSessionInfo(Pdu pdu, byte[] val)
		{
			const int MAX_ITS = 16;
			
			if(val == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.its_session_info, null);
			}
			else if(val.Length == MAX_ITS)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.its_session_info, val);
			}
			else
			{
				throw new ArgumentException("its_session_info must have length " + MAX_ITS);
			}
		}

		/// <summary>
		/// Takes the given PDU and inserts a destination subaddress into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="data">The data (see spec.)</param>
		public static void SetDestSubaddress(Pdu pdu, byte[] data)
		{
			if (data == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.dest_subaddress, null);
			}
			else if(data.Length >= SUBADDRESS_MIN && data.Length <= SUBADDRESS_MAX)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.dest_subaddress, data);
			}
			else
			{
				throw new ArgumentException(
					"Destination subaddress must be between " + SUBADDRESS_MIN + 
					" and " + SUBADDRESS_MAX + " bytes.");
			}
		}
		
		/// <summary>
		/// Takes the given PDU and inserts a source subaddress into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="val">The value to insert.</param>
		public static void SetSourceSubaddress(Pdu pdu, byte[] data)
		{
			if (data == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.source_subaddress, null);
			}
			else if (data.Length >= SUBADDRESS_MIN && data.Length <= SUBADDRESS_MAX)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.source_subaddress, data);
			}
			else
			{
				throw new ArgumentException(
					"Source subaddress must be between " + SUBADDRESS_MIN + 
					" and " + SUBADDRESS_MAX + " bytes.");
			}
		}
		
		/// <summary>
		/// Takes the given PDU and inserts a callback number into the TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="val">The value to insert.</param>
		public static void SetCallbackNum(Pdu pdu, byte[] val)
		{
			const int CALLBACK_NUM_MIN = 4;
			const int CALLBACK_NUM_MAX = 19;

			if (val == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.callback_num, null);
			}
			else if(val.Length >= CALLBACK_NUM_MIN && val.Length <= CALLBACK_NUM_MAX)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.callback_num, val);
			}
			else
			{
				throw new ArgumentException(
					"callback_num size must be between " + CALLBACK_NUM_MIN + 
					" and " + CALLBACK_NUM_MAX + " characters.");
			}
		}
		
		/// <summary>
		/// Takes the given PDU and inserts a message payload into its TLV table.
		/// </summary>
		/// <param name="pdu">The PDU to operate on.</param>
		/// <param name="val">The value to insert.</param>
		public static void SetMessagePayload(Pdu pdu, DataCoding coding, object val)
		{
			byte[] encodedValue = null;

			if (val == null)
			{
				pdu.SetOptionalParamBytes(OptionalParamCodes.message_payload, null);
			}
			else if(val is string)
			{
				encodedValue = GetEncodedText(coding, val as string);
			}
			else if(val is byte[])
			{
				encodedValue =(byte[])val;
			}
			else
			{
				throw new ArgumentException("Message Payload must be a string or byte array.");
			}
			
			if (encodedValue != null) 
			{
				const int MAX_PAYLOAD_LENGTH = 64000;
				if(encodedValue.Length < MAX_PAYLOAD_LENGTH)
				{
					pdu.SetOptionalParamBytes(
						OptionalParamCodes.message_payload, encodedValue);
				}
				else
				{
					throw new ArgumentException(
						"Message Payload must be " + MAX_PAYLOAD_LENGTH + " characters or less in size.");
				}
			}
		}
		
		/// Trims the trailing zeroes off of the response Pdu.  Useful for
        /// tracing and other purposes.  This uses the command length to
        /// actually trim it down, so TLVs and strings are not lost.  If the
        /// response actually is the same length as the command length, this
        /// method performs a pass-through.
        /// </summary>
        /// <returns>The trimmed Pdu(byte array).</returns>
        public static byte[] TrimResponsePdu(byte[] response)
        {
            uint commLength = Pdu.DecodeCommandLength(response);
            if(commLength == response.Length)
            {
                return response;
            }
            //trap any weird data coming in
            if(commLength >= Int32.MaxValue || commLength > response.Length)
            {
                return new Byte[0];
            }

            byte[] trimmed = new Byte[commLength];
            for(int i = 0; i < trimmed.Length; i++)
            {
                trimmed[i] = response[i];
            }

            return trimmed;
        }
	}
}
