//----------------------------------------------------------------------------------------------
// <copyright file="UserContext.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using Newtonsoft.Json;

    /// <summary>
    /// Add user context to messages
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// Gets or sets the id of the user
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string UserAadId { get; set; }

        /// <summary>
        /// Gets or sets the name of the user
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string UserName { get; set; }
    }
}