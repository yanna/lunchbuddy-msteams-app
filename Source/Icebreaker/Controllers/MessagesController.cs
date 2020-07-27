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
    using Icebreaker.Model;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Connector;
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
        private readonly AdminMessageHandler adminMessageHandler;
        private readonly DebugMessageHandler debugMessageHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesController"/> class.
        /// </summary>
        /// <param name="bot">The Icebreaker bot instance</param>
        /// <param name="telemetryClient">The telemetry client instance</param>
        public MessagesController(IcebreakerBot bot, TelemetryClient telemetryClient)
        {
            this.bot = bot;
            this.telemetryClient = telemetryClient;
            this.adminMessageHandler = new AdminMessageHandler(bot, telemetryClient);
            this.debugMessageHandler = new DebugMessageHandler();
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
                    if (activity.Value.ToString().TryParseJson(out EditUserInfoAdaptiveCard.UserInfo userInfo))
                    {
                        await this.HandleSaveUserInfoOrProfile(connectorClient, activity, tenantId, userInfo.UserAadId, userInfo, userInfo.GetStatus());
                    }
                    else if (activity.Value.ToString().TryParseJson(out EditUserProfileAdaptiveCard.UserProfile userProfile))
                    {
                        await this.HandleSaveUserInfoOrProfile(connectorClient, activity, tenantId, senderAadId, userProfile, userStatus: null);
                    }
                    else if (activity.Value.ToString().TryParseJson(out EditTeamSettingsAdaptiveCard.TeamSettings teamSettings))
                    {
                        await this.HandleSaveTeamSettings(connectorClient, activity, teamSettings);
                    }
                    else if (activity.Value.ToString().TryParseJson(out ChooseUserResult chooseUser))
                    {
                        if (chooseUser.MessageId == MessageIds.AdminEditUser)
                        {
                            await this.adminMessageHandler.HandleAdminEditUserForUser(
                                connectorClient, activity, tenantId, chooseUser.GetUserId(), chooseUser.GetUserName());
                        }
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
                else if (this.adminMessageHandler.CanHandleMessage(msg))
                {
                    await this.adminMessageHandler.HandleMessage(msg, connectorClient, activity, senderAadId, senderChannelAccountId);
                }
                else if (this.debugMessageHandler.CanHandleMessage(msg))
                {
                    await this.debugMessageHandler.HandleMessage(msg, connectorClient, activity, senderAadId, senderChannelAccountId);
                }
                else
                {
                    if (!hasTeamContext)
                    {
                        await this.HandleUnrecognizedMsgInOneOnOneChat(connectorClient, activity, tenantId, activity.From);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error while handling message activity: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }
        }

        private async Task HandleUnrecognizedMsgInOneOnOneChat(ConnectorClient connectorClient, Activity activity, string tenantId, ChannelAccount sender)
        {
            var senderAadId = sender.GetUserId();
            var userInfo = await this.bot.GetOrCreateUnpersistedUserInfo(tenantId, senderAadId);

            var hasJoinedBefore = userInfo.Status != EnrollmentStatus.NotJoined;
            var isAdminOfMoreThanOneTeam = userInfo.AdminForTeams.Count > 1;

            if (hasJoinedBefore || isAdminOfMoreThanOneTeam)
            {
                var replyActivity = activity.CreateReply();
                await this.bot.SendUnrecognizedInputMessage(connectorClient, replyActivity, tenantId, senderAadId, userInfo.AdminForTeams, userInfo.Status);
            }
            else
            {
                TeamContext teamContext = null;
                if (userInfo.AdminForTeams.Count == 1)
                {
                    var firstTeamId = userInfo.AdminForTeams.First();
                    teamContext.TeamId = firstTeamId;
                    teamContext.TeamName = await this.bot.GetTeamNameAsync(connectorClient, firstTeamId);
                }

                await this.bot.WelcomeUser(connectorClient, sender, tenantId, teamId: string.Empty, botInstaller: string.Empty, teamContext);
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

        private async Task HandleSaveUserInfoOrProfile(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string senderAadId,
            EditUserProfileAdaptiveCard.UserProfile userProfile,
            EnrollmentStatus? userStatus)
        {
            // Who knows whether users will enter the separator and a space, so split without the space and trim.
            string[] teamsSeparator = { AdaptiveCardHelper.TeamsSeparatorWithSpace.Trim() };
            var splitTeams = userProfile.Subteams.Split(teamsSeparator, StringSplitOptions.RemoveEmptyEntries);
            var subteams = splitTeams.Select(team => team.Trim().ToLowerInvariant()).ToList();

            if (userStatus == null)
            {
                await this.bot.SaveUserProfile(
                    connectorClient,
                    activity,
                    tenantId,
                    senderAadId,
                    userProfile.Discipline,
                    userProfile.Gender,
                    userProfile.Seniority,
                    subteams);
            }
            else
            {
                await this.bot.SaveUserInfo(
                    connectorClient,
                    activity,
                    tenantId,
                    senderAadId,
                    userProfile.Discipline,
                    userProfile.Gender,
                    userProfile.Seniority,
                    subteams,
                    (EnrollmentStatus)userStatus);
            }
        }

        private Task HandleSaveTeamSettings(ConnectorClient connectorClient, Activity activity, EditTeamSettingsAdaptiveCard.TeamSettings teamSettings)
        {
            return this.bot.SaveTeamSettings(
                connectorClient,
                activity,
                teamSettings.TeamId,
                teamSettings.NotifyMode,
                teamSettings.SubteamNames);
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
                    // conversation-update fires whenever a new 1:1 gets created between us and someone else
                    // only process the Teams ones.
                    if (string.IsNullOrEmpty(teamsChannelData?.Team?.Id))
                    {
                        // conversation-update is for 1:1 chat. Just ignore.
                        return;
                    }

                    string myBotId = message.Recipient.Id;
                    string teamId = teamsChannelData.Team.Id;

                    if (message.MembersAdded?.Count() > 0)
                    {
                        foreach (var member in message.MembersAdded)
                        {
                            if (member.Id == myBotId)
                            {
                                await this.HandleAddedBot(connectorClient, message, teamId, tenantId, teamsChannelData?.Team?.Name);
                            }
                            else
                            {
                                await this.bot.SaveAddedUserToTeam(member.GetUserId(), teamId);

                                var installedTeam = await this.bot.GetInstalledTeam(teamId);
                                await this.bot.WelcomeUser(connectorClient, member, tenantId, teamId, installedTeam.InstallerName);
                            }
                        }
                    }

                    if (message.MembersRemoved?.Count() > 0)
                    {
                        foreach (var member in message.MembersRemoved)
                        {
                            if (member.Id == myBotId)
                            {
                                await this.HandleRemovedBot(message, teamId, tenantId);
                            }
                            else
                            {
                                await this.bot.SaveRemovedUserFromTeam(member.GetUserId(), teamId);
                            }
                        }
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

        private async Task HandleAddedBot(ConnectorClient connectorClient, Activity message, string teamId, string tenantId, string teamName)
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
            var personAADId = personThatAddedBot?.GetUserId();

            var addedSuccessfully = await this.bot.SaveAddedBotToTeam(message.ServiceUrl, teamId, tenantId, personName, adminUserAadId: personAADId, adminUserChannelAccountId: personChannelAccountId);

            if (!addedSuccessfully)
            {
                this.telemetryClient.TrackTrace($"Failed to save that the bot was added to team {teamId}", SeverityLevel.Error);
            }

            if (addedSuccessfully && personThatAddedBot != null)
            {
                // Welcome the admin. The team is manually welcomed by the admin through clicking on the "Admin: Send Welcome Card" button as the
                // admin need to edit the subteam names through "Admin: Edit Team Settings" first.
                var adminTeamContext = new TeamContext { TeamId = teamId, TeamName = teamName };
                await this.bot.WelcomeUser(connectorClient, personThatAddedBot, tenantId, teamId, "you", adminTeamContext);
            }
            else if (!addedSuccessfully && personThatAddedBot != null)
            {
                await this.bot.SendFailedToInstall(connectorClient, personThatAddedBot, tenantId);
            }
        }

        private async Task HandleRemovedBot(Activity message, string teamId, string tenantId)
        {
            this.telemetryClient.TrackTrace($"Bot removed from team {teamId}");

            var properties = new Dictionary<string, string>
            {
                { "Scope", message.Conversation?.ConversationType },
                { "TeamId", teamId },
                { "UninstallerId", message.From.Id },
            };
            this.telemetryClient.TrackEvent("AppUninstalled", properties);

            await this.bot.SaveRemoveBotFromTeam(teamId, tenantId);
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
    }
}