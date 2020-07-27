//----------------------------------------------------------------------------------------------
// <copyright file="AdminMessageHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Web.UI.WebControls;
    using Icebreaker.Helpers;
    using Icebreaker.Helpers.AdaptiveCards;
    using Icebreaker.Helpers.HeroCards;
    using Icebreaker.Model;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams.Models;
    using Newtonsoft.Json;
    using Properties;

    /// <summary>
    /// Handles admin messages
    /// </summary>
    public class AdminMessageHandler
    {
        private readonly IcebreakerBot bot;
        private readonly TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminMessageHandler"/> class.
        /// </summary>
        /// <param name="bot">The Icebreaker bot instance</param>
        /// <param name="telemetryClient">The telemetry client instance</param>
        public AdminMessageHandler(IcebreakerBot bot, TelemetryClient telemetryClient)
        {
            this.bot = bot;
            this.telemetryClient = telemetryClient;
        }

        /// <summary>
        /// Whether this handler will handle the message
        /// </summary>
        /// <param name="msgId">Message id</param>
        /// <returns>bool</returns>
        public bool CanHandleMessage(string msgId)
        {
            var acceptedMsgs = new List<string>
            {
                MessageIds.AdminMakePairs,
                MessageIds.AdminNotifyPairs,
                MessageIds.AdminChangeNotifyModeNeedApproval,
                MessageIds.AdminChangeNotifyModeNoApproval,
                MessageIds.AdminEditTeamSettings,
                MessageIds.AdminEditUser,
                MessageIds.AdminWelcomeTeam
            };
            return acceptedMsgs.Contains(msgId.ToLowerInvariant());
        }

        /// <summary>
        /// Handle the incoming message
        /// </summary>
        /// <param name="msgId">message id</param>
        /// <param name="connectorClient">connector client</param>
        /// <param name="activity">activity that had the message</param>
        /// <param name="senderAadId">sender AAD id</param>
        /// <param name="senderChannelAccountId">sender ChannelAccount id</param>
        /// <returns>Task</returns>
        public async Task HandleMessage(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId, string senderChannelAccountId)
        {
            if (msgId == MessageIds.AdminMakePairs)
            {
                await this.HandleAdminMakePairs(connectorClient, activity, senderAadId);
            }
            else if (msgId == MessageIds.AdminNotifyPairs)
            {
                if (activity.Value != null && activity.Value.ToString().TryParseJson(out MakePairsResult result))
                {
                    await this.HandleAdminNotifyPairs(connectorClient, activity, senderAadId, result.TeamId);
                }
            }
            else if (msgId == MessageIds.AdminChangeNotifyModeNeedApproval)
            {
                await this.HandleAdminNotifyNeedApproval(connectorClient, activity, senderAadId);
            }
            else if (msgId == MessageIds.AdminChangeNotifyModeNoApproval)
            {
                await this.HandleAdminNotifyNoApproval(connectorClient, activity, senderAadId);
            }
            else if (msgId == MessageIds.AdminEditTeamSettings)
            {
                await this.HandleAdminEditTeamSettings(connectorClient, activity, senderAadId);
            }
            else if (msgId == MessageIds.AdminEditUser)
            {
                await this.HandleAdminEditUser(connectorClient, activity, senderAadId);
            }
            else if (msgId == MessageIds.AdminWelcomeTeam)
            {
                await this.HandleWelcomeTeam(connectorClient, activity, senderAadId);
            }
        }

        /// <summary>
        /// Handle editing the user with a specified user
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="activity">activity</param>
        /// <param name="tenantId">user's tenant id</param>
        /// <param name="userAadId">user AAD id</param>
        /// <param name="userName">user display name</param>
        /// <returns>Task</returns>
        public async Task HandleAdminEditUserForUser(ConnectorClient connectorClient, Activity activity, string tenantId, string userAadId, string userName)
        {
            await this.bot.EditUserInfo(connectorClient, activity.CreateReply(), tenantId, userAadId, userName);
        }

        private async Task HandleAdminEditUser(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                await this.HandleAdminEditUserForTeam(connectorClient, activity, senderAadId, request.TeamId, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionEditUser,
                    adminActionMessageId: MessageIds.AdminEditUser,
                    this.HandleAdminEditUserForTeam);
            }
        }

        private async Task HandleAdminEditUserForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId, string teamName)
        {
            var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
            var users = allMembers.Select(account => new ChooseUserAdaptiveCard.User { AadId = account.GetUserId(), Name = account.Name }).OrderBy(t => t.Name);
            var pickUser = ChooseUserAdaptiveCard.GetCard(users.ToList(), MessageIds.AdminEditUser);

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(pickUser) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        private async Task HandleAdminMakePairs(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                await this.HandleAdminMakePairsForTeam(connectorClient, activity, senderAadId, request.TeamId, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionGeneratePairs,
                    adminActionMessageId: MessageIds.AdminMakePairs,
                    this.HandleAdminMakePairsForTeam);
            }
        }

        private async Task HandleAdminNotifyNoApproval(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                await this.HandleAdminNotifyNoApprovalForTeam(connectorClient, activity, senderAadId, request.TeamId, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionNotifyNoApproval,
                    adminActionMessageId: MessageIds.AdminChangeNotifyModeNoApproval,
                    this.HandleAdminNotifyNoApprovalForTeam);
            }
        }

        private async Task HandleAdminNotifyNeedApproval(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                await this.HandleAdminNotifyNeedApprovalForTeam(connectorClient, activity, senderAadId, request.TeamId, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionNotifyNeedApproval,
                    adminActionMessageId: MessageIds.AdminChangeNotifyModeNeedApproval,
                    this.HandleAdminNotifyNeedApprovalForTeam);
            }
        }

        private async Task HandleAdminNotifyNoApprovalForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId, string teamName)
        {
            var team = await this.bot.GetInstalledTeam(teamId);
            var isSuccess = await this.bot.ChangeTeamNotifyPairsMode(needApproval: false, team);

            Activity reply = activity.CreateReply();
            reply.Text = isSuccess ? Resources.NotifyModeNoApprovalSuccess : Resources.NotifyModeFail;
            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleAdminNotifyNeedApprovalForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId, string teamName)
        {
            var team = await this.bot.GetInstalledTeam(teamId);
            var isSuccess = await this.bot.ChangeTeamNotifyPairsMode(needApproval: true, team);

            Activity reply = activity.CreateReply();
            reply.Text = isSuccess ? Resources.NotifyModeNeedApprovalSuccess : Resources.NotifyModeFail;
            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleAdminActionWithNoTeamSpecified(
            ConnectorClient connectorClient,
            Activity activity,
            string senderAadId,
            string adminActionName,
            string adminActionMessageId,
            Func<ConnectorClient, Activity, string, string, string, Task> adminActionFcn)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered {adminActionName} with no team specified");

            var teamIdsAllowingAdminActionsByUser = await this.bot.GetTeamsAllowingAdminActionsByUser(senderAadId);

            if (teamIdsAllowingAdminActionsByUser.Count == 0)
            {
                var noTeamMsg = string.Format(Resources.AdminActionNoTeamMsg, adminActionName);
                var noTeamReply = activity.CreateReply(noTeamMsg);
                await connectorClient.Conversations.ReplyToActivityAsync(noTeamReply);
            }
            else if (teamIdsAllowingAdminActionsByUser.Count == 1)
            {
                var teamId = teamIdsAllowingAdminActionsByUser.First();
                var teamName = await this.bot.GetTeamNameAsync(connectorClient, teamId);
                await adminActionFcn.Invoke(connectorClient, activity, senderAadId, teamId, teamName);
            }
            else
            {
                var teamActions = new List<CardAction>();

                foreach (var teamId in teamIdsAllowingAdminActionsByUser)
                {
                    var teamName = await this.bot.GetTeamNameAsync(connectorClient, teamId);
                    var teamCardAction = new CardAction()
                    {
                        Title = teamName,
                        DisplayText = teamName,
                        Type = ActionTypes.MessageBack,
                        Text = adminActionMessageId,
                        Value = JsonConvert.SerializeObject(new TeamContext { TeamId = teamId, TeamName = teamName })
                    };
                    teamActions.Add(teamCardAction);
                }

                var pickTeamReply = activity.CreateReply();
                pickTeamReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = string.Format(Resources.AdminActionWhichTeamText, adminActionName),
                        Buttons = teamActions
                    }.ToAttachment(),
                };

                await connectorClient.Conversations.ReplyToActivityAsync(pickTeamReply);
            }
        }

        private async Task HandleAdminMakePairsForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId, string teamName)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered make pairs");

            var team = await this.bot.GetInstalledTeam(teamId);
            var matchResult = await this.bot.MakePairsForTeam(team);

            Activity reply = activity.CreateReply();

            if (matchResult.Pairs.Any())
            {
                reply.Attachments = new List<Attachment>
                {
                    this.bot.CreateMatchAttachment(matchResult, team.Id, teamName)
                };
            }
            else
            {
                reply.Text = Resources.NewPairingsNotEnoughUsers;
            }

            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleAdminNotifyPairs(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered notify pairs");

            string replyMessage = string.Empty;

            try
            {
                var makePairsResult = JsonConvert.DeserializeObject<MakePairsResult>(activity.Value.ToString());

                var members = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
                var membersByChannelAccountId = members.ToDictionary(key => key.Id, value => value);

                // Evaluate all values so we can fail early if someone no longer exists
                var pairs = makePairsResult.PairChannelAccountIds.Select(pair => new Tuple<ChannelAccount, ChannelAccount>(
                    membersByChannelAccountId[pair.Item1],
                    membersByChannelAccountId[pair.Item2])).ToList();

                var team = await this.bot.GetInstalledTeam(teamId);
                var numPairsNotified = await this.bot.NotifyAllPairs(team, pairs);
                replyMessage = string.Format(Resources.ManualNotifiedUsersMessage, numPairsNotified);
            }
            catch (Exception ex)
            {
                replyMessage = Resources.ManualNotifiedUsersErrorMessage;
                this.telemetryClient.TrackTrace($"Error while notifying pairs: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            Activity reply = activity.CreateReply(replyMessage);
            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleAdminEditTeamSettings(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                await this.HandleAdminEditTeamSettingsForTeam(connectorClient, activity, senderAadId, request.TeamId, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionEditTeamSettings,
                    adminActionMessageId: MessageIds.AdminEditTeamSettings,
                    this.HandleAdminEditTeamSettingsForTeam);
            }
        }

        private Task HandleAdminEditTeamSettingsForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId, string teamName)
        {
            return this.bot.EditTeamSettings(connectorClient, activity.CreateReply(), teamId, teamName);
        }

        private async Task HandleWelcomeTeam(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                var team = await this.bot.GetInstalledTeam(request.TeamId);
                await this.HandleWelcomeTeamForTeam(connectorClient, activity, senderAadId, team, request.TeamName);
            }
            else
            {
                await this.HandleAdminActionWithNoTeamSpecified(
                    connectorClient,
                    activity,
                    senderAadId,
                    adminActionName: Resources.AdminActionEditTeamSettings,
                    adminActionMessageId: MessageIds.AdminEditTeamSettings,
                    this.HandleAdminEditTeamSettingsForTeam);
            }
        }

        private Task HandleWelcomeTeamForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, TeamInstallInfo team, string teamName)
        {
            return this.bot.WelcomeTeam(connectorClient, activity.CreateReply(), team.TeamId, activity.Recipient.Id, team.InstallerName);
        }
    }
}