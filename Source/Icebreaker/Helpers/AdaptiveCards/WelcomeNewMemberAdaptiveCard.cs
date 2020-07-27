//----------------------------------------------------------------------------------------------
// <copyright file="WelcomeNewMemberAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Model;
    using Icebreaker.Properties;
    using Microsoft.Azure;

    /// <summary>
    /// Builder class for the welcome new member card
    /// </summary>
    public static class WelcomeNewMemberAdaptiveCard
    {
        private static readonly string CardTemplate;

        static WelcomeNewMemberAdaptiveCard()
        {
            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/WelcomeNewMemberAdaptiveCard.json");
            CardTemplate = File.ReadAllText(cardJsonFilePath);
        }

        /// <summary>
        /// Creates the welcome new member card.
        /// </summary>
        /// <param name="userStatus">User status</param>
        /// <param name="teamName">The team name. Can be empty</param>
        /// <param name="botDisplayName">The bot name</param>
        /// <param name="botInstaller">The name of the person that installed the bot to the team. Can be empty.</param>
        /// <param name="adminTeamContext">Team context for the admin actions. If this is not null the admin version of the card is shown</param>
        /// <returns>The welcome new member card</returns>
        public static string GetCardJson(EnrollmentStatus userStatus, string teamName, string botDisplayName, string botInstaller, TeamContext adminTeamContext)
        {
            string introMessagePart1 = string.Empty;
            if (!string.IsNullOrEmpty(teamName))
            {
                if (string.IsNullOrEmpty(botInstaller))
                {
                    introMessagePart1 = string.Format(Resources.InstallMessageUnknownInstaller, teamName);
                }
                else
                {
                    introMessagePart1 = string.Format(Resources.InstallMessageKnownInstaller, botInstaller, teamName);
                }
            }

            var showAdminActions = adminTeamContext != null;

            var introMessagePart2 = Resources.InstallMessageBotDescription;
            var introMessagePart3 = showAdminActions ? Resources.InstallMessageInstructionAdmin : Resources.InstallMessageInstruction;
            var suggestedNextStep = showAdminActions ?
                string.Format(Resources.InstallMessageSuggestedNextStepAdmin, Resources.MakePairsButtonText) : Resources.InstallMessageSuggestedNextStep;

            var baseDomain = CloudConfigurationManager.GetSetting("AppBaseDomain");
            var welcomeCardImageUrl = $"https://{baseDomain}/Content/welcome-card-image.png";

            var statusAction = AdaptiveCardHelper.GetButtonTextAndMsgIdForStatusButton(userStatus);
            var statusActionText = statusAction.Item1;
            var statusActionMsgId = statusAction.Item2;

            var salutationText = Resources.SalutationTitleText;
            var editProfileText = Resources.EditProfileButtonText;

            var variablesToValues = new Dictionary<string, string>()
            {
                { "botDisplayName", botDisplayName },
                { "introMessagePart1", introMessagePart1 },
                { "introMessagePart2", introMessagePart2 },
                { "introMessagePart3", introMessagePart3 },
                { "suggestedNextStep", suggestedNextStep },
                { "welcomeCardImageUrl", welcomeCardImageUrl },
                { "editProfileText", editProfileText },
                { "statusActionText", statusActionText },
                { "statusActionMessageId", statusActionMsgId },
                { "salutationText", salutationText }
            };

            var cardBody = AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);

            if (showAdminActions)
            {
                var card = AdaptiveCard.FromJson(cardBody).Card;
                var adminActions = AdaptiveCardHelper.CreateAdminActions(adminTeamContext);
                card.Actions.InsertRange(0, adminActions);
                cardBody = card.ToJson();
            }

            return cardBody;
        }
    }
}