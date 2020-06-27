//----------------------------------------------------------------------------------------------
// <copyright file="StableMarriageAlgorithm.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    public class StableMarriageAlgorithm : IMatchAlgorithm
    {
        private readonly Random random;
        private readonly IDictionary<string, PersonData> peopleData;

        /// <summary>
        /// Initializes a new instance of the <see cref="StableMarriageAlgorithm"/> class.
        /// </summary>
        /// <param name="random">random generator</param>
        /// <param name="peopleData">people data</param>
        public StableMarriageAlgorithm(Random random, IDictionary<string, PersonData> peopleData)
        {
            this.random = random;
            this.peopleData = peopleData;
        }

        /// <summary>
        /// Perform the marriage match
        /// </summary>
        /// <param name="guys">Set of guys with preferences for each girl</param>
        /// <typeparam name="T">type of the data contained in person</typeparam>
        public static void DoMarriage<T>(IList<Person<T>> guys)
        {
            /*Gale-Shapley Stable Marriage algorithm
                https://en.wikipedia.org/wiki/Stable_marriage_problem
                function stableMatching {
                    Initialize all m ∈ M and w ∈ W to free
                    while ∃ free man m who still has a woman w to propose to {
                    w = first woman on m’s list to whom m has not yet proposed
                    if w is free
                        (m, w) become engaged
                    else some pair (m', w) already exists
                        if w prefers m to m'
                            m' becomes free
                            (m, w) become engaged
                        else
                            (m', w) remain engaged
                }
            */

            int freeGuysCount = guys.Count;
            while (freeGuysCount > 0)
            {
                var freeGuy = guys.FirstOrDefault(guy => guy.Fiance == null);
                if (freeGuy == null)
                {
                    break;
                }

                Person<T> gal = freeGuy.NextCandidateNotYetProposedTo();
                if (gal == null)
                {
                    break;
                }

                if (gal.Fiance == null)
                {
                    freeGuy.EngageTo(gal);
                    freeGuysCount--;
                }
                else if (gal.Prefers(freeGuy))
                {
                    freeGuy.EngageTo(gal);
                }
            }
        }

        /// <summary>
        /// Create pairs from the set of users
        /// </summary>
        /// <param name="channelAccounts">users to make pairs. order will be shuffled.</param>
        /// <returns>a list of pairs</returns>
        public MatchResult CreateMatches(List<ChannelAccount> channelAccounts)
        {
            var randomAlgorithm = new RandomAlgorithm(this.random);

            var channelAccountCount = channelAccounts.Count;
            if (channelAccountCount == 0)
            {
                return new MatchResult();
            }

            if (channelAccountCount == 1)
            {
                return new MatchResult(new List<Tuple<ChannelAccount, ChannelAccount>>(), channelAccounts.First());
            }

            if (channelAccountCount < 4)
            {
                return randomAlgorithm.CreateMatches(channelAccounts);
            }

            var people = channelAccounts.Select(account => new Person<ChannelAccount>(account)).ToList();
            randomAlgorithm.Shuffle<Person<ChannelAccount>>(people);

            return this.CreateMatches(people);
        }

        private MatchResult CreateMatches(List<Person<ChannelAccount>> people)
        {
            // 1. Split into two groups
            var halfPeopleCount = people.Count / 2;
            var group1 = people.GetRange(0, halfPeopleCount);
            var group2 = people.GetRange(halfPeopleCount, halfPeopleCount);
            var oddPerson = people.Count % 2 == 0 ? null : people.Last().Data;

            var userIdToPerson = people.ToDictionary(
                person => person.GetUserId(),
                person => person);

            // 2. Get preferences for each group
            group1.ForEach(person => person.Preferences = new PersonPreferences(person.GetUserId(), group2, userIdToPerson, this.peopleData).Get());
            group2.ForEach(person => person.Preferences = new PersonPreferences(person.GetUserId(), group1, userIdToPerson, this.peopleData).Get());

            // 3. Run stable marriage
            DoMarriage<ChannelAccount>(group1);
            var pairs = group1.Select(person => new Tuple<ChannelAccount, ChannelAccount>(person.Data, person.Fiance.Data)).ToList();
            return new MatchResult(pairs, oddPerson);
        }
    }
}