//----------------------------------------------------------------------------------------------
// <copyright file="AdaptiveCardHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Model;
    using Icebreaker.Properties;
    using Microsoft.Azure;
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
        /// Create attachment for a bot reply for an adaptive card
        /// </summary>
        /// <param name="card">Adaptive card</param>
        /// <returns>Attachment</returns>
        public static Attachment CreateAdaptiveCardAttachment(AdaptiveCard card)
        {
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = card,
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
                CreateSubmitAction(Resources.WelcomeTeamButtonText, MessageIds.AdminWelcomeTeam, adminTeamContext),
                CreateSubmitAction(Resources.EditUserButtonText, MessageIds.AdminEditUser, adminTeamContext),
                CreateSubmitAction(Resources.MakePairsButtonText, MessageIds.AdminMakePairs, adminTeamContext)
            };
            return adminActions;
        }

        /// <summary>
        /// Create tour url
        /// </summary>
        /// <returns>Url to the bot tour page</returns>
        public static string CreateTourUrl()
        {
            var baseDomain = CloudConfigurationManager.GetSetting("AppBaseDomain");
            var htmlUrl = Uri.EscapeDataString($"https://{baseDomain}/Content/tour.html?theme={{theme}}");
            var tourTitle = Resources.WelcomeTourTitle;
            var appId = CloudConfigurationManager.GetSetting("ManifestAppId");
            var tourUrl = $"https://teams.microsoft.com/l/task/{appId}?url={htmlUrl}&height=533px&width=600px&title={tourTitle}";
            return tourUrl;
        }

        /// <summary>
        /// Create a card that just lists the key value pairs
        /// </summary>
        /// <param name="title">title of the card</param>
        /// <param name="pairs">key value pairs to show</param>
        /// <returns>the card</returns>
        public static AdaptiveCard CreateSubmitResultCard(
            string title,
            List<Tuple<string, string>> pairs)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock()
                    {
                        Text = title,
                        Size = AdaptiveTextSize.Large,
                        Wrap = true,
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveFactSet()
                    {
                        Facts = pairs.Select(pair => new AdaptiveFact(pair.Item1, pair.Item2)).ToList()
                    }
                }
            };
            return card;
        }

        /// <summary>
        /// Determine what the status button should say and the corresponding message id based on the current user status
        /// </summary>
        /// <param name="userStatus">User status</param>
        /// <returns>Button text and message id</returns>
        public static Tuple<string, string> GetButtonTextAndMsgIdForStatusButton(EnrollmentStatus userStatus)
        {
            string buttonText = string.Empty;
            string messageId = string.Empty;

            switch (userStatus)
            {
                case EnrollmentStatus.NotJoined:
                    buttonText = Resources.JoinButtonText;
                    messageId = MessageIds.OptIn;
                    break;
                case EnrollmentStatus.Active:
                    buttonText = Resources.PausePairingsButtonText;
                    messageId = MessageIds.OptOut;
                    break;
                case EnrollmentStatus.Inactive:
                    buttonText = Resources.ResumePairingsButtonText;
                    messageId = MessageIds.OptIn;
                    break;
            }

            return new Tuple<string, string>(buttonText, messageId);
        }

        /// <summary>
        /// Create the adaptive card submit action corresponding to the current status.
        /// </summary>
        /// <param name="userStatus">user status</param>
        /// <returns>submit action</returns>
        public static AdaptiveSubmitAction CreateStatusSubmitAction(EnrollmentStatus userStatus)
        {
            var textAndMsg = GetButtonTextAndMsgIdForStatusButton(userStatus);
            return CreateSubmitAction(textAndMsg.Item1, textAndMsg.Item2);
        }
    }
}