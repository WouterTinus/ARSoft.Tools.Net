﻿#region Copyright and License
// Copyright 2010..2023 Alexander Reinert
// 
// This file is part of the ARSoft.Tools.Net - C# DNS client/server and SPF Library (https://github.com/alexreinert/ARSoft.Tools.Net)
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ARSoft.Tools.Net.Dns
{
	/// <summary>
	///   <para>Host identity protocol</para>
	///   <para>
	///     Defined in
	///     <a href="https://www.rfc-editor.org/rfc/rfc5205.html">RFC 5205</a>.
	///   </para>
	/// </summary>
	public class HipRecord : DnsRecordBase
	{
		/// <summary>
		///   Algorithm of the key
		/// </summary>
		public IpSecKeyRecord.IpSecAlgorithm Algorithm { get; private set; }

		/// <summary>
		///   Host identity tag
		/// </summary>
		public byte[] Hit { get; private set; }

		/// <summary>
		///   Binary data of the public key
		/// </summary>
		public byte[] PublicKey { get; private set; }

		/// <summary>
		///   Possible rendezvous servers
		/// </summary>
		public List<DomainName> RendezvousServers { get; private set; }

		internal HipRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, IList<byte> resultData, int currentPosition, int length)
			: base(name, recordType, recordClass, timeToLive)
		{
			int endPosition = currentPosition + length;

			int hitLength = resultData[currentPosition++];
			Algorithm = (IpSecKeyRecord.IpSecAlgorithm) resultData[currentPosition++];
			int publicKeyLength = DnsMessageBase.ParseUShort(resultData, ref currentPosition);
			Hit = DnsMessageBase.ParseByteData(resultData, ref currentPosition, hitLength);
			PublicKey = DnsMessageBase.ParseByteData(resultData, ref currentPosition, publicKeyLength);
			RendezvousServers = new List<DomainName>();
			while (currentPosition < endPosition)
			{
				RendezvousServers.Add(DnsMessageBase.ParseDomainName(resultData, ref currentPosition));
			}
		}

		internal HipRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, DomainName origin, string[] stringRepresentation)
			: base(name, recordType, recordClass, timeToLive)
		{
			if (stringRepresentation.Length < 3)
				throw new FormatException();

			Algorithm = (IpSecKeyRecord.IpSecAlgorithm) Byte.Parse(stringRepresentation[0]);
			Hit = stringRepresentation[1].FromBase16String();
			PublicKey = stringRepresentation[2].FromBase64String();
			RendezvousServers = stringRepresentation.Skip(3).Select(x => ParseDomainName(origin, x)).ToList();
		}

		/// <summary>
		///   Creates a new instace of the HipRecord class
		/// </summary>
		/// <param name="name"> Name of the record </param>
		/// <param name="timeToLive"> Seconds the record should be cached at most </param>
		/// <param name="algorithm"> Algorithm of the key </param>
		/// <param name="hit"> Host identity tag </param>
		/// <param name="publicKey"> Binary data of the public key </param>
		/// <param name="rendezvousServers"> Possible rendezvous servers </param>
		public HipRecord(DomainName name, int timeToLive, IpSecKeyRecord.IpSecAlgorithm algorithm, byte[] hit, byte[] publicKey, List<DomainName> rendezvousServers)
			: base(name, RecordType.Hip, RecordClass.INet, timeToLive)
		{
			Algorithm = algorithm;
			Hit = hit;
			PublicKey = publicKey;
			RendezvousServers = rendezvousServers;
		}

		internal override string RecordDataToString()
		{
			return (byte) Algorithm
			       + " " + Hit.ToBase16String()
			       + " " + PublicKey.ToBase64String()
			       + " " + String.Join(" ", RendezvousServers.Select(s => s.ToString(true)));
		}

		protected internal override int MaximumRecordDataLength
		{
			get
			{
				int res = 4;
				res += Hit.Length;
				res += PublicKey.Length;
				res += RendezvousServers.Sum(s => s.MaximumRecordDataLength + 2);
				return res;
			}
		}

		protected internal override void EncodeRecordData(IList<byte> messageData, ref int currentPosition, Dictionary<DomainName, ushort>? domainNames, bool useCanonical)
		{
			messageData[currentPosition++] = (byte) Hit.Length;
			messageData[currentPosition++] = (byte) Algorithm;
			DnsMessageBase.EncodeUShort(messageData, ref currentPosition, (ushort) PublicKey.Length);
			DnsMessageBase.EncodeByteArray(messageData, ref currentPosition, Hit);
			DnsMessageBase.EncodeByteArray(messageData, ref currentPosition, PublicKey);
			foreach (DomainName server in RendezvousServers)
			{
				DnsMessageBase.EncodeDomainName(messageData, ref currentPosition, server, null, false);
			}
		}
	}
}