﻿// <copyright file="EditTeamSettingsAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.IO;
    using System.Web.Hosting;

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
        public static string GetCard(string teamId, string teamName, string adminUserName, string teamNotifyMode, string subteamNames)
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
    }
}