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
    using System.Linq;
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

        /// <summary>
        /// When displaying the list of values from the database to the user, use this to separate them.
        /// </summary>
        private static readonly string TeamsSeparatorWithSpace = ", ";
        private static readonly string NamesSeparatorWithSpace = ", ";

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
        /// <param name="lowPreferenceNames">List of full names the user has low preference for. Can be empty</param>
        /// <returns>user profile card</returns>
        public static string GetCardJson(
            string userId,
            string teamName,
            string discipline,
            string gender,
            string seniority,
            List<string> teams,
            string subteamNamesHint,
            List<string> lowPreferenceNames)
        {
            var teamNamesHint = string.IsNullOrEmpty(subteamNamesHint) ? string.Empty : $"Teams in {teamName}: " + subteamNamesHint;

            // TODO: Lots of strings to put in the resources including those in the json file
            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", $"Tell me about yourself" },
                { "description", "This helps me improve your matches." },
                { "defaultDiscipline", GetValueOrDefault(discipline, DEFAULTDISCIPLINE) },
                { "defaultGender", GetValueOrDefault(gender, DEFAULTGENDER) },
                { "defaultSeniority", GetValueOrDefault(seniority, DEFAULTSENIORITY) },
                { "defaultTeams", string.Join(TeamsSeparatorWithSpace, teams) },
                { "teamNamesHint", teamNamesHint },
                { "defaultLowPreferenceNames", string.Join(NamesSeparatorWithSpace, lowPreferenceNames) },
                { "userId", userId }
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
        /// <param name="lowPreferenceNames">Full names of low preference matches</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetResultCard(string discipline, string gender, string seniority, List<string> teams, List<string> lowPreferenceNames)
        {
            var pairs = GetDataForResultCard(discipline, gender, seniority, teams, lowPreferenceNames);
            return AdaptiveCardHelper.CreateSubmitResultCard("Saved Your Profile", pairs);
        }

        /// <summary>
        /// Creates the data for the read only user profile card
        /// </summary>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <param name="lowPreferenceNames">Full names of users the person has low preference for</param>
        /// <returns>pairs of data</returns>
        public static List<Tuple<string, string>> GetDataForResultCard(string discipline, string gender, string seniority, List<string> teams, List<string> lowPreferenceNames)
        {
            return new List<Tuple<string, string>>
            {
                new Tuple<string, string>("Discipline", GetUIText(discipline)),
                new Tuple<string, string>("Subteams", string.Join(TeamsSeparatorWithSpace, teams)),
                new Tuple<string, string>("Seniority", GetUIText(seniority)),
                new Tuple<string, string>("Gender", GetUIText(gender)),
                new Tuple<string, string>("Low Pref", string.Join(NamesSeparatorWithSpace, lowPreferenceNames))
            };
        }

        /// <summary>
        /// Get list of sub teams based on the string
        /// </summary>
        /// <param name="subteams">subteam names separated by a separator</param>
        /// <returns>List of subteam names</returns>
        public static List<string> GetSubteams(string subteams)
        {
            return GetSeparatedValues(subteams, TeamsSeparatorWithSpace);
        }

        /// <summary>
        /// Get list of full names based on the string
        /// </summary>
        /// <param name="fullNames">Full names separated by a separator</param>
        /// <returns>List of names</returns>
        public static List<string> GetLowPreferenceNames(string fullNames)
        {
            return GetSeparatedValues(fullNames, NamesSeparatorWithSpace);
        }

        private static List<string> GetSeparatedValues(string valuesWithSeparator, string separator)
        {
            // Who knows whether users will enter the separator and a space, so split without the space and trim.
            string[] separators = { separator.Trim() };
            var splitValues = valuesWithSeparator.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            return splitValues.Select(team => team.Trim()).ToList();
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
            /// Gets or sets User AAD id
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string UserId { get; set; } = string.Empty;

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

            /// <summary>
            /// Gets or sets the full names of people the user has low preference for, separated by commas
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string LowPreferenceNames { get; set; } = string.Empty;
        }
    }
}