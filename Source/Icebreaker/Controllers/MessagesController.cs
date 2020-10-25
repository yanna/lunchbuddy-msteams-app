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
    using Icebreaker.Controllers;
    using Icebreaker.Helpers;
    using Icebreaker.Helpers.AdaptiveCards;
    using Icebreaker.Helpers.HeroCards;
    using Icebreaker.Model;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams.Models;
    using Newtonsoft.Json;
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
                var senderName = activity.From.Name;

                var teamChannelData = activity.GetChannelData<TeamsChannelData>();
                var tenantId = teamChannelData.Tenant.Id;
                var isMessageInChannel = teamChannelData.Team != null;

                // Submit action from an adaptive card results in no text and a value.
                if (activity.Text == null && activity.Value != null)
                {
                    if (activity.Value.ToString().TryParseJson(out EditUserProfileAdaptiveCard.UserProfile userProfile))
                    {
                        // Do not user the senderAadId as this can be triggered by an Admin for another user
                        await this.HandleSaveUserProfile(connectorClient, activity, tenantId, userProfile.UserId, userProfile);
                    }
                    else if (activity.Value.ToString().TryParseJson(out EditTeamSettingsAdaptiveCard.TeamSettings teamSettings))
                    {
                        await this.HandleSaveTeamSettings(connectorClient, activity, teamSettings, tenantId);
                    }
                    else if (activity.Value.ToString().TryParseJson(out ChooseUserResult chooseUserResult))
                    {
                        if (this.adminMessageHandler.CanHandleMessage(chooseUserResult.MessageId))
                        {
                            await this.adminMessageHandler.HandleMessage(chooseUserResult.MessageId, connectorClient, activity, senderAadId);
                        }
                    }

                    return;
                }

                var msg = activity.Text;

                if (msg == MessageIds.OptOut)
                {
                    var userAndTeam = ActivityHelper.GetUserAndTeam(activity, senderAadId, senderName);
                    await this.HandleOptOut(
                        connectorClient,
                        activity,
                        tenantId,
                        userAndTeam,
                        userAndTeam.User.UserAadId != senderAadId);
                }
                else if (msg == MessageIds.OptIn)
                {
                    var userAndTeam = ActivityHelper.GetUserAndTeam(activity, senderAadId, senderName);
                    await this.HandleOptIn(
                        connectorClient,
                        activity,
                        tenantId,
                        userAndTeam);
                }
                else if (msg == MessageIds.EditProfile)
                {
                    var userAndTeam = ActivityHelper.GetUserAndTeam(activity, senderAadId, senderName);
                    await this.bot.EditUserProfile(connectorClient, activity.CreateReply(), tenantId, userAndTeam, userAndTeam.User.UserAadId != senderAadId);
                }
                else if (this.adminMessageHandler.CanHandleMessage(msg))
                {
                    await this.adminMessageHandler.HandleMessage(msg, connectorClient, activity, senderAadId);
                }
                else if (this.debugMessageHandler.CanHandleMessage(msg))
                {
                    var teams = await this.bot.GetAllTeams(connectorClient);
                    await this.debugMessageHandler.HandleMessage(msg, connectorClient, activity, teams.FirstOrDefault()?.TeamId);
                }
                else
                {
                    if (isMessageInChannel)
                    {
                        await this.bot.SendUnrecognizedChannelMessage(connectorClient, activity, activity.GetChannelData<TeamsChannelData>().Team.Id);
                    }
                    else
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
            // We either display a welcome user message or a simple unrecognized message.
            // Both will list all the actions you can do and admin actions if the user is an admin.

            // 1. Determine the team context.
            // 2. If we don't have just one, ask for which team to perform actions for
            // 3. Show welcome user if the user has not joined that team
            // 4. Show unrecognized message if the user has joined that team before.
            TeamContext teamForActions = null;

            // Try to get it from the message if this was from the welcome team "Chat with me" action.
            var teamIdFromWelcomeTeam = AdaptiveCardHelper.GetTeamIdFromChatWithMeMessage(activity.Text);
            if (!string.IsNullOrEmpty(teamIdFromWelcomeTeam))
            {
                string teamName = await this.bot.GetTeamNameAsync(connectorClient, teamIdFromWelcomeTeam);
                teamForActions = new TeamContext { TeamId = teamIdFromWelcomeTeam, TeamName = teamName };
            }
            else
            {
                // Extract it from the ChooseTeamHeroCard response
                var userAndTeam = ActivityHelper.GetUserAndTeam(activity, sender.GetUserId(), sender.Name);
                teamForActions = userAndTeam?.Team;
            }

            if (teamForActions == null)
            {
                var allTeams = await this.bot.GetAllTeams(connectorClient);

                if (allTeams.Count > 1)
                {
                    var chooseTeamCard = ChooseTeamHeroCard.GetCard(Resources.UnrecognizedInputChooseTeam, allTeams, actionMessage: activity.Text);
                    await ActivityHelper.ReplyWithHeroCard(connectorClient, activity, chooseTeamCard);
                    return;
                }

                if (allTeams.Count == 0)
                {
                    await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(Resources.UnrecognizedInputNoTeam));
                    return;
                }

                teamForActions = allTeams.First();
            }

            var userInfo = await this.bot.GetOrCreateUnpersistedUserInfo(tenantId, sender.GetUserId());
            var userStatus = userInfo.GetStatusInTeam(teamForActions.TeamId);
            var isUserAdminOfTeam = userInfo.AdminForTeams.Contains(teamForActions.TeamId);

            if (userStatus == EnrollmentStatus.NotJoined)
            {
                await this.bot.WelcomeUser(connectorClient, sender, tenantId, teamForActions, userStatus, botInstallerName: string.Empty, isUserAdminOfTeam);
            }
            else
            {
                await this.bot.SendUnrecognizedOneOnOneMessage(connectorClient, activity.CreateReply(), teamForActions, userStatus, isUserAdminOfTeam);
            }
        }

        private async Task HandleOptIn(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            UserAndTeam userAndTeam)
        {
            var userId = userAndTeam.User.UserAadId;
            var userName = userAndTeam.User.UserName;
            var teamId = userAndTeam.Team.TeamId;
            var teamName = userAndTeam.Team.TeamName;

            // User opted in
            this.telemetryClient.TrackTrace($"User {userId} opted in");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", userId },
                { "OptInStatus", "true" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptInUser(tenantId, userId, teamId);

            var optInReply = activity.CreateReply();
            if (isSuccessful)
            {
                var actionText = string.Format(Resources.PausePairingsButtonText, teamName);

                optInReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = string.Format(Resources.OptInConfirmation, userName, teamName),
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = actionText,
                                DisplayText = actionText,
                                Type = ActionTypes.MessageBack,
                                Text = MessageIds.OptOut,
                                Value = JsonConvert.SerializeObject(userAndTeam)
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

        private async Task HandleOptOut(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            UserAndTeam userAndTeam,
            bool isAnotherUser)
        {
            var userId = userAndTeam.User.UserAadId;
            var userName = userAndTeam.User.UserName;
            var teamId = userAndTeam.Team.TeamId;
            var teamName = userAndTeam.Team.TeamName;

            // User opted out
            this.telemetryClient.TrackTrace($"User {userId} opted out");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", userId },
                { "OptInStatus", "false" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptOutUser(tenantId, userId, teamId);

            var optOutReply = activity.CreateReply();

            if (isSuccessful)
            {
                  var text = isAnotherUser ? string.Format(Resources.OptOutConfirmationAnotherUser, userName, teamName) :
                    string.Format(Resources.OptOutConfirmation, teamName);

                var actionText = string.Format(Resources.ResumePairingsButtonText, teamName);

                optOutReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = text,
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = actionText,
                                DisplayText = actionText,
                                Type = ActionTypes.MessageBack,
                                Text = MessageIds.OptIn,
                                Value = JsonConvert.SerializeObject(userAndTeam)
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

        private async Task HandleSaveUserProfile(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string userId,
            EditUserProfileAdaptiveCard.UserProfile userProfile)
        {
            var subteams = EditUserProfileAdaptiveCard.GetSubteams(userProfile.Subteams);
            var lowPreferenceNames = EditUserProfileAdaptiveCard.GetSubteams(userProfile.LowPreferenceNames);

            await this.bot.SaveUserProfile(
                connectorClient,
                activity,
                tenantId,
                userId,
                userProfile.Discipline,
                userProfile.Gender,
                userProfile.Seniority,
                subteams,
                lowPreferenceNames);
        }

        private async Task HandleSaveTeamSettings(ConnectorClient connectorClient, Activity activity, EditTeamSettingsAdaptiveCard.TeamSettings teamSettings, string tenantId)
        {
            var adminUserId = string.Empty;

            // Get the name from the original id if we can, otherwise query for it.
            if (!string.IsNullOrEmpty(teamSettings.AdminUserName))
            {
                if (teamSettings.AdminUserName == teamSettings.OriginalAdminUserName)
                {
                    adminUserId = teamSettings.OriginalAdminUserId;
                }
                else
                {
                    var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamSettings.TeamId);
                    var foundAdminUser = allMembers.FirstOrDefault(account => account.Name.ToLower() == teamSettings.AdminUserName.Trim().ToLower());

                    if (foundAdminUser == null)
                    {
                        var errorMsg = string.Format(Resources.EditTeamSettingsUnrecognizedUserName, teamSettings.AdminUserName);
                        await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(errorMsg));
                        return;
                    }

                    adminUserId = foundAdminUser.GetUserId();
                }
            }

            await this.bot.SaveTeamSettings(
                connectorClient,
                activity,
                tenantId,
                teamSettings.TeamId,
                adminUserId,
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
                    string teamName = teamsChannelData.Team.Name;
                    var teamContext = new TeamContext { TeamId = teamId, TeamName = teamName };

                    if (message.MembersAdded?.Count() > 0)
                    {
                        foreach (var member in message.MembersAdded)
                        {
                            if (member.Id == myBotId)
                            {
                                await this.HandleAddedBot(connectorClient, message, tenantId, teamContext);
                            }
                            else
                            {
                                await this.bot.SaveAddedUserToTeam(member.GetUserId(), teamId);

                                var installedTeam = await this.bot.GetInstalledTeam(teamId);
                                await this.bot.WelcomeUser(connectorClient, member, tenantId, teamContext, EnrollmentStatus.NotJoined, installedTeam.InstallerName, isAdminUser: false);
                            }
                        }
                    }

                    if (message.MembersRemoved?.Count() > 0)
                    {
                        foreach (var member in message.MembersRemoved)
                        {
                            if (member.Id == myBotId)
                            {
                                await this.HandleRemovedBot(message, teamId);
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

        private async Task HandleAddedBot(ConnectorClient connectorClient, Activity message, string tenantId, TeamContext teamContext)
        {
            var teamId = teamContext.TeamId;

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
                // Welcome the admin.
                // We don't send a welcome card to the team yet as the admin needs to edit the subteam names through "Admin: Edit Team Settings" first.
                // Then the welcome team card is sent manually through the "Admin: Send Welcome Card" button.
                // Assumption: user status is NotJoined. Don't want to incur a query as if the user status is not NotJoined, it's not a big deal, they can just edit it.
                await this.bot.WelcomeUser(connectorClient, personThatAddedBot, tenantId, teamContext, userStatus: EnrollmentStatus.NotJoined, "you", isAdminUser: true);
            }
            else if (!addedSuccessfully && personThatAddedBot != null)
            {
                await this.bot.SendFailedToInstall(connectorClient, personThatAddedBot, tenantId);
            }
        }

        private async Task HandleRemovedBot(Activity message, string teamId)
        {
            this.telemetryClient.TrackTrace($"Bot removed from team {teamId}");

            var properties = new Dictionary<string, string>
            {
                { "Scope", message.Conversation?.ConversationType },
                { "TeamId", teamId },
                { "UninstallerId", message.From.Id },
            };
            this.telemetryClient.TrackEvent("AppUninstalled", properties);

            await this.bot.SaveRemoveBotFromTeam(teamId);
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