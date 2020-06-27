//----------------------------------------------------------------------------------------------
// <copyright file="MatchResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Results from matching users into pairs
    /// </summary>
    public class MatchResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatchResult"/> class.
        /// </summary>
        public MatchResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MatchResult"/> class.
        /// </summary>
        /// <param name="pairs">results of pairing</param>
        /// <param name="oddPerson">optional odd person</param>
        public MatchResult(IList<Tuple<ChannelAccount, ChannelAccount>> pairs, ChannelAccount oddPerson = null)
        {
            this.Pairs = pairs;
            this.OddPerson = oddPerson;
        }

        /// <summary>
        /// Gets the pairs of users
        /// </summary>
        public IList<Tuple<ChannelAccount, ChannelAccount>> Pairs { get; private set; } = new List<Tuple<ChannelAccount, ChannelAccount>>();

        /// <summary>
        /// Gets the optional odd person which may happen if we have an odd number of users to pair
        /// </summary>
        public ChannelAccount OddPerson { get; private set; }
    }
}