//----------------------------------------------------------------------------------------------
// <copyright file="EditUserInfoAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Builder class for the edit user info card which includes the user profile and
    /// the user opt in status
    /// </summary>
    public static class EditUserInfoAdaptiveCard
    {
        static EditUserInfoAdaptiveCard()
        {
        }

        /// <summary>
        /// Creates the editable user profile card which uses the EditUserProfileAdaptiveCard contents and adds
        /// an additional input for changing the opt in status
        /// </summary>
        /// <param name="userId">User AAD id</param>
        /// <param name="userName">User display name</param>
        /// <param name="optedIn">whether user is opted in to matches</param>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <param name="subteamNamesHint">List of suggested sub team names. Can be empty</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetCard(
            string userId,
            string userName,
            bool optedIn,
            string discipline,
            string gender,
            string seniority,
            List<string> teams,
            string subteamNamesHint)
        {
            var editProfileCardJson = EditUserProfileAdaptiveCard.GetCardJson(discipline, gender, seniority, teams, subteamNamesHint, titleSize: "Medium");
            var card = AdaptiveCard.FromJson(editProfileCardJson).Card;
            var editProfileBody = card.Body;

            var editInfoCard = new AdaptiveCard(new AdaptiveSchemaVersion(1, 2))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock()
                    {
                        Text = "Admin Edit User Info For " + userName,
                        Size = AdaptiveTextSize.Large,
                        Wrap = true,
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveTextBlock()
                    {
                        Text = "Enrollment",
                        Size = AdaptiveTextSize.Medium,
                        Weight = AdaptiveTextWeight.Bolder,
                    },
                    new AdaptiveChoiceSetInput()
                    {
                        Id = "OptedIn",
                        Style = AdaptiveChoiceInputStyle.Expanded,
                        Choices = new List<AdaptiveChoice>
                        {
                            new AdaptiveChoice
                            {
                                Title = "Opt In",
                                Value = "True"
                            },
                            new AdaptiveChoice
                            {
                                Title = "Opt Out",
                                Value = "False"
                            }
                        },
                        Value = optedIn.ToString()
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "Submit",
                        Data = JObject.FromObject(new { UserAadId = userId })
                    }
                }
            };

            editInfoCard.Body.AddRange(editProfileBody);

            return editInfoCard;
        }
    }
}