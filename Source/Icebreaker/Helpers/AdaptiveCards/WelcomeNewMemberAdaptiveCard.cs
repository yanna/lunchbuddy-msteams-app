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
        /// <param name="teamName">The team name</param>
        /// <param name="botDisplayName">The bot name</param>
        /// <param name="botInstaller">The name of the person that installed the bot to the team</param>
        /// <param name="showAdminActions">Show admin actions</param>
        /// <param name="adminTeamContext">Team context for the admin actions</param>
        /// <returns>The welcome new member card</returns>
        public static string GetCardJson(string teamName, string botDisplayName, string botInstaller, bool showAdminActions, TeamContext adminTeamContext)
        {
            string introMessagePart1 = string.Empty;
            if (string.IsNullOrEmpty(botInstaller))
            {
                introMessagePart1 = string.Format(Resources.InstallMessageUnknownInstaller, teamName);
            }
            else
            {
                introMessagePart1 = string.Format(Resources.InstallMessageKnownInstaller, botInstaller, teamName);
            }

            var introMessagePart2 = Resources.InstallMessageBotDescription;
            var introMessagePart3 = showAdminActions ? Resources.InstallMessageInstructionAdmin : Resources.InstallMessageInstruction;
            var suggestedNextStep = showAdminActions ?
                string.Format(Resources.InstallMessageSuggestedNextStepAdmin, Resources.MakePairsButtonText) : Resources.InstallMessageSuggestedNextStep;

            var baseDomain = CloudConfigurationManager.GetSetting("AppBaseDomain");
            var welcomeCardImageUrl = $"https://{baseDomain}/Content/welcome-card-image.png";

            var pauseMatchesText = Resources.PausePairingsButtonText;
            var tourUrl = AdaptiveCardHelper.CreateTourUrl();
            var salutationText = Resources.SalutationTitleText;
            var tourButtonText = Resources.TakeATourButtonText;
            var editProfileText = Resources.EditProfileButtonText;

            var variablesToValues = new Dictionary<string, string>()
            {
                { "team", teamName },
                { "botDisplayName", botDisplayName },
                { "introMessagePart1", introMessagePart1 },
                { "introMessagePart2", introMessagePart2 },
                { "introMessagePart3", introMessagePart3 },
                { "suggestedNextStep", suggestedNextStep },
                { "welcomeCardImageUrl", welcomeCardImageUrl },
                { "editProfileText", editProfileText },
                { "pauseMatchesText", pauseMatchesText },
                { "tourUrl", tourUrl },
                { "salutationText", salutationText },
                { "tourButtonText", tourButtonText }
            };

            var cardBody = AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);

            if (showAdminActions)
            {
                var card = AdaptiveCard.FromJson(cardBody).Card;
                var adminActions = AdaptiveCardHelper.CreateAdminActions(adminTeamContext);
                card.Actions.AddRange(adminActions);
                cardBody = card.ToJson();
            }

            return cardBody;
        }
    }
}