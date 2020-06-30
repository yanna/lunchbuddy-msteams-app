//----------------------------------------------------------------------------------------------
// <copyright file="UserInfo.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
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
        /// Gets or sets the tenant id
        /// </summary>
        [JsonProperty("tenantId")]
        public string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the service URL
        /// </summary>
        [JsonProperty("serviceUrl")]
        public string ServiceUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is opted in to pairups.
        /// </summary>
        [JsonProperty("optedIn")]
        public bool OptedIn { get; set; }

        /// <summary>
        /// Gets or sets the user's discipline [data,design,engineering,pm] + custom
        /// </summary>
        [JsonProperty("discipline")]
        public string Discipline { get; set; }

        /// <summary>
        /// Gets or sets the user's discipline [female, male] + custom
        /// </summary>
        [JsonProperty("gender")]
        public string Gender { get; set; }

        /// <summary>
        /// Gets or sets the user's seniority [intern,level1,level2,senior,principal,partner] + custom
        /// </summary>
        [JsonProperty("seniority")]
        public string Seniority { get; set; }

        /// <summary>
        /// Gets or sets the internal teams the user has belonged to.
        /// This would be more fine grained than the larger team name.
        /// </summary>
        [JsonProperty("teams")]
        public List<string> Teams { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a list of past matches
        /// </summary>
        [JsonProperty("pastMatches")]
        public List<UserMatch> Matches { get; set; } = new List<UserMatch>();
    }
}