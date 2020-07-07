//----------------------------------------------------------------------------------------------
// <copyright file="TeamInstallInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
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
        /// Gets or sets the name of the person that installed the bot to the team
        /// </summary>
        [JsonProperty("installerName")]
        public string InstallerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user AAD id of the person that can perform admin actions.
        /// These include manual generation of pair matches, notifying pairs of said matches, changing the notify mode etc.
        /// </summary>
        [JsonProperty("adminUserId")]
        public string AdminUserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the mode of how matches will be notified
        /// </summary>
        [JsonProperty("notifyMode")]
        public string NotifyMode { get; set; } = NotifyModeNoApproval;

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Team - Id = {this.TeamId},TenantId = {this.TenantId}, ServiceUrl = {this.ServiceUrl}, " +
                $"InstallerName = {this.InstallerName}, AdminUserId = {this.AdminUserId}, NotifyMode = {this.NotifyMode}";
        }
    }
}