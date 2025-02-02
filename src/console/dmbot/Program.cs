// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using ConsoleAppFramework;
using dmbot;
using FishyFlip;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.Chat.Bsky.Convo;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;

var app = ConsoleApp.Create();
app.Add<AppCommands>();
app.Run(args);

/// <summary>
/// App Commands.
/// </summary>
#pragma warning disable SA1649 // File name should match first type name
public class AppCommands
#pragma warning restore SA1649 // File name should match first type name
{
    /// <summary>
    /// Create a new direct message.
    /// </summary>
    /// <param name="post">The post to create, can be written using a subset of markdown.</param>
    /// <param name="identifiers">-id, Identifiers to send to.</param>
    /// <param name="username">-u, Username.</param>
    /// <param name="password">-p, Password.</param>
    /// <param name="embedRecord">-r, The record to embed in the post.</param>
    /// <param name="embedRecordCid">-c, The CID of the record to embed in the post. Required if embedding record.</param>
    /// <param name="instanceUrl">-i, Instance URL.</param>
    /// <param name="verbose">-v, Verbose logging.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Task.</returns>
    [Command("dm")]
    public async Task CreateDirectMessageAsync([Argument] string post, string[] identifiers, string username, string password, string? embedRecord = default, string? embedRecordCid = default,  string instanceUrl = "https://public.api.bsky.app", bool verbose = false, CancellationToken cancellationToken = default)
    {
        var consoleLog = new ConsoleLog(verbose);

        ATUri? atUri = null;
        if (!string.IsNullOrEmpty(embedRecord) && !ATUri.TryCreate(embedRecord, out atUri))
        {
            consoleLog.LogError("Invalid record URI for embedding.");
            return;
        }

        if (atUri is not null && string.IsNullOrEmpty(embedRecordCid))
        {
            consoleLog.LogError("CID is required for embedding a record.");
            return;
        }

        var atProtocol = this.GenerateProtocol(instanceUrl, consoleLog);

        if (await this.AuthenticateWithAppPasswordAsync(username, password, atProtocol, consoleLog) == false)
        {
            return;
        }

        var atDids = await this.GenerateATDidsAsync(identifiers, atProtocol, consoleLog, cancellationToken);

        if (atDids.Count == 0)
        {
            return;
        }

        var (convoForMembers, error) = await atProtocol.GetConvoForMembersAsync(atDids, cancellationToken);

        if (error != null)
        {
            consoleLog.LogError(error.ToString());
            return;
        }

        var convoId = convoForMembers?.Convo.Id ?? throw new Exception("Failed to get conversation id.");

        var mdPost = MarkdownPost.Parse(post);

        if (mdPost is null)
        {
            consoleLog.LogError("Failed to parse post.");
            return;
        }

        var messageInput = new MessageInput(mdPost.Post, mdPost.Facets, this.GenerateRecordWithMedia(atUri, embedRecordCid, consoleLog));
        (var convoResult, var convoError) = await atProtocol.SendMessageAsync(convoId, messageInput, cancellationToken);

        if (convoError != null)
        {
            consoleLog.LogError(convoError.ToString());
            return;
        }

        consoleLog.Log($"Message sent to {convoId}.");
    }

    private async Task<List<ATDid>> GenerateATDidsAsync(string[] identifiers, ATProtocol atProtocol, ConsoleLog consoleLog, CancellationToken cancellationToken)
    {
        var atDids = new List<ATDid>();
        foreach (var identifier in identifiers)
        {
            if (!ATIdentifier.TryCreate(identifier, out var atIdentifier))
            {
                consoleLog.LogError("Invalid identifier.");
                return new List<ATDid>();
            }

            if (atIdentifier is ATDid atDid)
            {
                atDids.Add(atDid);
            }
            else if (atIdentifier is ATHandle handle)
            {
                (var handleResult, var handleError) = await atProtocol.Identity.ResolveHandleAsync(handle!, cancellationToken);
                if (handleError != null)
                {
                    consoleLog.LogError(handleError.ToString());
                    return new List<ATDid>();
                }

                atDids.Add(handleResult?.Did ?? throw new Exception("Failed to resolve handle."));
            }
            else
            {
                consoleLog.LogError("Invalid identifier.");
                return new List<ATDid>();
            }
        }

        return atDids;
    }

    private EmbedRecord? GenerateRecordWithMedia(ATUri? atUri, string? cid, ConsoleLog consoleLog)
    {
        if (atUri is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(cid))
        {
            consoleLog.LogError("CID is required for embedding a record.");
            return null;
        }

        var embedRecord = new EmbedRecord(new FishyFlip.Lexicon.Com.Atproto.Repo.StrongRef(atUri!, cid));

        consoleLog.Log($"Embedding record {atUri}.");

        return embedRecord;
    }

    private async Task<bool> AuthenticateWithAppPasswordAsync(string username, string password, ATProtocol atProtocol, ConsoleLog consoleLog)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            consoleLog.LogError("Username and password are required.");
            return false;
        }

        var (result, error) = await atProtocol.AuthenticateWithPasswordResultAsync(username, password);
        if (result is null)
        {
            consoleLog.LogError($"Failed to authenticate as {username}.");
            return false;
        }

        consoleLog.Log($"Authenticated as {username}.");
        return true;
    }

    private ATProtocol GenerateProtocol(string instanceUrl, ConsoleLog consoleLog)
    {
        var atProtocolBuilder = new ATProtocolBuilder();
        if (!Uri.TryCreate(instanceUrl, UriKind.Absolute, out var instanceUri))
        {
            consoleLog.LogError("Invalid instance URL.");
        }
        else
        {
            atProtocolBuilder.WithInstanceUrl(instanceUri);
        }

        if (consoleLog.IsVerbose)
        {
            atProtocolBuilder.WithLogger(consoleLog.Logger);
        }
        else
        {
            atProtocolBuilder.WithLogger(LoggerFactory.Create(builder => { builder.AddDebug(); }).CreateLogger("protocol"));
        }

        var atProtocol = atProtocolBuilder.Build();
        return atProtocol;
    }
}
