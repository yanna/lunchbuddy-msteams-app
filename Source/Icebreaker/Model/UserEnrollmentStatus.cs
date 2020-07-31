//----------------------------------------------------------------------------------------------
// <copyright file="UserEnrollmentStatus.cs" company="Microsoft">
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
    /// Represents a user's enrollment status in a team
    /// </summary>
    public class UserEnrollmentStatus : Document
    {
        /// <summary>
        /// Gets or sets user enrollment status
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
        /// Gets or sets the team id the status is for
        /// </summary>
        [JsonProperty("teamId")]
        public string TeamId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets user enrollment status
        /// </summary>
        [JsonProperty("status")]
        private string StatusInternal { get; set; } = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.NotJoined);

        /// <summary>
        /// Get the status of the user for a team
        /// </summary>
        /// <param name="teamId">team id</param>
        /// <param name="statuses">current statues for different teams</param>
        /// <returns>Status for the team</returns>
        public static EnrollmentStatus GetStatusInTeam(string teamId, List<UserEnrollmentStatus> statuses)
        {
            var teamStatus = statuses.FirstOrDefault(status => status.TeamId == teamId);
            return teamStatus == null ? EnrollmentStatus.NotJoined : teamStatus.Status;
        }
    }
}