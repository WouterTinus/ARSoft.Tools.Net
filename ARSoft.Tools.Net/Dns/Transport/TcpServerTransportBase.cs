#region Copyright and License
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

using System.Net;
using System.Net.Sockets;

namespace ARSoft.Tools.Net.Dns;

/// <summary>
///   A abstract base transport used by a server using tcp communication
/// </summary>
/// <typeparam name="TTransport">The type of the implemented transport</typeparam>
/// <typeparam name="TConnection">The type of the connection used by the transport</typeparam>
/// <typeparam name="TStream">The type of the Stream used by the transport</typeparam>
public abstract class TcpServerTransportBase<TTransport, TConnection, TStream> : IServerTransport
	where TTransport : TcpServerTransportBase<TTransport, TConnection, TStream>
	where TConnection : TcpServerTransportBase<TTransport, TConnection, TStream>.TcpServerConnectionBase
	where TStream : Stream
{
	/// <summary>
	///   The read and write timeout of the transport in milliseconds
	/// </summary>
	public int Timeout { get; }

	/// <summary>
	///   The keep alive timeout in milliseconds for waiting for subsequent queries on the same connection
	/// </summary>
	public int KeepAlive { get; }

	private readonly TcpListener _tcpListener;

	/// <summary>
	///   The default allowed response size if no EDNS option is set.
	/// </summary>
	public ushort DefaultAllowedResponseSize => ushort.MaxValue;

	/// <summary>
	///   A value indicating, if the transport supports sending multiple response to a single query.
	/// </summary>
	public bool SupportsMultipleResponses => true;

	/// <summary>
	///   A value indicating, if truncated responses are allowed using this transport.
	/// </summary>
	public bool AllowTruncatedResponses => false;

	/// <summary>
	///   The transport protocol this transport is using
	/// </summary>
	public abstract TransportProtocol TransportProtocol { get; }

	protected TcpServerTransportBase(IPEndPoint bindEndPoint, int timeout = 5000, int keepAlive = 120000)
	{
		Timeout = timeout;
		KeepAlive = keepAlive;
		_tcpListener = new TcpListener(bindEndPoint);
		if (bindEndPoint.Address.Equals(IPAddress.IPv6Any))
			_tcpListener.Server.DualMode = true;
	}

	/// <summary>
	///   Binds the transport to the network stack
	/// </summary>
	public void Bind()
	{
		_tcpListener.Start();
	}

	/// <summary>
	///   Closes the transport
	/// </summary>
	public void Close()
	{
		_tcpListener.Stop();
	}

	protected abstract TConnection CreateConnection(TcpClient client);

	/// <summary>
	///   Waits for a new connection and return the connection
	/// </summary>
	/// <param name="token"> The token to monitor cancellation requests </param>
	/// <returns>A new connection to a client or null, if no connection could not be established</returns>
	public async Task<IServerConnection?> AcceptConnectionAsync(CancellationToken token = default)
	{
		try
		{
			var client = await _tcpListener.AcceptTcpClientAsync(token);
			client.SendTimeout = Timeout;
			client.ReceiveTimeout = Timeout;

			return CreateConnection(client);
		}
		catch
		{
			return null;
		}
	}

	public void Dispose()
	{
		try
		{
			_tcpListener.Stop();
		}
		catch { }
	}

	/// <summary>
	///   The abstract base connection which is used to the server using TCP communitions
	/// </summary>
	public class TcpServerConnectionBase : IServerConnection
	{
		private readonly TcpClient _client;
		protected readonly TStream Stream;
		protected readonly TTransport TransportInternal;

		/// <summary>
		///   The corresponding transport
		/// </summary>
		public IServerTransport Transport => TransportInternal;

		/// <summary>
		///   The remote endpoint of the connection
		/// </summary>
		public IPEndPoint RemoteEndPoint => (IPEndPoint) _client.Client.RemoteEndPoint!;

		/// <summary>
		///   The local endpoint of the connection
		/// </summary>
		public IPEndPoint LocalEndPoint => (IPEndPoint) _client.Client.LocalEndPoint!;

		protected TcpServerConnectionBase(TTransport transport, TcpClient client, TStream stream)
		{
			TransportInternal = transport;
			_client = client;
			Stream = stream;
		}

		/// <summary>
		///   Receives a new package of the client
		/// </summary>
		/// <param name="token"> The token to monitor cancellation requests </param>
		/// <returns>A raw package sent by the client</returns>
		public virtual async Task<DnsReceivedRawPackage?> ReceiveAsync(CancellationToken token = default)
		{
			try
			{
				var requestBuffer = new byte[2];
				if (!await TryReadAsync(requestBuffer, TransportInternal.KeepAlive, token)) // client disconnected while reading or timeout
					return null;

				var offset = 0;
				int requestLength = DnsMessageBase.ParseUShort(requestBuffer, ref offset);

				requestBuffer = new byte[requestLength + DnsRawPackage.LENGTH_HEADER_LENGTH];
				if (!await TryReadAsync(new ArraySegment<byte>(requestBuffer, DnsRawPackage.LENGTH_HEADER_LENGTH, requestLength), TransportInternal.Timeout, token)) // client disconnected while reading or timeout
					return null;

				return new DnsReceivedRawPackage(requestBuffer, RemoteEndPoint, LocalEndPoint);
			}
			catch
			{
				return null;
			}
		}

		private async Task<bool> TryReadAsync(ArraySegment<byte> buffer, int timeout, CancellationToken token)
		{
			try
			{
				var readBytes = 0;
				while (readBytes < buffer.Count)
				{
					if (token.IsCancellationRequested || !_client.IsConnected())
						return false;

					var newReadBytes = await Stream.ReadAsync(buffer.Array!, buffer.Offset + readBytes, buffer.Count - readBytes, token).WithTimeout(timeout, token);

					if (newReadBytes == 0) // timeout
						return false;

					readBytes += newReadBytes;
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		///   Sends a raw package to the client
		/// </summary>
		/// <param name="package">The package, which should be sent to the client</param>
		/// <param name="token"> The token to monitor cancellation requests </param>
		/// <returns>true, of the package could be sent to the client; otherwise, false</returns>
		public virtual async Task<bool> SendAsync(DnsRawPackage package, CancellationToken token = default)
		{
			try
			{
				await Stream.WriteAsync(package.ToArraySegment(true), token);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		///   A value indicating if data could be read from the client
		/// </summary>
		public bool CanRead => _client.Connected;

		public void Dispose()
		{
			try
			{
				Stream.Dispose();
			}
			catch { }

			try
			{
				_client.Dispose();
			}
			catch { }
		}
	}
}