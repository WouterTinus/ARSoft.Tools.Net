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

using ARSoft.Tools.Net.Dns.DynamicUpdate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ARSoft.Tools.Net.Dns
{
	/// <summary>
	///   Message returned as result to a dns query
	/// </summary>
	public class DnsMessage : DnsRecordMessageBase
	{
		/// <summary>
		///   Parses a the contents of a byte array as DnsMessage
		/// </summary>
		/// <param name="data">Buffer, that contains the message data</param>
		/// <returns>A new instance of the DnsMessage class</returns>
		public static DnsMessage Parse(ArraySegment<byte> data)
		{
			return Parse<DnsMessage>(data);
		}

		#region Header
		/// <summary>
		///   <para>Gets or sets the autoritive answer (AA) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc1035.html">RFC 1035</a>.
		///   </para>
		/// </summary>
		public bool IsAuthoritiveAnswer
		{
			get { return (Flags & 0x0400) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0400;
				}
				else
				{
					Flags &= 0xfbff;
				}
			}
		}

		/// <summary>
		///   <para>Gets or sets the truncated response (TC) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc1035.html">RFC 1035</a>.
		///   </para>
		/// </summary>
		public bool IsTruncated
		{
			get { return (Flags & 0x0200) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0200;
				}
				else
				{
					Flags &= 0xfdff;
				}
			}
		}

		/// <summary>
		///   <para>Gets or sets the recursion desired (RD) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc1035.html">RFC 1035</a>.
		///   </para>
		/// </summary>
		public bool IsRecursionDesired
		{
			get { return (Flags & 0x0100) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0100;
				}
				else
				{
					Flags &= 0xfeff;
				}
			}
		}

		/// <summary>
		///   <para>Gets or sets the recursion allowed (RA) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc1035.html">RFC 1035</a>.
		///   </para>
		/// </summary>
		public bool IsRecursionAllowed
		{
			get { return (Flags & 0x0080) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0080;
				}
				else
				{
					Flags &= 0xff7f;
				}
			}
		}

		/// <summary>
		///   <para>Gets or sets the authentic data (AD) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc4035.html">RFC 4035</a>.
		///   </para>
		/// </summary>
		public bool IsAuthenticData
		{
			get { return (Flags & 0x0020) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0020;
				}
				else
				{
					Flags &= 0xffdf;
				}
			}
		}

		/// <summary>
		///   <para>Gets or sets the checking disabled (CD) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc4035.html">RFC 4035</a>.
		///   </para>
		/// </summary>
		public bool IsCheckingDisabled
		{
			get { return (Flags & 0x0010) != 0; }
			set
			{
				if (value)
				{
					Flags |= 0x0010;
				}
				else
				{
					Flags &= 0xffef;
				}
			}
		}
		#endregion

		/// <summary>
		///   <para>Gets or sets the DNSSEC answer OK (DO) flag</para>
		///   <para>
		///     Defined in
		///     <a href="https://www.rfc-editor.org/rfc/rfc4035.html">RFC 4035</a>
		///     and
		///     <a href="https://www.rfc-editor.org/rfc/rfc3225.html">RFC 3225</a>.
		///   </para>
		/// </summary>
		public bool IsDnsSecOk
		{
			get
			{
				OptRecord? ednsOptions = EDnsOptions;
				return (ednsOptions != null) && ednsOptions.IsDnsSecOk;
			}
			set
			{
				OptRecord? ednsOptions = EDnsOptions;
				if (ednsOptions == null)
				{
					if (value)
					{
						throw new ArgumentOutOfRangeException(nameof(value), "Setting DO flag is allowed in edns messages only");
					}
				}
				else
				{
					ednsOptions.IsDnsSecOk = value;
				}
			}
		}

		/// <summary>
		///   Creates a new instance of the DnsMessage as response to the current instance
		/// </summary>
		/// <returns>A new instance of the DnsMessage as response to the current instance</returns>
		public DnsMessage CreateResponseInstance()
		{
			DnsMessage result = new DnsMessage()
			{
				TransactionID = TransactionID,
				IsEDnsEnabled = IsEDnsEnabled,
				IsQuery = false,
				OperationCode = OperationCode,
				IsRecursionDesired = IsRecursionDesired,
				IsCheckingDisabled = IsCheckingDisabled,
				IsDnsSecOk = IsDnsSecOk,
				Questions = new List<DnsQuestion>(Questions),
			};

			if (IsEDnsEnabled)
			{
				result.EDnsOptions!.Version = EDnsOptions!.Version;
				result.EDnsOptions!.UdpPayloadSize = EDnsOptions!.UdpPayloadSize;
			}

			return result;
		}

		protected internal override DnsMessageBase CreateFailureResponse()
		{
			DnsMessage msg = CreateResponseInstance();
			msg.ReturnCode = ReturnCode.ServerFailure;
			return msg;
		}

		internal override bool IsReliableSendingRequested => (Questions.Count > 0) && ((Questions[0].RecordType == RecordType.Axfr) || (Questions[0].RecordType == RecordType.Ixfr) || (Questions[0].RecordType == RecordType.Any));

		internal override bool IsReliableResendingRequested => IsTruncated;

		internal override bool IsNextMessageWaiting(bool isSubsequentResponseMessage)
		{
			if (isSubsequentResponseMessage)
			{
				return (AnswerRecords.Count > 0) && (AnswerRecords[AnswerRecords.Count - 1].RecordType != RecordType.Soa);
			}

			if (Questions.Count == 0)
				return false;

			if ((Questions[0].RecordType != RecordType.Axfr) && (Questions[0].RecordType != RecordType.Ixfr))
				return false;

			return (AnswerRecords.Count > 0)
			       && (AnswerRecords[0].RecordType == RecordType.Soa)
			       && ((AnswerRecords.Count == 1) || (AnswerRecords[AnswerRecords.Count - 1].RecordType != RecordType.Soa));
		}
	}
}