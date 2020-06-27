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
    public class RandomAlgorithm
    {
        /// <summary>
        /// Run the algorithm
        /// </summary>
        /// <param name="users">users to make pairs</param>
        /// <returns>a list of pairs</returns>
        public static List<Tuple<ChannelAccount, ChannelAccount>> CreatePairs(List<ChannelAccount> users)
        {
            Randomize(users);

            var pairs = new List<Tuple<ChannelAccount, ChannelAccount>>();
            for (int i = 0; i < users.Count - 1; i += 2)
            {
                pairs.Add(new Tuple<ChannelAccount, ChannelAccount>(users[i], users[i + 1]));
            }

            return pairs;
        }

        private static void Randomize<T>(IList<T> items)
        {
            Random rand = new Random(Guid.NewGuid().GetHashCode());

            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = rand.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
}