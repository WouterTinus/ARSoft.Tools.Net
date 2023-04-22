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

namespace ARSoft.Tools.Net.Dns.DynamicUpdate
{
	/// <summary>
	///   Delete record action
	/// </summary>
	public class DeleteRecordUpdate : UpdateBase
	{
		/// <summary>
		///   Record which should be deleted
		/// </summary>
		public DnsRecordBase Record { get; }

		/// <summary>
		///   Creates a new instance of the DeleteRecordUpdate
		/// </summary>
		/// <param name="record"> Record which should be deleted </param>
		public DeleteRecordUpdate(DnsRecordBase record)
			: base(record.Name)
		{
			Record = record;
		}

		protected override int MaximumRecordDataLength => Record.MaximumRecordDataLength;

		protected override RecordType RecordTypeInternal => Record.RecordType;

        /// <summary>
        /// According to RFC2136 section 2.5.4 (Delete An RR From An RRset) 
        /// "CLASS must be specified as NONE to distinguish this from an RR addition"
        /// </summary>
		protected override RecordClass RecordClassInternal => RecordClass.None;

		protected override int TimeToLive => 0;

		protected override void EncodeRecordData(IList<byte> messageData, ref int currentPosition, Dictionary<DomainName, ushort>? domainNames, bool useCanonical)
		{
			Record.EncodeRecordData(messageData, ref currentPosition, domainNames, useCanonical);
		}
	}
}