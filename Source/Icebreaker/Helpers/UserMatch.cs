//----------------------------------------------------------------------------------------------
// <copyright file="UserMatch.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using System;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a user match
    /// </summary>
    public class UserMatch : Document
    {
        /// <summary>
        /// Gets or sets the user AAD id
        /// </summary>
        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date the match occurred in UTC
        /// </summary>
        [JsonProperty("matchDateUtc")]
        public DateTime MatchDateUtc { get; set; }
    }
}