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
        /// Create a submit action that will apply the title, message and store extra data to give the action more context
        /// </summary>
        /// <param name="title">Button title and the message back when clicked</param>
        /// <param name="messageId">Message id this button will invoke</param>
        /// <param name="extraData">Either TeamContext of the action or ChooseUserResult for the action</param>
        /// <returns>Submit action for an adaptive card</returns>
        public static AdaptiveSubmitAction CreateSubmitAction(string title, string messageId, object extraData)
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

            if (extraData != null)
            {
                // In order for the data to be in the activity.Value and for it to be deserialized into a TeamContext
                // object, it needs to be on the root object.
                data.Merge(JObject.FromObject(extraData));
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
        /// Returns a list of user actions that a user can take
        /// </summary>
        /// <param name="teamContext">team context for the actions</param>
        /// <param name="enrollmentStatus">user status for the team</param>
        /// <returns>List of user actions</returns>
        public static List<AdaptiveAction> CreateUserActions(TeamContext teamContext, EnrollmentStatus enrollmentStatus)
        {
            return CreateUserActions(enrollmentStatus, teamContext.TeamName, teamContext);
        }

        /// <summary>
        /// Returns a list of user actions that are invoked when an admin wishes to perform the action on behalf of someone else
        /// </summary>
        /// <param name="userAndTeam">user and team info</param>
        /// <param name="enrollmentStatus">enrollment status for the team</param>
        /// <returns>List of user actions</returns>
        public static List<AdaptiveAction> CreateUserActionsForAdmin(ChooseUserResult userAndTeam, EnrollmentStatus enrollmentStatus)
        {
            return CreateUserActions(enrollmentStatus, userAndTeam.TeamContext.TeamName, userAndTeam);
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
        /// <param name="teamName">Team name the status is for</param>
        /// <returns>Button text and message id</returns>
        public static Tuple<string, string> GetButtonTextAndMsgIdForStatusButton(EnrollmentStatus userStatus, string teamName)
        {
            string buttonText = string.Empty;
            string messageId = string.Empty;

            switch (userStatus)
            {
                case EnrollmentStatus.NotJoined:
                    buttonText = string.Format(Resources.JoinButtonText, teamName);
                    messageId = MessageIds.OptIn;
                    break;
                case EnrollmentStatus.Active:
                    buttonText = string.Format(Resources.PausePairingsButtonText, teamName);
                    messageId = MessageIds.OptOut;
                    break;
                case EnrollmentStatus.Paused:
                    buttonText = string.Format(Resources.ResumePairingsButtonText, teamName);
                    messageId = MessageIds.OptIn;
                    break;
            }

            return new Tuple<string, string>(buttonText, messageId);
        }

        /// <summary>
        /// Create the adaptive card submit action corresponding to the current status.
        /// </summary>
        /// <param name="userStatus">user status</param>
        /// <param name="teamName">Team name the status is for</param>
        /// <param name="extraData">TeamContext or ChooseUserAndTeamResult object</param>
        /// <returns>submit action</returns>
        public static AdaptiveSubmitAction CreateStatusSubmitAction(EnrollmentStatus userStatus, string teamName, object extraData)
        {
            var textAndMsg = GetButtonTextAndMsgIdForStatusButton(userStatus, teamName);
            return CreateSubmitAction(textAndMsg.Item1, textAndMsg.Item2, extraData);
        }

        private static List<AdaptiveAction> CreateUserActions(EnrollmentStatus enrollmentStatus, string teamName, object submitActionData)
        {
            var userActions = new List<AdaptiveAction>()
            {
                CreateStatusSubmitAction(enrollmentStatus, teamName, submitActionData),
                CreateSubmitAction(Resources.EditProfileButtonText, MessageIds.EditProfile, submitActionData),
            };

            return userActions;
        }
    }
}