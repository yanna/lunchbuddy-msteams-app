//----------------------------------------------------------------------------------------------
// <copyright file="RandomAlgorithm.cs" company="Microsoft">
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
    public class RandomAlgorithm : IMatchAlgorithm
    {
        private readonly Random random;

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomAlgorithm"/> class.
        /// </summary>
        /// <param name="random">random generator</param>
        public RandomAlgorithm(Random random)
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
            this.Shuffle(users);

            var pairs = new List<Tuple<ChannelAccount, ChannelAccount>>();
            int i = 0;
            for (; i < users.Count - 1; i += 2)
            {
                pairs.Add(new Tuple<ChannelAccount, ChannelAccount>(users[i], users[i + 1]));
            }

            var oddPerson = i == users.Count ? null : users[i];
            return new MatchResult(pairs, oddPerson);
        }

        /// <summary>
        /// Randomly shuffle a list of items
        /// </summary>
        /// <typeparam name="T">type of item</typeparam>
        /// <param name="items">shuffled items</param>
        public void Shuffle<T>(IList<T> items)
        {
            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = this.random.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
}