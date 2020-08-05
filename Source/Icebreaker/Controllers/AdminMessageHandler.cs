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
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json;
    using Properties;

    /// <summary>
    /// Handles admin messages
    /// </summary>
    public class AdminMessageHandler
    {
        private readonly IcebreakerBot bot;
        private readonly TelemetryClient telemetryClient;

        // Function params: messageId, Connector client, message activity, senderAAdId
        private readonly Dictionary<string, Func<string, ConnectorClient, Activity, string, Task>> messageHandlers;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminMessageHandler"/> class.
        /// </summary>
        /// <param name="bot">The Icebreaker bot instance</param>
        /// <param name="telemetryClient">The telemetry client instance</param>
        public AdminMessageHandler(IcebreakerBot bot, TelemetryClient telemetryClient)
        {
            this.bot = bot;
            this.telemetryClient = telemetryClient;

            this.messageHandlers = new Dictionary<string, Func<string, ConnectorClient, Activity, string, Task>>
            {
                { MessageIds.AdminMakePairs, this.HandleAdminMakePairs },
                { MessageIds.AdminNotifyPairs, this.HandleAdminNotifyPairs },
                { MessageIds.AdminEditTeamSettings, this.HandleAdminEditTeamSettings },
                { MessageIds.AdminEditUser, this.HandleAdminEditUser },
                { MessageIds.AdminWelcomeTeam, this.HandleWelcomeTeam }
            };
        }

        /// <summary>
        /// Whether this handler will handle the message
        /// </summary>
        /// <param name="msgId">Message id</param>
        /// <returns>bool</returns>
        public bool CanHandleMessage(string msgId)
        {
            return this.messageHandlers.Keys.Contains(msgId);
        }

        /// <summary>
        /// Handle the incoming message
        /// </summary>
        /// <param name="msgId">message id</param>
        /// <param name="connectorClient">connector client</param>
        /// <param name="activity">activity that had the message</param>
        /// <param name="senderAadId">sender AAD id</param>
        /// <returns>Task</returns>
        public Task HandleMessage(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            var handler = this.messageHandlers[msgId];
            return handler(msgId, connectorClient, activity, senderAadId);
        }

        /// <summary>
        /// Handle editing the user with a specified user
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="activity">activity</param>
        /// <param name="tenantId">user's tenant id</param>
        /// <param name="userAndTeam">user and team info</param>
        /// <returns>Task</returns>
        private async Task SendEditAnyUserCard(ConnectorClient connectorClient, Activity activity, string tenantId, UserAndTeam userAndTeam)
        {
            // Provide the user actions for the user
            var userInfo = await this.bot.GetOrCreateUnpersistedUserInfo(tenantId, userAndTeam.User.UserAadId);
            var userStatus = userInfo.GetStatusInTeam(userAndTeam.Team.TeamId);

            var editUserCard = EditAnyUserAdaptiveCard.GetCard(userStatus, userAndTeam);
            await ActivityHelper.ReplyWithAdaptiveCard(connectorClient, activity, editUserCard);
        }

        private async Task HandleAdminEditUser(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            // Choose user prompt based on the team, then show the edit any user card once we have the user
            var teamContext = ActivityHelper.ParseCardActionData<TeamContext>(activity);
            if (teamContext != null)
            {
                var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamContext.TeamId);
                var users = allMembers.Select(account => new ChooseUserAdaptiveCard.User { AadId = account.GetUserId(), Name = account.Name }).OrderBy(t => t.Name);
                var pickUserCard = ChooseUserAdaptiveCard.GetCard(users.ToList(), teamContext, msgId);
                await ActivityHelper.ReplyWithAdaptiveCard(connectorClient, activity, pickUserCard);
                return;
            }

            var chooseUserResult = ActivityHelper.ParseCardActionData<ChooseUserResult>(activity);
            if (chooseUserResult != null)
            {
                var userAndTeam = new UserAndTeam { Team = chooseUserResult.TeamContext, User = new UserContext { UserAadId = chooseUserResult.GetUserId(), UserName = chooseUserResult.GetUserName() } };
                await this.SendEditAnyUserCard(connectorClient, activity, ActivityHelper.GetTenantId(activity), userAndTeam);
                return;
            }
        }

        private async Task HandleAdminMakePairs(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            var teamContext = ActivityHelper.ParseCardActionData<TeamContext>(activity);

            this.telemetryClient.TrackTrace($"User {senderAadId} triggered make pairs");

            var team = await this.bot.GetInstalledTeam(teamContext.TeamId);
            var matchResult = await this.bot.MakePairsForTeam(team);

            Activity reply = activity.CreateReply();

            if (matchResult.Pairs.Any())
            {
                reply.Attachments = new List<Attachment>
                {
                    this.bot.CreateMatchAttachment(matchResult, team.Id, teamContext.TeamName)
                };
            }
            else
            {
                var numUsers = matchResult.OddPerson != null ? 1 : 0;
                reply.Text = string.Format(Resources.NewPairingsNotEnoughUsers, numUsers);
            }

            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleAdminNotifyPairs(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            var makePairsResult = ActivityHelper.ParseCardActionData<MakePairsResult>(activity);

            this.telemetryClient.TrackTrace($"User {senderAadId} triggered notify pairs");

            string replyMessage = string.Empty;

            try
            {
                var members = await connectorClient.Conversations.GetConversationMembersAsync(makePairsResult.TeamId);
                var membersByChannelAccountId = members.ToDictionary(key => key.Id, value => value);

                // Evaluate all values so we can fail early if someone no longer exists
                var pairs = makePairsResult.PairChannelAccountIds.Select(pair => new Tuple<ChannelAccount, ChannelAccount>(
                    membersByChannelAccountId[pair.Item1],
                    membersByChannelAccountId[pair.Item2])).ToList();

                var team = await this.bot.GetInstalledTeam(makePairsResult.TeamId);
                var numUsersNotified = await this.bot.NotifyAllPairs(team, pairs);
                replyMessage = string.Format(Resources.ManualNotifiedUsersMessage, numUsersNotified, pairs.Count * 2);
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

        private async Task HandleAdminEditTeamSettings(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            var teamContext = ActivityHelper.ParseCardActionData<TeamContext>(activity);
            await this.bot.EditTeamSettings(connectorClient, activity.CreateReply(), teamContext.TeamId, teamContext.TeamName);
        }

        private async Task HandleWelcomeTeam(string msgId, ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            var teamContext = ActivityHelper.ParseCardActionData<TeamContext>(activity);
            var team = await this.bot.GetInstalledTeam(teamContext.TeamId);
            await this.bot.WelcomeTeam(connectorClient, activity.CreateReply(), team.TeamId, activity.Recipient.Id, team.InstallerName);
        }
    }
}