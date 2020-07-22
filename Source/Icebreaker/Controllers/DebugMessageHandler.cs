//----------------------------------------------------------------------------------------------
// <copyright file="DebugMessageHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Icebreaker.Helpers.AdaptiveCards;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;

    /// <summary>
    /// Handles admin messages
    /// </summary>
    public class DebugMessageHandler
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DebugMessageHandler"/> class.
        /// </summary>
        public DebugMessageHandler()
        {
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
                MessageIds.DebugNotifyUser,
                MessageIds.DebugWelcomeUser,
                MessageIds.DebugWelcomeUserAdmin
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
            if (msgId == MessageIds.DebugNotifyUser)
            {
                await this.HandleDebugNotifyUser(connectorClient, activity, activity.From.AsTeamsChannelAccount());
            }
            else if (msgId == MessageIds.DebugWelcomeUser)
            {
                await this.HandleDebugWelcomeUser(connectorClient, activity, activity.From.AsTeamsChannelAccount());
            }
            else if (msgId == MessageIds.DebugWelcomeUserAdmin)
            {
                await this.HandleDebugWelcomeUserAdmin(connectorClient, activity, activity.From.AsTeamsChannelAccount());
            }
        }

        private async Task HandleDebugNotifyUser(ConnectorClient connectorClient, Activity activity, TeamsChannelAccount sender)
        {
            var notifyCard = PairUpNotificationAdaptiveCard.GetCardJson("TestTeam", sender, sender, "LunchBuddy");

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(notifyCard) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        private async Task HandleDebugWelcomeUser(ConnectorClient connectorClient, Activity activity, TeamsChannelAccount sender)
        {
            var welcomeCard = WelcomeNewMemberAdaptiveCard.GetCardJson("TestTeam", "LunchBuddy", "InstallerPerson", false, null);

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(welcomeCard) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        private async Task HandleDebugWelcomeUserAdmin(ConnectorClient connectorClient, Activity activity, TeamsChannelAccount sender)
        {
            var welcomeCard = WelcomeNewMemberAdaptiveCard.GetCardJson("TestTeam", "LunchBuddy", "you", true, null);

            var replyActivity = activity.CreateReply();
            replyActivity.Attachments = new List<Attachment> { AdaptiveCardHelper.CreateAdaptiveCardAttachment(welcomeCard) };

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }
    }
}