//----------------------------------------------------------------------------------------------
// <copyright file="TeamContext.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    using Newtonsoft.Json;

    /// <summary>
    /// Add team context to messages
    /// </summary>
    public class TeamContext
    {
        /// <summary>
        /// Gets or sets the id of the team
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string TeamId { get; set; }

        /// <summary>
        /// Gets or sets the name of the team
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string TeamName { get; set; }
    }
}