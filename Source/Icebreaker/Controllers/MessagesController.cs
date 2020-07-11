//----------------------------------------------------------------------------------------------
// <copyright file="MessagesController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.UI.WebControls;
    using Icebreaker.Controllers;
    using Icebreaker.Helpers;
    using Icebreaker.Helpers.AdaptiveCards;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Properties;

    /// <summary>
    /// Controller for the bot messaging endpoint
    /// </summary>
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private readonly IcebreakerBot bot;
        private readonly TelemetryClient telemetryClient;
        private AdminMessagesHandler adminMessagesHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesController"/> class.
        /// </summary>
        /// <param name="bot">The Icebreaker bot instance</param>
        /// <param name="telemetryClient">The telemetry client instance</param>
        public MessagesController(IcebreakerBot bot, TelemetryClient telemetryClient)
        {
            this.bot = bot;
            this.telemetryClient = telemetryClient;
            this.adminMessagesHandler = new AdminMessagesHandler(bot, telemetryClient);
        }

        /// <summary>
        /// POST: api/messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// <param name="activity">The incoming activity</param>
        /// <returns>Task that resolves to the HTTP response message</returns>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            this.LogActivityTelemetry(activity);

            using (var connectorClient = new ConnectorClient(new Uri(activity.ServiceUrl)))
            {
                if (activity.Type == ActivityTypes.Message)
                {
                    await this.HandleMessageActivity(connectorClient, activity);
                }
                else
                {
                    await this.HandleSystemActivity(connectorClient, activity);
                }
            }

            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task HandleMessageActivity(ConnectorClient connectorClient, Activity activity)
        {
            try
            {
                var senderAadId = activity.From.Properties["aadObjectId"].ToString();
                var senderChannelAccountId = activity.From.Id;
                var teamChannelData = activity.GetChannelData<TeamsChannelData>();
                var tenantId = teamChannelData.Tenant.Id;
                var hasTeamContext = teamChannelData.Team != null;

                // Submit action from an adaptive card results in no text and hopefully some value.
                if (activity.Text == null && activity.Value != null)
                {
                    if (activity.Value.ToString().TryParseJson(out UserProfile userProfile))
                    {
                        await this.HandleSaveProfile(connectorClient, activity, tenantId, senderAadId, userProfile);
                    }
                    else if (activity.Value.ToString().TryParseJson(out TeamSettings teamSettings))
                    {
                        await this.HandleSaveTeamSettings(connectorClient, activity, teamSettings);
                    }

                    return;
                }

                var msg = activity.Text.ToLowerInvariant();

                if (msg == MessageIds.OptOut)
                {
                    await this.HandleOptOut(connectorClient, activity, senderAadId, tenantId);
                }
                else if (msg == MessageIds.OptIn)
                {
                    await this.HandleOptIn(connectorClient, activity, senderAadId, tenantId);
                }
                else if (msg == MessageIds.EditProfile && !hasTeamContext)
                {
                    await this.HandleEditProfile(connectorClient, activity, tenantId, senderAadId);
                }
                else if (this.adminMessagesHandler.CanHandleMessage(msg))
                {
                    await this.adminMessagesHandler.HandleMessage(msg, connectorClient, activity, senderAadId, senderChannelAccountId);
                }
                else if (msg == MessageIds.DebugNotifyUser)
                {
                    await this.HandleDebugNotifyUser(connectorClient, activity, activity.From.AsTeamsChannelAccount());
                }
                else if (msg == MessageIds.DebugWelcomeUser)
                {
                    await this.HandleDebugWelcomeUser(connectorClient, activity, activity.From.AsTeamsChannelAccount());
                }
                else
                {
                    if (!hasTeamContext)
                    {
                        // Unknown input in a personal chat, not in the team channel
                        var replyActivity = activity.CreateReply();
                        await this.bot.SendUnrecognizedInputMessage(connectorClient, replyActivity, tenantId, senderAadId, activity.From.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error while handling message activity: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }
        }

        private async Task HandleOptIn(ConnectorClient connectorClient, Activity activity, string senderAadId, string tenantId)
        {
            // User opted in
            this.telemetryClient.TrackTrace($"User {senderAadId} opted in");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", senderAadId },
                { "OptInStatus", "true" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptInUser(tenantId, senderAadId);

            var optInReply = activity.CreateReply();
            if (isSuccessful)
            {
                optInReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = Resources.OptInConfirmation,
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = Resources.PausePairingsButtonText,
                                DisplayText = Resources.PausePairingsButtonText,
                                Type = ActionTypes.MessageBack,
                                Text = MessageIds.OptOut
                            }
                        }
                    }.ToAttachment(),
                };
            }
            else
            {
                optInReply.Text = Resources.OptInUserFailText;
            }

            await connectorClient.Conversations.ReplyToActivityAsync(optInReply);
        }

        private async Task HandleOptOut(ConnectorClient connectorClient, Activity activity, string senderAadId, string tenantId)
        {
            // User opted out
            this.telemetryClient.TrackTrace($"User {senderAadId} opted out");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", senderAadId },
                { "OptInStatus", "false" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptOutUser(tenantId, senderAadId);

            var optOutReply = activity.CreateReply();

            if (isSuccessful)
            {
                optOutReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = Resources.OptOutConfirmation,
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = Resources.ResumePairingsButtonText,
                                DisplayText = Resources.ResumePairingsButtonText,
                                Type = ActionTypes.MessageBack,
                                Text = MessageIds.OptIn
                            }
                        }
                    }.ToAttachment(),
                };
            }
            else
            {
                optOutReply.Text = Resources.OptOutUserFailText;
            }

            await connectorClient.Conversations.ReplyToActivityAsync(optOutReply);
        }

        private async Task HandleEditProfile(ConnectorClient connectorClient, Activity activity, string tenantId, string senderAadId)
        {
            var replyActivity = activity.CreateReply();
            await this.bot.EditUserProfile(connectorClient, replyActivity, tenantId, senderAadId);
        }

        private async Task HandleSaveProfile(ConnectorClient connectorClient, Activity activity, string tenantId, string senderAadId, UserProfile userProfile)
        {
            // Who knows whether users will enter the separator and a space, split without the space and trim.
            string[] teamsSeparator = { AdaptiveCardHelper.TeamsSeparatorWithSpace.Trim() };
            var splitTeams = userProfile.Teams.Split(teamsSeparator, StringSplitOptions.RemoveEmptyEntries);
            var teams = splitTeams.Select(team => team.Trim().ToLowerInvariant()).ToList();
            await this.bot.SaveUserProfile(
                connectorClient,
                activity,
                tenantId,
                senderAadId,
                userProfile.Discipline,
                userProfile.Gender,
                userProfile.Seniority,
                teams);
        }

        private Task HandleSaveTeamSettings(ConnectorClient connectorClient, Activity activity, TeamSettings teamSettings)
        {
            return this.bot.SaveTeamSettings(
                connectorClient,
                activity,
                teamSettings.TeamId,
                teamSettings.NotifyMode,
                teamSettings.SubteamNames);
        }

        private async Task HandleDebugNotifyUser(ConnectorClient connectorClient, Activity activity, TeamsChannelAccount sender)
        {
            var notifyCard = PairUpNotificationAdaptiveCard.GetCard("TestTeam", sender, sender, "LunchBuddy");

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(notifyCard) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        private async Task HandleDebugWelcomeUser(ConnectorClient connectorClient, Activity activity, TeamsChannelAccount sender)
        {
            var welcomeCard = WelcomeNewMemberAdaptiveCard.GetCard("TestTeam", "Firstname", "LunchBuddy", "InstallerPerson");

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(welcomeCard) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        private async Task HandleSystemActivity(ConnectorClient connectorClient, Activity message)
        {
            this.telemetryClient.TrackTrace("Processing system message");

            try
            {
                var teamsChannelData = message.GetChannelData<TeamsChannelData>();
                var tenantId = teamsChannelData.Tenant.Id;

                if (message.Type == ActivityTypes.ConversationUpdate)
                {
                    // conversation-update fires whenever a new 1:1 gets created between us and someone else as well
                    // only process the Teams ones.
                    if (string.IsNullOrEmpty(teamsChannelData?.Team?.Id))
                    {
                        // conversation-update is for 1:1 chat. Just ignore.
                        return;
                    }

                    string myBotId = message.Recipient.Id;
                    string teamId = message.Conversation.Id;

                    if (message.MembersAdded?.Count() > 0)
                    {
                        foreach (var member in message.MembersAdded)
                        {
                            if (member.Id == myBotId)
                            {
                                this.telemetryClient.TrackTrace($"Bot installed to team {teamId}");

                                var properties = new Dictionary<string, string>
                                {
                                    { "Scope", message.Conversation?.ConversationType },
                                    { "TeamId", teamId },
                                    { "InstallerId", message.From.Id },
                                };
                                this.telemetryClient.TrackEvent("AppInstalled", properties);

                                // Try to determine the name of the person that installed the app, which is usually the sender of the message (From.Id)
                                // Note that in some cases we cannot resolve it to a team member, because the app was installed to the team programmatically via Graph
                                var teamMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);

                                var personThatAddedBot = teamMembers.FirstOrDefault(x => x.Id == message.From.Id);
                                var personName = personThatAddedBot?.Name;
                                var personChannelAccountId = personThatAddedBot?.Id;

                                await this.bot.SaveAddedToTeam(message.ServiceUrl, teamId, tenantId, personName, personChannelAccountId);

                                // Turn this off for now because constantly installing/uninstalling to try new version of the app.
                                // TODO: not big deal to not have it. Perhaps turn it off permanently.
                                // await this.bot.WelcomeTeam(connectorClient, teamId, personName);
                            }
                            else
                            {
                                this.telemetryClient.TrackTrace($"New member {member.Id} added to team {teamsChannelData.Team.Id}");

                                var installedTeam = await this.bot.GetInstalledTeam(teamsChannelData.Team.Id);
                                await this.bot.WelcomeUser(connectorClient, member.Id, tenantId, teamsChannelData.Team.Id, installedTeam.InstallerName);
                            }
                        }
                    }

                    if (message.MembersRemoved?.Any(x => x.Id == myBotId) == true)
                    {
                        this.telemetryClient.TrackTrace($"Bot removed from team {teamId}");

                        var properties = new Dictionary<string, string>
                        {
                            { "Scope", message.Conversation?.ConversationType },
                            { "TeamId", teamId },
                            { "UninstallerId", message.From.Id },
                        };
                        this.telemetryClient.TrackEvent("AppUninstalled", properties);

                        // we were just removed from a team
                        await this.bot.SaveRemoveFromTeam(message.ServiceUrl, teamId, tenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error while handling system activity: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
                throw;
            }
        }

        /// <summary>
        /// Log telemetry about the incoming activity.
        /// </summary>
        /// <param name="activity">The activity</param>
        private void LogActivityTelemetry(Activity activity)
        {
            var fromObjectId = activity.From?.Properties["aadObjectId"]?.ToString();
            var clientInfoEntity = activity.Entities?.Where(e => e.Type == "clientInfo")?.FirstOrDefault();
            var channelData = activity.GetChannelData<TeamsChannelData>();

            var properties = new Dictionary<string, string>
            {
                { "ActivityId", activity.Id },
                { "ActivityType", activity.Type },
                { "UserAadObjectId", fromObjectId },
                {
                    "ConversationType",
                    string.IsNullOrWhiteSpace(activity.Conversation?.ConversationType) ? "personal" : activity.Conversation.ConversationType
                },
                { "ConversationId", activity.Conversation?.Id },
                { "TeamId", channelData?.Team?.Id },
                { "Locale", clientInfoEntity?.Properties["locale"]?.ToString() },
                { "Platform", clientInfoEntity?.Properties["platform"]?.ToString() }
            };
            this.telemetryClient.TrackEvent("UserActivity", properties);
        }

        private class UserProfile
        {
            public string Discipline { get; set; } = string.Empty;

            public string Seniority { get; set; } = string.Empty;

            public string Gender { get; set; } = string.Empty;

            public string Teams { get; set; } = string.Empty;
        }

        private class TeamSettings
        {
            public string TeamId { get; set; } = string.Empty;

            public string NotifyMode { get; set; } = string.Empty;

            public string SubteamNames { get; set; } = string.Empty;
        }
    }
}