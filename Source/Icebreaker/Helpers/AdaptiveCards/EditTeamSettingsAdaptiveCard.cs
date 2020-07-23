// <copyright file="EditTeamSettingsAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;
    using global::AdaptiveCards;
    using Icebreaker.Model;

    /// <summary>
    /// Card for editing the team settings
    /// </summary>
    public static class EditTeamSettingsAdaptiveCard
    {
        private static readonly string CardTemplate;

        static EditTeamSettingsAdaptiveCard()
        {
            var cardJsonFilePath = HostingEnvironment.MapPath("~/Helpers/AdaptiveCards/EditTeamSettingsAdaptiveCard.json");
            CardTemplate = File.ReadAllText(cardJsonFilePath);
        }

        /// <summary>
        /// Creates the edit team settings card
        /// </summary>
        /// <param name="teamId">Team id</param>
        /// <param name="teamName">User discipline</param>
        /// <param name="adminUserName">Admin user name. Can be empty if bot installed by Graph</param>
        /// <param name="teamNotifyMode">How pairings will be notified</param>
        /// <param name="subteamNames">Sub team names hints for Edit Profile page</param>
        /// <returns>user profile card</returns>
        public static string GetCardJson(string teamId, string teamName, string adminUserName, string teamNotifyMode, string subteamNames)
        {
            // TODO: Make admin reassignable to another person in the team
            var variablesToValues = new Dictionary<string, string>()
            {
                { "teamId", teamId },
                { "teamName", teamName },
                { "adminUserName", adminUserName },
                { "noApprovalValue", TeamInstallInfo.NotifyModeNoApproval },
                { "needApprovalValue", TeamInstallInfo.NotifyModeNeedApproval },
                { "defaultNotifyMode", teamNotifyMode },
                { "subteamNames", subteamNames }
            };

            return AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);
        }

        /// <summary>
        /// Creates the read only team settings card
        /// </summary>
        /// <param name="notifyMode">Notify mode</param>
        /// <param name="subteamNames">Subteam names</param>
        /// <returns>team settings card</returns>
        public static AdaptiveCard GetResultCard(string notifyMode, string subteamNames)
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

            var pairs = new List<Tuple<string, string>>()
            {
                new Tuple<string, string>("Subteam Names", GetUIText(subteamNames)),
                new Tuple<string, string>("Notify Mode", notifyModeDisplay)
            };

            return AdaptiveCardHelper.CreateSubmitResultCard("Saved Team Settings", pairs);
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