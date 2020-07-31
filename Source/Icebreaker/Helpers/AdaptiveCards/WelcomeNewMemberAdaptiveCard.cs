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
        /// Creates the welcome new member card that welcomes the user to a specific team.
        /// </summary>
        /// <param name="teamContext">Team context for the card.</param>
        /// <param name="userStatus">User status</param>
        /// <param name="botInstallerName">The name of the person that installed the bot to the team. Can be empty.</param>
        /// <param name="showAdminActions">Whether to show the admin actions</param>
        /// <returns>The welcome new member card</returns>
        public static AdaptiveCard GetCard(TeamContext teamContext, EnrollmentStatus userStatus, string botInstallerName, bool showAdminActions)
        {
            string introMessagePart1;
            if (string.IsNullOrEmpty(botInstallerName))
            {
                introMessagePart1 = string.Format(Resources.InstallMessageUnknownInstaller, teamContext.TeamName);
            }
            else
            {
                introMessagePart1 = string.Format(Resources.InstallMessageKnownInstaller, botInstallerName, teamContext.TeamName);
            }

            var introMessagePart2 = Resources.InstallMessageBotDescription;
            var introMessagePart3 = showAdminActions ? Resources.InstallMessageInstructionAdmin : Resources.InstallMessageInstruction;
            var suggestedNextStep = showAdminActions ?
                string.Format(Resources.InstallMessageSuggestedNextStepAdmin, Resources.MakePairsButtonText) : Resources.InstallMessageSuggestedNextStep;

            var baseDomain = CloudConfigurationManager.GetSetting("AppBaseDomain");
            var welcomeCardImageUrl = $"https://{baseDomain}/Content/welcome-card-image.png";

            var salutationText = Resources.SalutationTitleText;

            var variablesToValues = new Dictionary<string, string>()
            {
                { "salutationText", salutationText },
                { "welcomeCardImageUrl", welcomeCardImageUrl },
                { "introMessagePart1", introMessagePart1 },
                { "introMessagePart2", introMessagePart2 },
                { "introMessagePart3", introMessagePart3 },
                { "suggestedNextStep", suggestedNextStep },
            };

            var cardBody = AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);
            var card = AdaptiveCard.FromJson(cardBody).Card;

            if (showAdminActions)
            {
                var adminActions = AdaptiveCardHelper.CreateAdminActions(teamContext);
                card.Actions.AddRange(adminActions);
            }

            var userActions = AdaptiveCardHelper.CreateUserActions(teamContext, userStatus);
            card.Actions.AddRange(userActions);

            return card;
        }
    }
}