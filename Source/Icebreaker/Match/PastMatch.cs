//----------------------------------------------------------------------------------------------
// <copyright file="PastMatch.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;

    /// <summary>
    /// Past match data
    /// </summary>
    public class PastMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PastMatch"/> class.
        /// </summary>
        /// <param name="userId">past match user AAD id</param>
        /// <param name="matchedAt">date the match was at</param>
        public PastMatch(string userId, DateTime matchedAt)
        {
            this.UserId = userId;
            this.MatchedAt = matchedAt;
        }

        /// <summary>
        /// Gets the user AAD id
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Gets the date the match occurred
        /// </summary>
        public DateTime MatchedAt { get; private set; }
    }
}