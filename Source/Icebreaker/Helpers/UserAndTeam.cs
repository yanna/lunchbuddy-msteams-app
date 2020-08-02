//----------------------------------------------------------------------------------------------
// <copyright file="UserAndTeam.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using Newtonsoft.Json;

    /// <summary>
    /// Add team context to messages
    /// </summary>
    public class UserAndTeam
    {
        /// <summary>
        /// Gets or sets the team context
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public TeamContext Team { get; set; }

        /// <summary>
        /// Gets or sets the user context
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public UserContext User { get; set; }
    }
}