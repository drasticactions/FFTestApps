// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using ConsoleAppFramework;
using FishyFlip;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Lexicon.Chat.Bsky.Convo;
using FishyFlip.Lexicon.Com.Atproto.Repo;
using FishyFlip.Models;
using Microsoft.Extensions.Logging;
using randombot;

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
    /// Create a new random message.
    /// </summary>
    /// <param name="postTxtFilePath">Path to the text file of posts to pick from.</param>
    /// <param name="username">-u, Username.</param>
    /// <param name="password">-p, Password.</param>
    /// <param name="instanceUrl">-i, Instance URL.</param>
    /// <param name="verbose">-v, Verbose logging.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Task.</returns>
    [Command("random")]
    public async Task CreateRandomPostAsync([Argument] string postTxtFilePath, string username, string password, string instanceUrl = "https://public.api.bsky.app", bool verbose = false, CancellationToken cancellationToken = default)
    {
        var consoleLog = new ConsoleLog(verbose);

        if (!File.Exists(postTxtFilePath))
        {
            consoleLog.LogError("Post text file does not exist.");
            return;
        }

        var posts = File.ReadAllLines(postTxtFilePath);

        var atProtocol = this.GenerateProtocol(instanceUrl, consoleLog);

        if (await this.AuthenticateWithAppPasswordAsync(username, password, atProtocol, consoleLog) == false)
        {
            return;
        }

        var index = new Random().Next(0, posts.Length);
        var post = posts[index];

        var (result, error) = await atProtocol.CreatePostAsync(post, cancellationToken: cancellationToken);

        if (error is not null)
        {
            consoleLog.LogError($"Failed to create post: {error}");
            return;
        }

        consoleLog.Log($"Post created: {result!.Uri} - {result!.Cid}");
    }

    /// <summary>
    /// Create a new random message.
    /// </summary>
    /// <param name="postTxtFilePath">Path to the text file of posts to pick from.</param>
    /// <param name="identifier">-id, Identifier to send to.</param>
    /// <param name="username">-u, Username.</param>
    /// <param name="password">-p, Password.</param>
    /// <param name="instanceUrl">-i, Instance URL.</param>
    /// <param name="verbose">-v, Verbose logging.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Task.</returns>
    [Command("random reply")]
    public async Task CreateRandomReplyAsync([Argument] string postTxtFilePath, string identifier, string username, string password, string instanceUrl = "https://public.api.bsky.app", bool verbose = false, CancellationToken cancellationToken = default)
    {
        var consoleLog = new ConsoleLog(verbose);

        if (!File.Exists(postTxtFilePath))
        {
            consoleLog.LogError("Post text file does not exist.");
            return;
        }

        var posts = File.ReadAllLines(postTxtFilePath);

        if (!ATIdentifier.TryCreate(identifier, out var atIdentifier))
        {
            consoleLog.LogError("Invalid identifier.");
            return;
        }

        var atProtocol = this.GenerateProtocol(instanceUrl, consoleLog);

        if (await this.AuthenticateWithAppPasswordAsync(username, password, atProtocol, consoleLog) == false)
        {
            return;
        }

        var (postsResult, postsError) = await atProtocol.Feed.ListPostAsync(atIdentifier!, cancellationToken: cancellationToken);

        if (postsError is not null)
        {
            consoleLog.LogError($"Failed to get posts: {postsError}");
            return;
        }

        if (postsResult is null || postsResult.Records.Count == 0)
        {
            consoleLog.LogError("No posts found.");
            return;
        }

        var index = new Random().Next(0, posts.Length);
        var post = MarkdownPost.Parse(posts[index]);

        var replyDef = new ReplyRefDef(new StrongRef(postsResult.Records[0].Uri, postsResult.Records[0].Cid), new StrongRef(postsResult.Records[0].Uri, postsResult.Records[0].Cid));
        var (result, error) = await atProtocol.CreatePostAsync(post.Post, facets: post.Facets, reply: replyDef, cancellationToken: cancellationToken);

        if (error is not null)
        {
            consoleLog.LogError($"Failed to create post: {error}");
            return;
        }

        consoleLog.Log($"Post created: {result!.Uri} - {result!.Cid}");
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
