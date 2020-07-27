//----------------------------------------------------------------------------------------------
// <copyright file="UserInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Model
{
    using System;
    using System.Collections.Generic;
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
        /// Gets or sets user enrollment status [NotJoined,Active,Paused]
        /// </summary>
        [JsonIgnore]
        public EnrollmentStatus Status
        {
            get
            {
                try
                {
                    return (EnrollmentStatus)Enum.Parse(typeof(EnrollmentStatus), this.StatusInternal);
                }
                catch (Exception)
                {
                }

                return EnrollmentStatus.NotJoined;
            }

            set
            {
                this.StatusInternal = Enum.GetName(typeof(EnrollmentStatus), value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the user wants to receive matches
        /// </summary>
        [JsonIgnore]
        public bool IsActive
        {
            get { return this.Status == EnrollmentStatus.Active; }
        }

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
        /// Gets or sets a list of past matches. The first one is the most recent.
        /// </summary>
        /// Note: There is a 2MB limit on document sizes.
        /// One of these user info objects with one user match was 914 bytes, with two user matches was 1191 bytes
        /// So 1191-914=277, roughly 300 bytes for one user match object.
        /// Rough math (2M-1k)/300 bytes/12 matches per year = 555 years of user matches can be stored before we hit the limit so we're ok.
        [JsonProperty("pastMatches")]
        public List<UserMatch> Matches { get; set; } = new List<UserMatch>();

        /// <summary>
        /// Gets or sets a list of teams the member is no longer part of. Unsure if this happens when AAD accounts get disabled.
        /// </summary>
        [JsonProperty("removedFromTeamIds")]
        public List<string> RemovedFromTeamIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets user enrollment status [NotJoined,Active,Paused]
        /// </summary>
        [JsonProperty("status")]
        private string StatusInternal { get; set; } = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.NotJoined);
    }
}