//----------------------------------------------------------------------------------------------
// <copyright file="MakePairsResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.HeroCards
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Result of generating pairs from opted in users in the team
    /// </summary>
    public class MakePairsResult
    {
        /// <summary>
        /// Gets or sets the channel account ids of the pair
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public List<Tuple<string, string>> PairChannelAccountIds { get; set; } = new List<Tuple<string, string>>();

        /// <summary>
        /// Gets or sets the team id the ChannelAccount ids were from
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string TeamId { get; set; } = string.Empty;
    }
}