//----------------------------------------------------------------------------------------------
// <copyright file="MatchResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;
    using System.Linq;
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
        public MatchResult(IList<MatchPair> pairs, ChannelAccount oddPerson = null)
        {
            this.Pairs = pairs;
            this.OddPerson = oddPerson;
        }

        /// <summary>
        /// Gets the pairs of users
        /// </summary>
        public IList<MatchPair> Pairs { get; private set; } = new List<MatchPair>();

        /// <summary>
        /// Gets the optional odd person which may happen if we have an odd number of users to pair
        /// </summary>
        public ChannelAccount OddPerson { get; private set; }

        /// <summary>
        /// Gets a value indicating whether there was any previously matched pairs in the results
        /// </summary>
        public bool HasAnyPreviouslyMatchedPair
        {
            get
            {
                return this.Pairs.Any(p => p.IsPreviouslyMatched);
            }
        }

        /// <summary>
        /// Matched pair of people
        /// </summary>
        public class MatchPair
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MatchPair"/> class.
            /// </summary>
            /// <param name="person1">Person 1 of pair</param>
            /// <param name="person2">Pereson 2 of pair</param>
            /// <param name="isPreviouslyMatched">Whether the pair has been matched before</param>
            public MatchPair(ChannelAccount person1, ChannelAccount person2, bool isPreviouslyMatched)
            {
                this.Person1 = person1;
                this.Person2 = person2;
                this.IsPreviouslyMatched = isPreviouslyMatched;
            }

            /// <summary>
            /// Gets person 1 of the matched pair
            /// </summary>
            public ChannelAccount Person1 { get; private set; }

            /// <summary>
            /// Gets person 2 of the matched pair
            /// </summary>
            public ChannelAccount Person2 { get; private set; }

            /// <summary>
            /// Gets a value indicating whether the pair was previously matched
            /// </summary>
            public bool IsPreviouslyMatched { get; private set; }
        }
    }
}