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
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ARSoft.Tools.Net.Dns
{
	/// <summary>
	///   <para>EDNS0 Client Subnet Option</para>
	///   <para>
	///     Defined in
	///     <a href="http://tools.ietf.org/html/draft-vandergaast-edns-client-subnet-02">draft-vandergaast-edns-client-subnet</a>
	///     .
	///   </para>
	/// </summary>
	public class ClientSubnetOption : EDnsOptionBase
	{
		/// <summary>
		///   The address family
		/// </summary>
		public AddressFamily Family => Address.AddressFamily;

		/// <summary>
		///   The source subnet mask
		/// </summary>
		public byte SourceNetmask { get; private set; }

		/// <summary>
		///   The scope subnet mask
		/// </summary>
		public byte ScopeNetmask { get; private set; }

		/// <summary>
		///   The address
		/// </summary>
		public IPAddress Address { get; private set; }

		internal ClientSubnetOption(IList<byte> resultData, int startPosition, int length)
			: base(EDnsOptionType.ClientSubnet)
		{
			var family = DnsMessageBase.ParseUShort(resultData, ref startPosition);
			SourceNetmask = resultData[startPosition++];
			ScopeNetmask = resultData[startPosition++];

			var addressData = new byte[family == 1 ? 4 : 16];
			DnsMessageBase.ParseByteData(resultData, ref startPosition, GetAddressLength()).CopyTo(addressData, 0);

			Address = new IPAddress(addressData);
		}

		/// <summary>
		///   Creates a new instance of the OwnerOption class
		/// </summary>
		/// <param name="sourceNetmask"> The source subnet mask </param>
		/// <param name="address"> The address </param>
		public ClientSubnetOption(byte sourceNetmask, IPAddress address)
			: this(sourceNetmask, 0, address) { }

		/// <summary>
		///   Creates a new instance of the OwnerOption class
		/// </summary>
		/// <param name="sourceNetmask"> The source subnet mask </param>
		/// <param name="scopeNetmask"> The scope subnet mask </param>
		/// <param name="address"> The address </param>
		public ClientSubnetOption(byte sourceNetmask, byte scopeNetmask, IPAddress address)
			: base(EDnsOptionType.ClientSubnet)
		{
			SourceNetmask = sourceNetmask;
			ScopeNetmask = scopeNetmask;
			Address = address;
		}

		internal override ushort DataLength => (ushort) (4 + GetAddressLength());

		internal override void EncodeData(IList<byte> messageData, ref int currentPosition)
		{
			DnsMessageBase.EncodeUShort(messageData, ref currentPosition, (ushort) (Family == AddressFamily.InterNetwork ? 1 : 2));
			messageData[currentPosition++] = SourceNetmask;
			messageData[currentPosition++] = ScopeNetmask;
			DnsMessageBase.EncodeByteArray(messageData, ref currentPosition, Address.GetAddressBytes(), GetAddressLength());
		}

		private int GetAddressLength()
		{
			return (int) Math.Ceiling(SourceNetmask / 8d);
		}
	}
}