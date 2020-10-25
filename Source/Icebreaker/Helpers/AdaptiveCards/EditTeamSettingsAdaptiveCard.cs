// <copyright file="EditTeamSettingsAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Web.Hosting;
    using global::AdaptiveCards;
    using Icebreaker.Model;
    using Newtonsoft.Json;

    /// <summary>
    /// Card for editing the team settings
    /// </summary>
    public static class EditTeamSettingsAdaptiveCard
    {
        private static readonly string EmptyUserText = "None";
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
        /// <param name="adminUser">Admin user. Can be null if bot installed by Graph or changed to no admin</param>
        /// <param name="teamNotifyMode">How pairings will be notified</param>
        /// <param name="subteamNames">Sub team names hints for Edit Profile page</param>
        /// <returns>user profile card</returns>
        public static AdaptiveCard GetCard(string teamId, string teamName, User adminUser, string teamNotifyMode, string subteamNames)
        {
            var variablesToValues = new Dictionary<string, string>()
            {
                { "teamId", teamId },
                { "teamName", teamName },
                { "noApprovalValue", TeamInstallInfo.NotifyModeNoApproval },
                { "needApprovalValue", TeamInstallInfo.NotifyModeNeedApproval },
                { "defaultNotifyMode", teamNotifyMode },
                { "subteamNames", subteamNames },
                { "adminUserName", adminUser == null ? string.Empty : adminUser.Name },
                { "originalAdminUserId", adminUser == null ? string.Empty : adminUser.AadId },
                { "originalAdminUserName", adminUser == null ? string.Empty : adminUser.Name }
            };

            var cardJson = AdaptiveCardHelper.ReplaceTemplateKeys(CardTemplate, variablesToValues);

            // There is an AdaptiveCard template library but it's only for .NET core.
            var card = AdaptiveCard.FromJson(cardJson).Card;
            return card;
        }

        /// <summary>
        /// Creates the read only team settings card
        /// </summary>
        /// <param name="adminUserName">Admin user name</param>
        /// <param name="notifyMode">Notify mode</param>
        /// <param name="subteamNames">Subteam names</param>
        /// <returns>team settings card</returns>
        public static AdaptiveCard GetResultCard(string adminUserName, string notifyMode, string subteamNames)
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
                new Tuple<string, string>("Admin User", GetAdminText(adminUserName)),
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

        private static string GetAdminText(string value) => string.IsNullOrEmpty(value) ? EmptyUserText : value;

        /// <summary>
        /// User class to specify the AAD id and display name
        /// </summary>
        public class User
        {
            /// <summary>
            /// Gets or sets the AAD id
            /// </summary>
            public string AadId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the Display name
            /// </summary>
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Class to encapsulate the data returned by the adaptive card.
        /// The member name need match the "Id" attribute and any data in the Submit action in the adaptive card.
        /// See EditTeamSettingsAdaptiveCard.json
        /// </summary>
        public class TeamSettings
        {
            /// <summary>
            /// Gets or sets the Admin user name. Can be empty.
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string OriginalAdminUserName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the Admin user id. Can be empty.
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string OriginalAdminUserId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the newly entered admin user name
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string AdminUserName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the notify mode
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string NotifyMode { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the subteam names
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string SubteamNames { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the Team id (extra data in submit action)
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string TeamId { get; set; } = string.Empty;
        }
    }
}