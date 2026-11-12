// Minimal RCON client for driving the local test server during a comparison run.
// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors
using System.Net.Sockets;
using System.Text;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: rcon <password> <command...>");
    return 2;
}

var password = args[0];
var command = string.Join(' ', args.Skip(1));

using var client = new TcpClient();
client.Connect("127.0.0.1", 25575);
using var stream = client.GetStream();

int Send(int type, string body)
{
    var payload = Encoding.UTF8.GetBytes(body);
    var packet = new byte[14 + payload.Length];
    BitConverter.TryWriteBytes(packet.AsSpan(0, 4), 10 + payload.Length);
    BitConverter.TryWriteBytes(packet.AsSpan(4, 4), Random.Shared.Next(1, int.MaxValue));
    BitConverter.TryWriteBytes(packet.AsSpan(8, 4), type);
    payload.CopyTo(packet, 12);
    stream.Write(packet);
    stream.Flush();
    return BitConverter.ToInt32(packet, 4);
}

string Read()
{
    var lengthBytes = new byte[4];
    stream.ReadExactly(lengthBytes);
    var length = BitConverter.ToInt32(lengthBytes);
    var body = new byte[length];
    stream.ReadExactly(body);
    return Encoding.UTF8.GetString(body, 8, Math.Max(0, length - 10));
}

Send(3, password);
var auth = Read();
if (auth.Length > 0)
{
    Console.Error.WriteLine($"auth failed: {auth}");
    return 1;
}

Send(2, command);
Console.WriteLine(Read());
return 0;
