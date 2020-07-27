//----------------------------------------------------------------------------------------------
// <copyright file="EditUserProfileAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;
    using global::AdaptiveCards;
    using Newtonsoft.Json;

    /// <summary>
    /// Builder class for the edit user profile card
    /// </summary>
    public static class EditUserProfileAdaptiveCard
    {
        private static readonly string DEFAULTDISCIPLINE = "data";
        private static readonly string DEFAULTGENDER = "female";
        private static readonly string DEFAULTSENIORITY = "intern";

        private static readonly string CardTemplate;

        static EditUserProfileAdaptiveCard()
        {
            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/EditUserProfileAdaptiveCard.json");
            CardTemplate = File.ReadAllText(cardJsonFilePath);
        }

        /// <summary>
        /// Creates the editable user profile card
        /// </summary>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <param name="subteamNamesHint">List of suggested sub team names. Can be empty</param>
        /// <param name="titleSize">Size of the title</param>
        /// <returns>user profile card</returns>
        public static string GetCardJson(
            string discipline,
            string gender,
            string seniority,
            List<string> teams,
            string subteamNamesHint,
            string titleSize = "Large")
        {
            var teamNamesHint = string.IsNullOrEmpty(subteamNamesHint) ? string.Empty : "Suggested Teams: " + subteamNamesHint;

            // TODO: Lots of strings to put in the resources including those in the json file
            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", "Please tell me about yourself" },
                { "titleSize", titleSize },
                { "description", "This helps me improve your matches." },
                { "defaultDiscipline", GetValueOrDefault(discipline, DEFAULTDISCIPLINE) },
                { "defaultGender", GetValueOrDefault(gender, DEFAULTGENDER) },
                { "defaultSeniority", GetValueOrDefault(seniority, DEFAULTSENIORITY) },
                { "defaultTeams", string.Join(AdaptiveCardHelper.TeamsSeparatorWithSpace, teams) },
                { "teamNamesHint", teamNamesHint }
            };

            return AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);
        }

        /// <summary>
        /// Creates the read only user profile card
        /// </summary>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetResultCard(string discipline, string gender, string seniority, List<string> teams)
        {
            var pairs = GetDataForResultCard(discipline, gender, seniority, teams);
            return AdaptiveCardHelper.CreateSubmitResultCard("Saved Your Profile", pairs);
        }

        /// <summary>
        /// Creates the data for the read only user profile card
        /// </summary>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <returns>pairs of data</returns>
        public static List<Tuple<string, string>> GetDataForResultCard(string discipline, string gender, string seniority, List<string> teams)
        {
            return new List<Tuple<string, string>>
            {
                new Tuple<string, string>("Discipline", GetUIText(discipline)),
                new Tuple<string, string>("Subteams", string.Join(AdaptiveCardHelper.TeamsSeparatorWithSpace, teams)),
                new Tuple<string, string>("Seniority", GetUIText(seniority)),
                new Tuple<string, string>("Gender", GetUIText(gender))
            };
        }

        /// <summary>
        /// Convert the stored value to a user presentable value.
        /// Mention if the value is empty.
        /// </summary>
        /// <param name="value">value to display</param>
        /// <returns>Value for the user</returns>
        private static string GetUIText(string value) => string.IsNullOrEmpty(value) ? "<empty>" : value;

        private static string GetValueOrDefault(string value, string defaultValue) => string.IsNullOrEmpty(value) ? defaultValue : value;

        /// <summary>
        /// Class to encapsulate the data returned by the adaptive card.
        /// The member name need match the "Id" attribute and any data in the Submit action in the adaptive card.
        /// See EditUserProfileAdaptiveCard.json
        /// </summary>
        public class UserProfile
        {
            /// <summary>
            /// Gets or sets User discipline
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Discipline { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets User seniority
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Seniority { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets User gender
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Gender { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets Names of subteams separated by commas
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Subteams { get; set; } = string.Empty;
        }
    }
}