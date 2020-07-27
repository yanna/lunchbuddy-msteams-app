//----------------------------------------------------------------------------------------------
// <copyright file="EditUserInfoAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using global::AdaptiveCards;
    using Icebreaker.Model;
    using Newtonsoft.Json;
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
        /// <param name="userStatus">whether user is opted in to matches</param>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <param name="subteamNamesHint">List of suggested sub team names. Can be empty</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetCard(
            string userId,
            string userName,
            EnrollmentStatus userStatus,
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
                        Text = "Status",
                        Size = AdaptiveTextSize.Medium,
                        Weight = AdaptiveTextWeight.Bolder,
                    },
                    new AdaptiveChoiceSetInput()
                    {
                        Id = "Status",
                        Style = AdaptiveChoiceInputStyle.Expanded,
                        Choices = new List<AdaptiveChoice>
                        {
                            new AdaptiveChoice
                            {
                                Title = "Not Joined",
                                Value = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.NotJoined)
                            },
                            new AdaptiveChoice
                            {
                                Title = "Active",
                                Value = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.Active)
                            },
                            new AdaptiveChoice
                            {
                                Title = "Inactive",
                                Value = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.Inactive)
                            }
                        },
                        Value = Enum.GetName(typeof(EnrollmentStatus), userStatus)
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

        /// <summary>
        /// Creates the read only user info card
        /// </summary>
        /// <param name="userStatus">Whether opted in to matches</param>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetResultCard(EnrollmentStatus userStatus, string discipline, string gender, string seniority, List<string> teams)
        {
            var pairs = new List<Tuple<string, string>>
            {
                new Tuple<string, string>("Status", Enum.GetName(typeof(EnrollmentStatus), userStatus))
            };
            pairs.AddRange(EditUserProfileAdaptiveCard.GetDataForResultCard(discipline, gender, seniority, teams));
            return AdaptiveCardHelper.CreateSubmitResultCard("Saved User Info", pairs);
        }

        /// <summary>
        /// Class to encapsulate the data returned by the adaptive card.
        /// The member name need match the "Id" attribute in the adaptive card.
        /// </summary>
        public class UserInfo : EditUserProfileAdaptiveCard.UserProfile
        {
            /// <summary>
            /// Gets or sets User status as a string. User GetStatus() to get the enum.
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Status { get; set; } = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.NotJoined);

            /// <summary>
            /// Gets or sets User AAD id
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string UserAadId { get; set; } = string.Empty;

            /// <summary>
            /// Return the status enum
            /// </summary>
            /// <returns>Enrollment status</returns>
            public EnrollmentStatus GetStatus()
            {
                try
                {
                    return (EnrollmentStatus)Enum.Parse(typeof(EnrollmentStatus), this.Status);
                }
                catch (Exception)
                {
                }

                return EnrollmentStatus.NotJoined;
            }
        }
    }
}