//----------------------------------------------------------------------------------------------
// <copyright file=""EditUserProfileAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

    /// <summary>
    /// Builder class for the welcome new member card
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
        /// Creates the user profile card
        /// </summary>
        /// <param name="discipline">The team name</param>
        /// <param name="gender">The first name of the new member</param>
        /// <param name="seniority">The bot name</param>
        /// <param name="teams">The person that installed the bot to the team</param>
        /// <returns>user profile card</returns>
        public static string GetCard(string discipline, string gender, string seniority, List<string> teams)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", "Please tell me about yourself" },
                { "body", "This helps me provide you with better matches." },
                { "defaultDiscipline", discipline },
                { "defaultGender", gender },
                { "defaultSeniority", seniority },
                { "defaultTeams", string.Join(",", teams) }
            };

            var cardBody = CardTemplate;
            foreach (var kvp in variablesToValues)
            {
                cardBody = cardBody.Replace($"%{kvp.Key}%", kvp.Value);
            }

            return cardBody;
        }
    }
}