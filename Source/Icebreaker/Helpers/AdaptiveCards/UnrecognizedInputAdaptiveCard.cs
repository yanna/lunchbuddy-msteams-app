//----------------------------------------------------------------------------------------------
// <copyright file="UnrecognizedInputAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Properties;

    /// <summary>
    /// Builder class for the unrecognized input message
    /// </summary>
    public class UnrecognizedInputAdaptiveCard
    {
        static UnrecognizedInputAdaptiveCard()
        {
        }

        /// <summary>
        /// Generates the adaptive card string for the unrecognized input.
        /// </summary>
        /// <param name="isOptedIn">Whether the user is opted in to being matched</param>
        /// <param name="showAdminActions">Whether to show the admin actions</param>
        /// <param name="adminTeamContext">Can be null. The admin team context for the admin actions</param>
        /// <returns>The adaptive card for the unrecognized input</returns>
        public static string GetCardJson(bool isOptedIn, bool showAdminActions, TeamContext adminTeamContext)
        {
            var messageContent = Resources.UnrecognizedInput;
            var pauseOrResumeMatchesButtonText = isOptedIn ? Resources.PausePairingsButtonText : Resources.ResumePairingsButtonText;
            var pauseOrResumeMatchesMessage = isOptedIn ? MessageIds.OptOut : MessageIds.OptIn;

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock()
                    {
                        Text = messageContent,
                        Wrap = true
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    CreateTakeTourAction(),
                    AdaptiveCardHelper.CreateSubmitAction(Resources.EditProfileButtonText, MessageIds.EditProfile),
                    AdaptiveCardHelper.CreateSubmitAction(pauseOrResumeMatchesButtonText, pauseOrResumeMatchesMessage)
                }
            };

            if (showAdminActions)
            {
                var adminActions = AdaptiveCardHelper.CreateAdminActions(adminTeamContext);
                card.Actions.AddRange(adminActions);
            }

            return card.ToJson();
        }

        private static AdaptiveOpenUrlAction CreateTakeTourAction()
        {
            var tourButtonText = Resources.TakeATourButtonText;
            var tourUrl = AdaptiveCardHelper.CreateTourUrl();

            return new AdaptiveOpenUrlAction
            {
                Title = tourButtonText,
                Url = new Uri(tourUrl)
            };
        }
    }
}