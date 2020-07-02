//----------------------------------------------------------------------------------------------
// <copyright file="AdaptiveCardHelper.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;

    /// <summary>
    /// Helper class for Adaptive Cards
    /// </summary>
    public static class AdaptiveCardHelper
    {
        /// <summary>
        /// What team names from the database are separated with when displayed to the user
        /// </summary>
        public const string TeamsSeparatorWithSpace = ", ";

        /// <summary>
        /// Replace the template keys with the provided values
        /// </summary>
        /// <param name="cardTemplate">Card template JSON</param>
        /// <param name="templateData">Data to replace the keys</param>
        /// <returns>JSON with replaced values</returns>
        public static string ReplaceTemplateKeys(string cardTemplate, IDictionary<string, string> templateData)
        {
            var cardBody = cardTemplate;

            foreach (var kvp in templateData)
            {
                cardBody = cardBody.Replace($"%{kvp.Key}%", kvp.Value);
            }

            return cardBody;
        }
    }
}