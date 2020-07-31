//----------------------------------------------------------------------------------------------
// <copyright file="UnrecognizedInputAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Services.Description;
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
        /// <param name="actionsTeamContext">Team context for the actions</param>
        /// <param name="userStatusForTeam">Enrollment status for the actionsTeamContext for the user</param>
        /// <param name="showAdminActions">Whether to show the admin actions</param>
        /// <returns>The adaptive card for the unrecognized input</returns>
        public static string GetCardJson(TeamContext actionsTeamContext, EnrollmentStatus userStatusForTeam, bool showAdminActions)
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
                }
            };

            card.Actions = new List<AdaptiveAction>();

            card.Actions.AddRange(AdaptiveCardHelper.CreateUserActions(actionsTeamContext, userStatusForTeam));

            if (showAdminActions)
            {
                var adminActions = AdaptiveCardHelper.CreateAdminActions(actionsTeamContext);
                card.Actions.AddRange(adminActions);
            }

            return card.ToJson();
        }
    }
}