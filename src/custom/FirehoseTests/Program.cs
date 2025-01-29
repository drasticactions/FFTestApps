// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using FishyFlip;
using FishyFlip.Models;

var atWebSocketProtocolBuilder = new ATWebSocketProtocolBuilder();
var atWebSocketProtocol = atWebSocketProtocolBuilder.Build();

atWebSocketProtocol.OnRecordReceived += (sender, e) =>
{
    if (e.Record is UnknownATObject unknown)
    {
        Console.WriteLine($"Unknown Message Received: {e.FrameCommit.Time.ToString()} -> {e.Record?.Type} -> {e.Record?.ToJson()}");
    }
};

atWebSocketProtocol.OnConnectionUpdated += (sender, e) =>
{
    Console.WriteLine($"State: {e.State}");
};

_ = atWebSocketProtocol.StartSubscribeReposAsync();

Console.Read();

await atWebSocketProtocol.StopSubscriptionAsync();
