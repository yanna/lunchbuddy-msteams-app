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
    /// Builder class for the team settings card
    /// </summary>
    public static class ViewTeamSettingsAdaptiveCard
    {
        private static readonly string CardTemplate;

        static ViewTeamSettingsAdaptiveCard()
        {
            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/ViewTeamSettingsAdaptiveCard.json");
            CardTemplate = File.ReadAllText(cardJsonFilePath);
        }

        /// <summary>
        /// Creates the read only team settings card
        /// </summary>
        /// <param name="notifyMode">Notify mode</param>
        /// <param name="subteamNames">Subteam names</param>
        /// <returns>team settings card</returns>
        public static string GetCard(string notifyMode, string subteamNames)
        {
            var notifyModeDisplay = string.Empty;
            switch (notifyMode)
            {
                case TeamInstallInfo.NotifyModeNeedApproval:
                    notifyModeDisplay = "Need Approval";
                    break;
                case TeamInstallInfo.NotifyModeNoApproval:
                    notifyModeDisplay = "No Approval";
                    break;
            }

            var variablesToValues = new Dictionary<string, string>()
            {
                { "title", "Saved Team Settings" },
                { "notifyMode", notifyModeDisplay },
                { "subteamNames", GetUIText(subteamNames) }
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