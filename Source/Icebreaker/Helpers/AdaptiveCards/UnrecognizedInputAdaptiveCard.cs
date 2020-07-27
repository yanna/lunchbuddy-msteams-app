//----------------------------------------------------------------------------------------------
// <copyright file="UnrecognizedInputAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Model;
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
        /// <param name="userStatus">User status</param>
        /// <param name="showAdminActions">Whether to show the admin actions</param>
        /// <param name="adminTeamContext">Can be null. The admin team context for the admin actions</param>
        /// <returns>The adaptive card for the unrecognized input</returns>
        public static string GetCardJson(EnrollmentStatus userStatus, bool showAdminActions, TeamContext adminTeamContext)
        {
            var messageContent = Resources.UnrecognizedInput;

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
                    AdaptiveCardHelper.CreateStatusSubmitAction(userStatus),
                    AdaptiveCardHelper.CreateSubmitAction(Resources.EditProfileButtonText, MessageIds.EditProfile),
                }
            };

            if (showAdminActions)
            {
                var adminActions = AdaptiveCardHelper.CreateAdminActions(adminTeamContext);
                card.Actions.AddRange(adminActions);
            }

            return card.ToJson();
        }
    }
}