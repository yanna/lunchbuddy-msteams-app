//----------------------------------------------------------------------------------------------
// <copyright file="EditUserProfileAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    /// <summary>
    /// Builder class for the edit user profile card
    /// </summary>
    public static class EditUserProfileAdaptiveCard
    {
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
        /// <returns>user profile card</returns>
        public static string GetCard(string discipline, string gender, string seniority, List<string> teams, string subteamNamesHint)
        {
            var teamNamesHint = string.IsNullOrEmpty(subteamNamesHint) ? string.Empty : "Suggested Teams: " + subteamNamesHint;

            // TODO: Lots of strings to put in the resources including those in the json file
            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", "Please tell me about yourself" },
                { "body", "This helps me improve your matches." },
                { "defaultDiscipline", discipline },
                { "defaultGender", gender },
                { "defaultSeniority", seniority },
                { "defaultTeams", string.Join(AdaptiveCardHelper.TeamsSeparatorWithSpace, teams) },
                { "teamNamesHint", teamNamesHint }
            };

            return AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);
        }
    }
}