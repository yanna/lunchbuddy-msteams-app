//----------------------------------------------------------------------------------------------
// <copyright file="MakePairsResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result of generating pairs from opted in users in the team
    /// </summary>
    public class MakePairsResult
    {
        /// <summary>
        /// Gets or sets the channel account ids of the pair
        /// </summary>
        public List<Tuple<string, string>> PairChannelAccountIds { get; set; } = new List<Tuple<string, string>>();

        /// <summary>
        /// Gets or sets the team id the ChannelAccount ids were from
        /// </summary>
        public string TeamId { get; set; } = string.Empty;
    }
}