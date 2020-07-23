//----------------------------------------------------------------------------------------------
// <copyright file="RandomMatchCreator.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Randomly match the users based on no other data.
    /// </summary>
    public class RandomMatchCreator : IMatchCreator
    {
        private readonly Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomMatchCreator"/> class.
        /// </summary>
        /// <param name="random">random generator</param>
        public RandomMatchCreator(Random random)
        {
            this.random = random;
        }

        /// <summary>
        /// Create pairs from the set of users.
        /// </summary>
        /// <param name="users">users to make pairs</param>
        /// <returns>a list of pairs</returns>
        public MatchResult CreateMatches(List<ChannelAccount> users)
        {
            RandomAlgorithm.Shuffle(this.random, users);

            var pairs = new List<MatchResult.MatchPair>();
            int i = 0;
            for (; i < users.Count - 1; i += 2)
            {
                // TODO: Get real isPreviouslyMatched by going to the DB.
                // Not bothering with that right now because this is only used for small sets.
                pairs.Add(new MatchResult.MatchPair(users[i], users[i + 1], isPreviouslyMatched: false));
            }

            var oddPerson = i == users.Count ? null : users[i];
            return new MatchResult(pairs, oddPerson);
        }
    }
}