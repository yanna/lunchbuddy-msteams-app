//----------------------------------------------------------------------------------------------
// <copyright file="UnrecognizedInputInChannelAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Properties;

    /// <summary>
    /// Builder class for the unrecognized input message
    /// </summary>
    public class UnrecognizedInputInChannelAdaptiveCard
    {
        /// <summary>
        /// Generates the adaptive card string for the unrecognized input in a channel.
        /// It invites the user to chat directly with the bot.
        /// </summary>
        /// <param name="botChannelAccountId">bot ChannelAccount id for deep link to work</param>
        /// <param name="teamId">Team id of the channel this message is for</param>
        /// <returns>The adaptive card for the unrecognized input</returns>
        public static AdaptiveCard GetCard(string botChannelAccountId, string teamId)
        {
            var botMessage = AdaptiveCardHelper.GetChatWithMeMessage(teamId);

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = "👋 " + Resources.UnrecognizedInputInChannelText,
                        Wrap = true
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveOpenUrlAction
                    {
                        Title = Resources.ChatWithMeButtonText,
                        Url = new System.Uri("https://teams.microsoft.com/l/chat/0/0?users=" + botChannelAccountId + "&message=" + botMessage)
                    }
                }
            };

            return card;
        }
    }
}