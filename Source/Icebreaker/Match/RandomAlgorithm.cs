//----------------------------------------------------------------------------------------------
// <copyright file="RandomAlgorithm.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Randomly match the users based on no other data.
    /// </summary>
    public class RandomAlgorithm
    {
        /// <summary>
        /// Randomly shuffle a list of items
        /// </summary>
        /// <typeparam name="T">type of item</typeparam>
        /// <param name="random">Randomizer</param>
        /// <param name="items">shuffled items</param>
        public static void Shuffle<T>(Random random, IList<T> items)
        {
            // For each spot in the array, pick
            // a random item to swap into that spot.
            for (int i = 0; i < items.Count - 1; i++)
            {
                int j = random.Next(i, items.Count);
                T temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
}