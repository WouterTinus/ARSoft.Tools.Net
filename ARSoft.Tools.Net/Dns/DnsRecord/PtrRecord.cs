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
	///   <para>Domain name pointer</para>
	///   <para>
	///     Defined in
	///     <a href="https://www.rfc-editor.org/rfc/rfc1035.html">RFC 1035</a>.
	///   </para>
	/// </summary>
	public class PtrRecord : DnsRecordBase
	{
		/// <summary>
		///   Domain name the address points to
		/// </summary>
		public DomainName PointerDomainName { get; private set; }

		internal PtrRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, IList<byte> resultData, int currentPosition, int length)
			: base(name, recordType, recordClass, timeToLive)
		{
			PointerDomainName = DnsMessageBase.ParseDomainName(resultData, ref currentPosition);
		}

		internal PtrRecord(DomainName name, RecordType recordType, RecordClass recordClass, int timeToLive, DomainName origin, string[] stringRepresentation)
			: base(name, recordType, recordClass, timeToLive)
		{
			if (stringRepresentation.Length != 1)
				throw new FormatException();

			PointerDomainName = ParseDomainName(origin, stringRepresentation[0]);
		}

		/// <summary>
		///   Creates a new instance of the PtrRecord class
		/// </summary>
		/// <param name="name"> Reverse name of the address </param>
		/// <param name="timeToLive"> Seconds the record should be cached at most </param>
		/// <param name="pointerDomainName"> Domain name the address points to </param>
		public PtrRecord(DomainName name, int timeToLive, DomainName pointerDomainName)
			: base(name, RecordType.Ptr, RecordClass.INet, timeToLive)
		{
			PointerDomainName = pointerDomainName ?? DomainName.Root;
		}

		internal override string RecordDataToString()
		{
			return PointerDomainName.ToString(true);
		}

		protected internal override int MaximumRecordDataLength => PointerDomainName.MaximumRecordDataLength + 2;

		protected internal override void EncodeRecordData(IList<byte> messageData, ref int currentPosition, Dictionary<DomainName, ushort>? domainNames, bool useCanonical)
		{
			DnsMessageBase.EncodeDomainName(messageData, ref currentPosition, PointerDomainName, domainNames, useCanonical);
		}
	}
}