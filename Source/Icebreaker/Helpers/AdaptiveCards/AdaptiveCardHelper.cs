//----------------------------------------------------------------------------------------------
// <copyright file="AdaptiveCardHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Properties;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Helper class for Adaptive Cards
    /// </summary>
    public static class AdaptiveCardHelper
    {
        /// <summary>
        /// What team names from the database are separated with when displayed to the user
        /// </summary>
        public const string TeamsSeparatorWithSpace = ", ";

        /// <summary>
        /// Replace the template keys with the provided values
        /// </summary>
        /// <param name="cardTemplate">Card template JSON</param>
        /// <param name="templateData">Data to replace the keys</param>
        /// <returns>JSON with replaced values</returns>
        public static string ReplaceTemplateKeys(string cardTemplate, IDictionary<string, string> templateData)
        {
            var cardBody = cardTemplate;

            foreach (var kvp in templateData)
            {
                cardBody = cardBody.Replace($"%{kvp.Key}%", kvp.Value);
            }

            return cardBody;
        }

        /// <summary>
        /// Create attachment for a bot reply for an adaptive card
        /// </summary>
        /// <param name="cardJSON">Adaptive card json</param>
        /// <returns>Attachment</returns>
        public static Attachment CreateAdaptiveCardAttachment(string cardJSON)
        {
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(cardJSON),
            };
        }

        /// <summary>
        /// Create a submit action that will apply the title, message and a team context for admin actions if available
        /// </summary>
        /// <param name="title">Button title and the message back when clicked</param>
        /// <param name="messageId">Message id this button will invoke</param>
        /// <param name="teamContext">Team context of the admin action</param>
        /// <returns>Submit action for an adaptive card</returns>
        public static AdaptiveSubmitAction CreateSubmitAction(string title, string messageId, TeamContext teamContext = null)
        {
            var data = JObject.FromObject(new
            {
                msteams = new
                {
                    type = "messageBack",
                    displayText = title,
                    text = messageId
                }
            });

            if (teamContext != null)
            {
                // In order for the data to be left in the activity.Value to be deserialized into a TeamContext
                // object it needs to be on the root object.
                data.Merge(JObject.FromObject(teamContext));
            }

            return new AdaptiveSubmitAction
            {
                Title = title,
                Data = data
            };
        }

        /// <summary>
        /// Create a list of admin actions
        /// </summary>
        /// <param name="adminTeamContext">Can be null. Team context for the admin actions if the user is admin to one team</param>
        /// <returns>List of admin actions</returns>
        public static List<AdaptiveAction> CreateAdminActions(TeamContext adminTeamContext)
        {
            var adminActions = new List<AdaptiveAction>()
            {
                CreateSubmitAction(Resources.EditTeamSettingsButtonText, MessageIds.AdminEditTeamSettings, adminTeamContext),
                CreateSubmitAction(Resources.MakePairsButtonText, MessageIds.AdminMakePairs, adminTeamContext)
            };
            return adminActions;
        }
    }
}