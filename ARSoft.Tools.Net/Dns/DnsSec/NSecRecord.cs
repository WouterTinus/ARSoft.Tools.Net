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
	///   <para>Next owner</para>
	///   <para>
	///     Defined in
	///     <a href="https://www.rfc-editor.org/rfc/rfc4034.html">RFC 4034</a>
	///     and
	///     <a href="https://www.rfc-editor.org/rfc/rfc3755.html">RFC 3755</a>.
	///   </para>
	/// </summary>
	public class NSecRecord : DnsRecordBase
	{
		/// <summary>
		///   Name of next owner
		/// </summary>
		public DomainName NextDomainName { get; internal set; }

		/// <summary>
		///   Record types of the next owner
		/// </summary>
		public List<RecordType> Types { get; private set; }

		internal NSecRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, IList<byte> resultData, int currentPosition, int length)
			: base(name, recordType, recordClass, timeToLive)
		{
			int endPosition = currentPosition + length;

			NextDomainName = DnsMessageBase.ParseDomainName(resultData, ref currentPosition);

			Types = ParseTypeBitMap(resultData, ref currentPosition, endPosition);
		}

		internal NSecRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, DomainName origin, string[] stringRepresentation)
			: base(name, recordType, recordClass, timeToLive)
		{
			if (stringRepresentation.Length < 2)
				throw new FormatException();

			NextDomainName = ParseDomainName(origin, stringRepresentation[0]);
			Types = stringRepresentation.Skip(1).Select(RecordTypeHelper.ParseShortString).ToList();
		}

		/// <summary>
		///   Creates a new instance of the NSecRecord class
		/// </summary>
		/// <param name="name"> Name of the record </param>
		/// <param name="recordClass"> Class of the record </param>
		/// <param name="timeToLive"> Seconds the record should be cached at most </param>
		/// <param name="nextDomainName"> Name of next owner </param>
		/// <param name="types"> Record types of the next owner </param>
		public NSecRecord(DomainName name, RecordClass recordClass, int timeToLive, DomainName nextDomainName, List<RecordType> types)
			: base(name, RecordType.NSec, recordClass, timeToLive)
		{
			NextDomainName = nextDomainName ?? DomainName.Root;

			if ((types == null) || (types.Count == 0))
			{
				Types = new List<RecordType>();
			}
			else
			{
				Types = types.Distinct().OrderBy(x => x).ToList();
			}
		}

		internal static List<RecordType> ParseTypeBitMap(IList<byte> resultData, ref int currentPosition, int endPosition)
		{
			List<RecordType> types = new List<RecordType>();
			while (currentPosition < endPosition)
			{
				byte windowNumber = resultData[currentPosition++];
				byte windowLength = resultData[currentPosition++];

				for (int i = 0; i < windowLength; i++)
				{
					byte bitmap = resultData[currentPosition++];

					for (int bit = 0; bit < 8; bit++)
					{
						if ((bitmap & (1 << Math.Abs(bit - 7))) != 0)
						{
							types.Add((RecordType) (windowNumber * 256 + i * 8 + bit));
						}
					}
				}
			}

			return types;
		}

		internal override string RecordDataToString()
		{
			return NextDomainName
			       + " " + String.Join(" ", Types.Select(RecordTypeHelper.ToShortString));
		}

		protected internal override int MaximumRecordDataLength => 2 + NextDomainName.MaximumRecordDataLength + GetMaximumTypeBitmapLength(Types);

		internal static int GetMaximumTypeBitmapLength(List<RecordType> types)
		{
			int res = 0;

			int windowEnd = 255;
			ushort lastType = 0;

			foreach (ushort type in types.Select(t => (ushort) t))
			{
				if (type > windowEnd)
				{
					res += 3 + lastType % 256 / 8;
					windowEnd = (type / 256 + 1) * 256 - 1;
				}

				lastType = type;
			}

			return res + 3 + lastType % 256 / 8;
		}

		protected internal override void EncodeRecordData(IList<byte> messageData, ref int currentPosition, Dictionary<DomainName, ushort>? domainNames, bool useCanonical)
		{
			DnsMessageBase.EncodeDomainName(messageData, ref currentPosition, NextDomainName, null, useCanonical);
			EncodeTypeBitmap(messageData, ref currentPosition, Types);
		}

		internal static void EncodeTypeBitmap(IList<byte> messageData, ref int currentPosition, List<RecordType> types)
		{
			int windowEnd = 255;
			byte[] windowData = new byte[32];
			int windowLength = 0;

			foreach (ushort type in types.Select(t => (ushort) t))
			{
				if (type > windowEnd)
				{
					if (windowLength > 0)
					{
						messageData[currentPosition++] = (byte) (windowEnd / 256);
						messageData[currentPosition++] = (byte) windowLength;
						DnsMessageBase.EncodeByteArray(messageData, ref currentPosition, windowData, windowLength);
					}

					windowEnd = (type / 256 + 1) * 256 - 1;
					windowLength = 0;
				}

				int typeLower = type % 256;

				int octetPos = typeLower / 8;
				int bitPos = typeLower % 8;

				while (windowLength <= octetPos)
				{
					windowData[windowLength] = 0;
					windowLength++;
				}

				byte octet = windowData[octetPos];
				octet |= (byte) (1 << Math.Abs(bitPos - 7));
				windowData[octetPos] = octet;
			}

			if (windowLength > 0)
			{
				messageData[currentPosition++] = (byte) (windowEnd / 256);
				messageData[currentPosition++] = (byte) windowLength;
				DnsMessageBase.EncodeByteArray(messageData, ref currentPosition, windowData, windowLength);
			}
		}

		internal bool IsCovering(DomainName name, DomainName zone)
		{
			return ((name.CompareTo(Name) > 0) && (name.CompareTo(NextDomainName) < 0)) // within zone
			       || ((name.CompareTo(Name) > 0) && NextDomainName.Equals(zone)); // behind zone
		}
	}
}