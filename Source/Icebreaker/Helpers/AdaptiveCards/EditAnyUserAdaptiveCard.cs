//----------------------------------------------------------------------------------------------
// <copyright file="EditAnyUserAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Model;

    /// <summary>
    /// Card for the admin to edit info about a user.
    /// </summary>
    public static class EditAnyUserAdaptiveCard
    {
        static EditAnyUserAdaptiveCard()
        {
        }

        /// <summary>
        /// Creates the editable user profile card which uses the EditUserProfileAdaptiveCard contents and adds
        /// an additional input for changing the opt in status
        /// </summary>
        /// <param name="userStatus">whether user is opted in to matches</param>
        /// <param name="userAndTeam">User and team info</param>
        /// <returns>Edit any user card</returns>
        public static AdaptiveCard GetCard(
            EnrollmentStatus userStatus,
            UserAndTeam userAndTeam)
        {
            var userName = userAndTeam.User.UserName;
            var teamName = userAndTeam.Team.TeamName;

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 2))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock()
                    {
                        Text = $"Edit User info for {userName} in {teamName}",
                        Size = AdaptiveTextSize.Large,
                        Wrap = true,
                        Weight = AdaptiveTextWeight.Bolder
                    }
                }
            };

            card.Actions = AdaptiveCardHelper.CreateUserActionsForAdmin(userAndTeam, userStatus);
            return card;
        }
    }
}