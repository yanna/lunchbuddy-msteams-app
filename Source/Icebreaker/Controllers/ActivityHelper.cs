//----------------------------------------------------------------------------------------------
// <copyright file="ActivityHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AdaptiveCards;
    using Icebreaker.Helpers;
    using Icebreaker.Helpers.AdaptiveCards;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams.Models;

    /// <summary>
    /// Helper functions related to the bot Activity
    /// </summary>
    public static class ActivityHelper
    {
        /// <summary>
        /// Get the data from a card action based on the type
        /// </summary>
        /// <typeparam name="T">Type to extract</typeparam>
        /// <param name="activity">Activity</param>
        /// <returns>Parsed response or null</returns>
        public static T ParseCardActionData<T>(Activity activity)
            where T : class
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out T cardActionData))
            {
                return cardActionData;
            }

            return null;
        }

        /// <summary>
        /// Send the adaptive card in reply to the original activity
        /// </summary>
        /// <param name="connectorClient">Connector client</param>
        /// <param name="originalActivity">Activity we will reply to</param>
        /// <param name="adaptiveCard">Adaptive card to show</param>
        /// <returns>Task</returns>
        public static Task ReplyWithAdaptiveCard(ConnectorClient connectorClient, Activity originalActivity, AdaptiveCard adaptiveCard)
        {
            var replyActivity = originalActivity.CreateReply();
            replyActivity.Attachments = new List<Attachment>
            {
                AdaptiveCardHelper.CreateAdaptiveCardAttachment(adaptiveCard)
            };

            return connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Send the hero card in reply to the original activity
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="originalActivity">Activity to reply to</param>
        /// <param name="heroCard">Hero card to show</param>
        /// <returns>Task</returns>
        public static Task ReplyWithHeroCard(ConnectorClient connectorClient, Activity originalActivity, HeroCard heroCard)
        {
            var reply = originalActivity.CreateReply();
            reply.Attachments = new List<Attachment>
            {
                heroCard.ToAttachment(),
            };
            return connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        /// <summary>
        /// Get the tenant id
        /// </summary>
        /// <param name="activity">Activity to extract info</param>
        /// <returns>Tenant id</returns>
        public static string GetTenantId(Activity activity)
        {
            var teamChannelData = activity.GetChannelData<TeamsChannelData>();
            return teamChannelData.Tenant.Id;
        }

        /// <summary>
        /// Get the user and team data embedded in the message, or get the embedded team and default to the sender AAD id and sender name.
        /// Returns null if no team context is found.
        /// </summary>
        /// <param name="activity">Activity to extract data from</param>
        /// <param name="senderAadId">AAD id of the person who sent the message</param>
        /// <param name="senderName">Name of the person who sent the message</param>
        /// <returns>User and team data</returns>
        public static UserAndTeam GetUserAndTeam(Activity activity, string senderAadId, string senderName)
        {
            var userAndTeam = ParseCardActionData<UserAndTeam>(activity);
            if (userAndTeam != null)
            {
                return userAndTeam;
            }

            var team = ParseCardActionData<TeamContext>(activity);
            if (team != null)
            {
                return new UserAndTeam { Team = team, User = new UserContext { UserAadId = senderAadId, UserName = senderName } };
            }

            return null;
        }
    }
}