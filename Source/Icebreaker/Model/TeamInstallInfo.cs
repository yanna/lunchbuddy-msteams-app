//----------------------------------------------------------------------------------------------
// <copyright file="TeamInstallInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Model
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents information about a team to which the LunchBuddy app was installed
    /// </summary>
    public class TeamInstallInfo : Document
    {
        /// <summary>
        /// Bot will create matches and send all pairs the match immediately
        /// </summary>
        [JsonIgnore]
        public const string NotifyModeNoApproval = "noapproval";

        /// <summary>
        /// Bot will create matches and send pairing to ApprovalUserId for approval
        /// </summary>
        [JsonIgnore]
        public const string NotifyModeNeedApproval = "needapproval";

        /// <summary>
        /// Gets or sets the team id.
        /// This is also the <see cref="Resource.Id"/>.
        /// </summary>
        [JsonIgnore]
        public string TeamId
        {
            get { return this.Id; }
            set { this.Id = value; }
        }

        /// <summary>
        /// Gets or sets the tenant id
        /// </summary>
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service URL
        /// </summary>
        [JsonProperty("serviceUrl")]
        public string ServiceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the person that installed the bot to the team.
        /// Can be empty if the bot was installed via Graph
        /// </summary>
        [JsonProperty("installerName")]
        public string InstallerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user that can perform admin actions.
        /// Defaults to the person who installed the bot.
        /// Can be empty if the bot was installed via Graph.
        /// Note: This is duplicated in UserInfo:AdminForTeams
        /// </summary>
        [JsonProperty("adminUser")]
        public User AdminUser { get; set; }

        /// <summary>
        /// Gets or sets the mode of how matches will be notified
        /// </summary>
        [JsonProperty("notifyMode")]
        public string NotifyMode { get; set; } = NotifyModeNoApproval;

        /// <summary>
        /// Gets or sets the subteam names hint for Edit Profile page.
        /// Can be empty if it was never configured.
        /// This is so that multiple people on the same team will conform to the same name.
        /// </summary>
        [JsonProperty("subteamNames")]
        public string SubteamNames { get; set; } = string.Empty;

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Team - Id = {this.TeamId},TenantId = {this.TenantId}, ServiceUrl = {this.ServiceUrl}, " +
                $"InstallerName = {this.InstallerName}, AdminUserChannelAccountId = {this.AdminUser?.ChannelAccountId}, " +
                $"AdminUserId = {this.AdminUser?.UserId}, NotifyMode = {this.NotifyMode}";
        }

        /// <summary>
        /// User definition
        /// </summary>
        public class User
        {
            /// <summary>
            /// Gets or sets the user AAD id of the person.
            /// This value is the same for the same person.
            /// </summary>
            [JsonProperty("userId")]
            public string UserId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the ChannelAccount id of the person.
            /// This id is dependent on the channel so the same person can have multiple ChannelAccount ids
            /// Do not use this to check if the user is the same.
            /// </summary>
            [JsonProperty("channelAccountId")]
            public string ChannelAccountId { get; set; } = string.Empty;
        }
    }
}