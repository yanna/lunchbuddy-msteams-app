//----------------------------------------------------------------------------------------------
// <copyright file="EditAnyUserAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Controllers;
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
        /// <param name="userId">User AAD id</param>
        /// <param name="userName">User display name</param>
        /// <param name="userStatus">whether user is opted in to matches</param>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <param name="subteamNamesHint">List of suggested sub team names. Can be empty</param>
        /// <param name="lowPreferenceNames">Full names of people the user has low preference for. Can be empty.</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetCard(
            EnrollmentStatus userStatus,
            ChooseUserResult chooseUserAndTeamResult)
        {
            var userName = chooseUserAndTeamResult.GetUserName();
            var teamName = chooseUserAndTeamResult.TeamContext.TeamName;

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

            card.Actions = AdaptiveCardHelper.CreateUserActionsForAdmin(chooseUserAndTeamResult, userStatus);
            return card;
        }
    }
}