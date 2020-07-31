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

                var senderChannelAccountId = activity.From.Id;
                var teamChannelData = activity.GetChannelData<TeamsChannelData>();
                var tenantId = teamChannelData.Tenant.Id;
                var isMessageInChannel = teamChannelData.Team != null;

                // Submit action from an adaptive card results in no text and hopefully some value.
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
                    else if (activity.Value.ToString().TryParseJson(out ChooseUserResult userAndTeam))
                    {
                        if (userAndTeam.MessageId == MessageIds.AdminEditUser)
                        {
                            await this.adminMessageHandler.HandleAdminEditUserForUser(
                                connectorClient, activity, tenantId, userAndTeam);
                        }
                    }

                    return;
                }

                var msg = activity.Text.ToLowerInvariant();

                if (msg == MessageIds.OptOut)
                {
                    await this.HandleOptOut(connectorClient, activity, senderAadId, senderName, tenantId);
                }
                else if (msg == MessageIds.OptIn)
                {
                    await this.HandleOptIn(connectorClient, activity, senderAadId, senderName, tenantId);
                }
                else if (msg == MessageIds.EditProfile && !isMessageInChannel)
                {
                    await this.HandleEditProfile(connectorClient, activity, tenantId, senderAadId);
                }
                else if (this.adminMessageHandler.CanHandleMessage(msg))
                {
                    await this.adminMessageHandler.HandleMessage(msg, connectorClient, activity, senderAadId, senderChannelAccountId);
                }
                else if (this.debugMessageHandler.CanHandleMessage(msg))
                {
                    var teams = await this.bot.GetAllTeams(connectorClient);
                    await this.debugMessageHandler.HandleMessage(msg, connectorClient, activity, teams.FirstOrDefault()?.TeamId);
                }
                else
                {
                    if (!isMessageInChannel)
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
            var teamIdFromWelcomeTeam = WelcomeTeamAdaptiveCard.GetTeamIdFromBotMessage(activity.Text);
            if (!string.IsNullOrEmpty(teamIdFromWelcomeTeam))
            {
                string teamName = await this.bot.GetTeamNameAsync(connectorClient, teamIdFromWelcomeTeam);
                teamForActions = new TeamContext { TeamId = teamIdFromWelcomeTeam, TeamName = teamName };
            }
            else
            {
                teamForActions = this.GetTeamContext(activity);
            }

            if (teamForActions == null)
            {
                var allTeams = await this.bot.GetAllTeams(connectorClient);

                if (allTeams.Count > 1)
                {
                    await SendChooseTeamForActionCard(connectorClient, activity, Resources.UnrecognizedInputChooseTeam, allTeams, actionMessage: activity.Text);
                    return;
                }

                if (allTeams.Count == 0)
                {
                    await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(Resources.UnrecognizedInputNoTeam));
                    return;
                }

                teamForActions = allTeams.First();
            }

            var senderAadId = sender.GetUserId();

            var userInfo = await this.bot.GetOrCreateUnpersistedUserInfo(tenantId, senderAadId);
            var userStatus = userInfo.GetStatusInTeam(teamForActions.TeamId);
            var isUserAdminOfTeam = userInfo.AdminForTeams.Contains(teamForActions.TeamId);

            if (userStatus == EnrollmentStatus.NotJoined)
            {
                await this.bot.WelcomeUser(connectorClient, sender, tenantId, teamForActions, userStatus, botInstallerName: string.Empty, isUserAdminOfTeam);
            }
            else
            {
                await this.bot.SendUnrecognizedInputMessage(connectorClient, activity.CreateReply(), teamForActions, userStatus, isUserAdminOfTeam);
            }
        }

        private async Task HandleOptIn(ConnectorClient connectorClient, Activity activity, string senderAadId, string senderName, string tenantId)
        {
            TeamContext optInTeam = null;
            string optInUserId = senderAadId;
            string optInUserName = senderName;

            ChooseUserResult userAndTeamResult = this.GetUserAndTeamResult(activity);

            if (userAndTeamResult != null)
            {
                optInTeam = userAndTeamResult.TeamContext;
                optInUserId = userAndTeamResult.GetUserId();
                optInUserName = userAndTeamResult.GetUserName();
            }

            optInTeam = optInTeam ?? this.GetTeamContext(activity);

            // Need to get list of possible teams the user can opt into
            if (optInTeam == null)
            {
                // This should not happen, the card that showed the command should have a team context.
                // Either this is from unrecognized input where we always ask for a team first or from a welcome card which has a team.
                await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(Resources.OptInOrOutNoTeam));
                return;
            }

            var submitData = userAndTeamResult ?? (object)optInTeam;
            await this.HandleOptIn(connectorClient, activity, optInUserId, optInUserName, tenantId, optInTeam, submitData);
        }

        private async Task HandleOptIn(ConnectorClient connectorClient, Activity activity, string userId, string userName, string tenantId, TeamContext teamContext, object submitData)
        {
            // User opted in
            this.telemetryClient.TrackTrace($"User {userId} opted in");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", userId },
                { "OptInStatus", "true" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptInUser(tenantId, userId, teamContext.TeamId);

            var optInReply = activity.CreateReply();
            if (isSuccessful)
            {
                var actionText = string.Format(Resources.PausePairingsButtonText, teamContext.TeamName);

                optInReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = string.Format(Resources.OptInConfirmation, userName, teamContext.TeamName),
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = actionText,
                                DisplayText = actionText,
                                Type = ActionTypes.MessageBack,
                                Text = MessageIds.OptOut,
                                Value = JsonConvert.SerializeObject(submitData)
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

        private TeamContext GetTeamContext(Activity activity)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext team))
            {
                return team;
            }

            return null;
        }

        private ChooseUserResult GetUserAndTeamResult(Activity activity)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out ChooseUserResult userAndTeam))
            {
                return userAndTeam;
            }

            return null;
        }

        private async Task HandleOptOut(ConnectorClient connectorClient, Activity activity, string senderAadId, string senderName, string tenantId)
        {
            TeamContext optOutTeam = null;
            string optOutUserId = senderAadId;
            string optOutUserName = senderName;

            ChooseUserResult userAndTeamResult = this.GetUserAndTeamResult(activity);

            if (userAndTeamResult != null)
            {
                optOutTeam = userAndTeamResult.TeamContext;
                optOutUserId = userAndTeamResult.GetUserId();
                optOutUserName = userAndTeamResult.GetUserName();
            }

            optOutTeam = optOutTeam ?? this.GetTeamContext(activity);

            // Need to get list of possible teams the user can opt out from
            if (optOutTeam == null)
            {
                // This should not happen, the card that showed the command should have a team context.
                // Either this is from unrecognized input where we always ask for a team first or from a welcome card which has a team.
                await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(Resources.OptInOrOutNoTeam));
                return;
            }

            var submitData = userAndTeamResult ?? (object) optOutTeam;
            await this.HandleOptOut(connectorClient, activity, optOutUserId, optOutUserName, isAnotherUser: optOutUserId != senderAadId, tenantId, optOutTeam, submitData);
        }

        private async Task HandleOptOut(ConnectorClient connectorClient, Activity activity, string userId, string userName, bool isAnotherUser, string tenantId, TeamContext optOutTeam, object submitData)
        {
            // User opted out
            this.telemetryClient.TrackTrace($"User {userId} opted out");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", userId },
                { "OptInStatus", "false" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            var isSuccessful = await this.bot.OptOutUser(tenantId, userId, optOutTeam.TeamId);

            var optOutReply = activity.CreateReply();

            if (isSuccessful)
            {
                var text = isAnotherUser ? string.Format(Resources.OptOutConfirmationAnotherUser, userName, optOutTeam.TeamName) :
                    string.Format(Resources.OptOutConfirmation, optOutTeam.TeamName);

                var actionText = string.Format(Resources.ResumePairingsButtonText, optOutTeam.TeamName);

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
                                Value = JsonConvert.SerializeObject(submitData)
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

        private static Task SendChooseTeamForActionCard(ConnectorClient connectorClient, Activity activity, string cardMsg, List<TeamContext> possibleTeamsForAction, string actionMessage)
        {
            var chooseTeamCard = ChooseTeamHeroCard.GetCard(cardMsg, possibleTeamsForAction, actionMessage);
            var chooseTeamReply = activity.CreateReply();
            chooseTeamReply.Attachments = new List<Attachment>
            {
                chooseTeamCard.ToAttachment(),
            };
            return connectorClient.Conversations.ReplyToActivityAsync(chooseTeamReply);
        }

        private async Task HandleEditProfile(ConnectorClient connectorClient, Activity activity, string tenantId, string senderAadId)
        {
            var userId = senderAadId;
            TeamContext teamContext = null;

            var userAndTeam = this.GetUserAndTeamResult(activity);
            if (userAndTeam != null)
            {
                userId = userAndTeam.GetUserId();
                teamContext = userAndTeam.TeamContext;
            }

            teamContext = teamContext ?? this.GetTeamContext(activity);

            if (teamContext == null)
            {
                // This should not happen, the card that showed the Edit Profile command should have a team context.
                // Either this is from unrecognized input where we always ask for a team first or from a welcome card which has a team.
                await connectorClient.Conversations.ReplyToActivityAsync(activity.CreateReply(Resources.EditProfileNoTeam));
                return;
            }

            var replyActivity = activity.CreateReply();
            await this.bot.EditUserProfile(connectorClient, replyActivity, tenantId, userId, teamContext);
        }

        private async Task HandleSaveUserProfile(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string senderAadId,
            EditUserProfileAdaptiveCard.UserProfile userProfile)
        {
            var subteams = EditUserProfileAdaptiveCard.GetSubteams(userProfile.Subteams);
            var lowPreferenceNames = EditUserProfileAdaptiveCard.GetSubteams(userProfile.LowPreferenceNames);

            await this.bot.SaveUserProfile(
                connectorClient,
                activity,
                tenantId,
                senderAadId,
                userProfile.Discipline,
                userProfile.Gender,
                userProfile.Seniority,
                subteams,
                lowPreferenceNames);
        }

        private Task HandleSaveTeamSettings(ConnectorClient connectorClient, Activity activity, EditTeamSettingsAdaptiveCard.TeamSettings teamSettings, string tenantId)
        {
            return this.bot.SaveTeamSettings(
                connectorClient,
                activity,
                tenantId,
                teamSettings.TeamId,
                teamSettings.GetUserId(),
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