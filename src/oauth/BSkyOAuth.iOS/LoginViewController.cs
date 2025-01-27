// <copyright file="LoginViewController.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using BSkyOAuth;
using FishyFlip;

/// <summary>
/// Login View Controller.
/// </summary>
public sealed class LoginViewController : UIViewController
{
    private const string ClientMetadataUrl = "https://drasticactions.vip/client-metadata.json";

    private const string RedirectUri = "vip.drasticactions:/callback";

    private readonly OAuthManager oauthManager;

    private ATProtocol atProtocol;

    private UIButton authButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginViewController"/> class.
    /// </summary>
    public LoginViewController()
    {
        this.oauthManager = new OAuthManager(this, "vip.drasticactions", this.OnSuccess, this.OnError);
        var atProtocolBuilder = new ATProtocolBuilder();
        this.atProtocol = atProtocolBuilder.Build();

        this.View!.BackgroundColor = UIColor.SystemBackground;
        this.authButton = new UIButton(UIButtonType.System);
        this.authButton.SetTitle("Authenticate", UIControlState.Normal);
        this.authButton.TouchUpInside += this.AuthButton_TouchUpInside;
        this.View!.AddSubview(this.authButton);

        this.authButton.TranslatesAutoresizingMaskIntoConstraints = false;
        this.authButton.CenterXAnchor.ConstraintEqualTo(this.View!.CenterXAnchor).Active = true;
        this.authButton.CenterYAnchor.ConstraintEqualTo(this.View!.CenterYAnchor).Active = true;
    }

    private async void AuthButton_TouchUpInside(object? sender, EventArgs e)
    {
        var uri = await this.atProtocol.GenerateOAuth2AuthenticationUrlAsync(
            ClientMetadataUrl,
            RedirectUri,
            new[] { "atproto" },
            null,
            null);
        this.oauthManager.StartAuthentication(uri!);
    }

    private async void OnSuccess(NSUrl? callbackUrl)
    {
        // OnSuccess means we got a successful response from the session, but
        // there may be an error in the response. We need to check for that.
        if (callbackUrl != null)
        {
            var parameters = callbackUrl.Query?.TrimStart('?')
                .Split('&')
                .Select(param => param.Split('='))
                .ToDictionary(split => split[0], split => Uri.UnescapeDataString(split[1])) ?? new Dictionary<string, string>();

            if (parameters.TryGetValue("code", out string? code))
            {
                // If we got a code, we can complete the authentication process.
                var session = await this.atProtocol.AuthenticateWithOAuth2CallbackAsync(callbackUrl.ToString());
                if (session != null)
                {
                    // We have a session!
                    var alert = UIAlertController.Create("Success", $"Authenticated as {session.Handle}", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    this.PresentViewController(alert, true, null);
                }
                else
                {
                    var alert = UIAlertController.Create("Error", "Failed to authenticate", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                    this.PresentViewController(alert, true, null);
                }
            }
            else if (parameters.TryGetValue("error", out string? error))
            {
                var alert = UIAlertController.Create("Error", error, UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                this.PresentViewController(alert, true, null);
            }
        }
    }

    private void OnError(NSError? error)
    {
        var alert = UIAlertController.Create("Error", error!.LocalizedDescription, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        this.PresentViewController(alert, true, null);
    }
}