//----------------------------------------------------------------------------------------------
// <copyright file="ViewUserProfileAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    /// <summary>
    /// Builder class for the read only user profile card
    /// </summary>
    public static class ViewUserProfileAdaptiveCard
    {
        private static readonly string CardTemplate;

        static ViewUserProfileAdaptiveCard()
        {
            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/ViewUserProfileAdaptiveCard.json");
            CardTemplate = File.ReadAllText(cardJsonFilePath);
        }

        /// <summary>
        /// Creates the read only user profile card
        /// </summary>
        /// <param name="discipline">User discipline</param>
        /// <param name="gender">User gender</param>
        /// <param name="seniority">User seniority</param>
        /// <param name="teams">Sub team names the user has been on</param>
        /// <returns>user profile card</returns>
        public static string GetCardJson(string discipline, string gender, string seniority, List<string> teams)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", "Saved Profile" },
                { "discipline", GetUIText(discipline) },
                { "teams", string.Join(AdaptiveCardHelper.TeamsSeparatorWithSpace, teams) },
                { "seniority", GetUIText(seniority) },
                { "gender", GetUIText(gender) },
            };

            return AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);
        }

        /// <summary>
        /// Convert the stored value to a user presentable value.
        /// Mention if the value is empty.
        /// </summary>
        /// <param name="value">value to display</param>
        /// <returns>Value for the user</returns>
        private static string GetUIText(string value) => string.IsNullOrEmpty(value) ? "<empty>" : value;
    }
}