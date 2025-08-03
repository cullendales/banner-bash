using Godot;
using System;

public partial class GameManager : Control
{
	LineEdit _ip, _port;
	Button   _join;

	public override void _Ready()
	{
		_ip   = GetNode<LineEdit>("IP");
		_port = GetNode<LineEdit>("Port");
		_join = GetNode<Button>("Join");

		_join.Pressed += JoinPressed;
	}

	private void JoinPressed()
	{
		string ip = string.IsNullOrWhiteSpace(_ip.Text) ? "127.0.0.1" : _ip.Text;
		int    port = int.TryParse(_port.Text, out var p) ? p : 7777;
		
		Client.Instance.ConnectToServer(ip, port);
	}
}
