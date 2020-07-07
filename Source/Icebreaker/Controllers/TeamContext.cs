//----------------------------------------------------------------------------------------------
// <copyright file="TeamContext.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    /// <summary>
    /// Add team context to messages
    /// </summary>
    public struct TeamContext
    {
        /// <summary>
        /// Gets or sets the id of the team
        /// </summary>
        public string TeamId { get; set; }

        /// <summary>
        /// Gets or sets the name of the team
        /// </summary>
        public string TeamName { get; set; }
    }
}