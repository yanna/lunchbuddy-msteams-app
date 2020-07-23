//----------------------------------------------------------------------------------------------
// <copyright file="StableMarriageMatchCreator.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Icebreaker.Helpers;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// The Gale-Shapley Stable Marriage Algorithm states that given N men and N women,
    /// where each person has ranked all members of the opposite sex in order of preference,
    /// marry the men and women together such that there are no two people of opposite sex who
    /// would both rather have each other than their current partners.
    /// If there are no such people, all the marriages are “stable”.
    ///
    ///  Note that the two groups are called male and female for the purposes of the algorithm only.
    /// It is not necessarily the case that everyone in female group is a female etc.
    /// </summary>
    public class StableMarriageMatchCreator : IMatchCreator
    {
        private readonly Random random;
        private readonly IDictionary<string, PersonData> peopleData;
        private readonly int numRetryOnPreviouslyMatchedPair;

        /// <summary>
        /// Initializes a new instance of the <see cref="StableMarriageMatchCreator"/> class.
        /// </summary>
        /// <param name="random">random generator</param>
        /// <param name="peopleData">userId to PersonData objects</param>
        /// <param name="numRetryOnPreviouslyMatchedPair">number of times to reshuffle and try again if a matched pair is found</param>
        public StableMarriageMatchCreator(Random random, IDictionary<string, PersonData> peopleData, int numRetryOnPreviouslyMatchedPair)
        {
            this.random = random;
            this.peopleData = peopleData;
            this.numRetryOnPreviouslyMatchedPair = numRetryOnPreviouslyMatchedPair;
        }

        /// <summary>
        /// Create pairs from the set of users
        /// </summary>
        /// <param name="channelAccounts">users to make pairs. order will be shuffled.</param>
        /// <returns>a list of pairs</returns>
        public MatchResult CreateMatches(List<ChannelAccount> channelAccounts)
        {
            var people = channelAccounts.Select(account => new Person<ChannelAccount>(account)).ToList();

            var numRetries = this.numRetryOnPreviouslyMatchedPair;
            MatchResult result;
            do
            {
                RandomAlgorithm.Shuffle<Person<ChannelAccount>>(this.random, people);
                result = this.CreateMatches(people);
            }
            while (result.HasAnyPreviouslyMatchedPair && (numRetries-- > 0));

            return result;
        }

        private MatchResult CreateMatches(List<Person<ChannelAccount>> people)
        {
            // 1. Split into two groups
            var halfPeopleCount = people.Count / 2;
            var group1 = people.GetRange(0, halfPeopleCount);
            var group2 = people.GetRange(halfPeopleCount, halfPeopleCount);
            var oddPerson = people.Count % 2 == 0 ? null : people.Last().Data;

            var userIdToPerson = people.ToDictionary(
                person => person.Data.GetUserId(),
                person => person);

            // 2. Get preferences for each group
            group1.ForEach(person => person.Preferences = new PersonPreferences(person.Data.GetUserId(), group2, userIdToPerson, this.peopleData).Get());
            group2.ForEach(person => person.Preferences = new PersonPreferences(person.Data.GetUserId(), group1, userIdToPerson, this.peopleData).Get());

            // 3. Run stable marriage
            StableMarriageAlgorithm.DoMarriage<ChannelAccount>(group1);
            var pairs = group1.Select(person =>
                {
                    var person1 = person.Data;
                    var person2 = person.Fiance.Data;
                    var isPreviouslyMatched = this.GetIsPreviouslyMatched(person1.GetUserId(), person2.GetUserId());
                    return new MatchResult.MatchPair(person1, person2, isPreviouslyMatched);
                }).ToList();
            return new MatchResult(pairs, oddPerson);
        }

        private bool GetIsPreviouslyMatched(string userId1, string userId2)
        {
            PersonData personData;
            this.peopleData.TryGetValue(userId1, out personData);
            var pastMatch = personData?.PastMatches.Find(m => m.UserId == userId2);
            return pastMatch != null;
        }
    }
}