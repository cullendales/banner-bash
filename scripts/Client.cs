using Godot;
using System;
using System.Net.Sockets;

public partial class Client : Node          // ← must inherit Node
{
	public static Client Instance { get; private set; }

	public override void _EnterTree()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
	}

	[Export] public string Ip   = "127.0.0.1";
	[Export] public int    Port = 7777;

	private const int BufferSize = 4096;
	private TcpClient      _socket;
	private NetworkStream  _stream;
	private readonly byte[] _receiveBuffer = new byte[BufferSize];

	public void ConnectToServer(string ip, int port)
	{
		Ip = ip; Port = port;

		_socket = new TcpClient
		{
			ReceiveBufferSize = BufferSize,
			SendBufferSize    = BufferSize
		};

		GD.Print($"Connecting to {Ip}:{Port}");
		_socket.BeginConnect(Ip, Port, OnConnected, null);
	}

	/* ---------------- CALLBACKS ---------------- */
	private void OnConnected(IAsyncResult ar)
	{
		try
		{
			_socket.EndConnect(ar);
			if (!_socket.Connected)
			{
				GD.PrintErr("Failed to connect (socket not connected).");
				return;
			}

			_stream = _socket.GetStream();
			_stream.BeginRead(_receiveBuffer, 0, BufferSize, OnReceive, null);
			GD.Print("✅ TCP handshake done.");
		}
		catch (Exception e)
		{
			GD.PrintErr($"Connect error: {e}");
		}
	}

	private void OnReceive(IAsyncResult ar)
	{
		try
		{
			int len = _stream.EndRead(ar); // How many bytes arrived
			if (len <= 0) return;   // connection closed

			byte[] data = new byte[len];
			Array.Copy(_receiveBuffer, data, len);

			/* TODO: parse your application-layer packets here
					 e.g. first 4 bytes = packet-ID, etc. */

			_stream.BeginRead(_receiveBuffer, 0, BufferSize, OnReceive, null);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Receive error: {e}");
		}
	}
}
