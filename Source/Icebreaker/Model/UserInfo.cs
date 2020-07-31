//----------------------------------------------------------------------------------------------
// <copyright file="UserInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a user
    /// </summary>
    public class UserInfo : Document
    {
        /// <summary>
        /// Gets or sets the user's AAD id
        /// This is also the <see cref="Resource.Id"/>.
        /// </summary>
        [JsonIgnore]
        public string UserId
        {
            get { return this.Id; }
            set { this.Id = value; }
        }

        /// <summary>
        /// Gets or sets the enrollment status of the user for a team. If there are no status then the user has not joined at all.
        /// </summary>
        [JsonProperty("statusInTeam")]
        public List<UserEnrollmentStatus> StatusInTeam { get; set; } = new List<UserEnrollmentStatus>();

        /// <summary>
        /// Gets or sets the list of team ids this user is an admin for
        /// </summary>
        [JsonProperty("adminForTeams")]
        public List<string> AdminForTeams { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the tenant id
        /// </summary>
        [JsonProperty("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's discipline [data,design,engineering,pm,future,other]
        /// </summary>
        [JsonProperty("discipline")]
        public string Discipline { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's discipline [female,male,other]
        /// </summary>
        [JsonProperty("gender")]
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's seniority [intern,level1,level2,senior,principal,partner,other]
        /// </summary>
        [JsonProperty("seniority")]
        public string Seniority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the internal teams the user has belonged to.
        /// This would be more fine grained than the larger team name.
        /// </summary>
        [JsonProperty("subTeams")]
        public List<string> Subteams { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the full names of people the user has a low preference for in matches.
        /// Their preference will be after people they have already matched with.
        /// </summary>
        [JsonProperty("lowPreferences")]
        public List<string> LowPreferences { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a list of past matches. The first one is the most recent.
        /// </summary>
        /// Note: There is a 2MB limit on document sizes.
        /// One of these user info objects with one user match was 914 bytes, with two user matches was 1191 bytes
        /// So 1191-914=277, roughly 300 bytes for one user match object.
        /// Rough math (2M-1k)/300 bytes/12 matches per year = 555 years of user matches can be stored before we hit the limit so we're ok.
        [JsonProperty("pastMatches")]
        public List<UserMatch> Matches { get; set; } = new List<UserMatch>();

        /// <summary>
        /// Returns the status of the user for the team
        /// </summary>
        /// <param name="teamId">team id</param>
        /// <returns>User enrollment status</returns>
        public EnrollmentStatus GetStatusInTeam(string teamId)
        {
            return UserEnrollmentStatus.GetStatusInTeam(teamId, this.StatusInTeam);
        }

        /// <summary>
        /// Update the status for a team
        /// </summary>
        /// <param name="newStatus">user status</param>
        /// <param name="teamId">team id</param>
        /// <returns>Whether already had status for teamId</returns>
        public bool SetStatusInTeam(EnrollmentStatus newStatus, string teamId)
        {
            var numRemoved = this.StatusInTeam.RemoveAll(status => status.TeamId == teamId);
            this.StatusInTeam.Add(new UserEnrollmentStatus { TeamId = teamId, Status = newStatus });
            return numRemoved > 0;
        }

        /// <summary>
        /// Is user available for matches in the team
        /// </summary>
        /// <param name="teamId">team id</param>
        /// <returns>Whether user is active</returns>
        public bool IsActiveInTeam(string teamId)
        {
            return this.GetStatusInTeam(teamId) == EnrollmentStatus.Active;
        }
    }
}