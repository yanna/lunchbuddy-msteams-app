//----------------------------------------------------------------------------------------------
// <copyright file="PersonPreferences.cs" company="Microsoft">
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
    /// Creates the preferences for each person of the group sorted by the best candidate first
    /// </summary>
    public class PersonPreferences
    {
        private readonly string userId;
        private readonly IList<Person<ChannelAccount>> group;
        private readonly IDictionary<string, Person<ChannelAccount>> userIdToPerson;
        private readonly IDictionary<string, PersonData> peopleData;

        /// <summary>
        /// Initializes a new instance of the <see cref="PersonPreferences"/> class.
        /// </summary>
        /// <param name="userId">user AAD id</param>
        /// <param name="group">group of people to order</param>
        /// <param name="userIdToPerson">dictionary of userId to person object</param>
        /// <param name="peopleData">data about the people</param>
        public PersonPreferences(
            string userId,
            IList<Person<ChannelAccount>> group,
            IDictionary<string, Person<ChannelAccount>> userIdToPerson,
            IDictionary<string, PersonData> peopleData)
        {
            this.userId = userId;
            this.group = group;
            this.userIdToPerson = userIdToPerson;
            this.peopleData = peopleData;
        }

        /// <summary>
        /// Returns the people in the group sorted by best candidate first
        /// </summary>
        /// <returns>list of people</returns>
        public List<Person<ChannelAccount>> Get()
        {
            var srcPersonData = this.GetPersonData(this.userId);

            var personScores = this.group.Select(group2Person =>
            {
                var group2PersonId = group2Person.Data.GetUserId();
                return new PersonScore
                {
                    Score = this.GetScore(this.userId, group2PersonId),
                    Person = this.userIdToPerson[group2PersonId]
                };
            });

            return personScores.OrderByDescending(personScore => personScore.Score).Select(personScore => personScore.Person).ToList();
        }

        private PersonData GetPersonData(string userId)
        {
            PersonData personData;
            return this.peopleData.TryGetValue(userId, out personData) ? personData : new PersonData();
        }

        /// <summary>
        /// Returns a score for what the srcPerson thinks about personScoreIsFor.
        /// The higher the score the more preferred the person will be.
        /// </summary>
        /// <param name="srcPersonUserId">user id of source person</param>
        /// <param name="personScoreIsForUserId">user id of the person to calculate the score for</param>
        /// <returns>an integer score</returns>
        private long GetScore(string srcPersonUserId, string personScoreIsForUserId)
        {
            var srcPersonData = this.GetPersonData(srcPersonUserId);
            var personScoreIsForData = this.GetPersonData(personScoreIsForUserId);

            // Low preferences are last resort
            var lowPreferences = srcPersonData.GetLowPreferenceNamesInLowerCase();
            if (lowPreferences.Any() && !string.IsNullOrEmpty(personScoreIsForData.Name) && lowPreferences.Contains(personScoreIsForData.Name.ToLowerInvariant()))
            {
                return long.MinValue;
            }

            // Past Matches are negative
            // They should come after the people who have never matched and
            // also sorted from oldest match to most recent (least desirable)
            var mostRecentPastMatch = srcPersonData.PastMatches.Where(match => match.UserId == personScoreIsForUserId).OrderByDescending(match => match.MatchedAt).FirstOrDefault();
            if (mostRecentPastMatch != null)
            {
                return mostRecentPastMatch.MatchedAt.Ticks * -1;
            }

            long score = 0;

            // For coronavirus times, strongly favouring similar teams so people
            // feel comfortable with their matches.
            var isInSameTeam = srcPersonData.GetTeamsInLowerCase().Intersect(personScoreIsForData.GetTeamsInLowerCase()).Any();
            if (isInSameTeam)
            {
                score += 16;
            }

            // Favour different seniority.
            // * Don't want the interns to be together to give them
            // exposure to the team during their 4 weeks here
            // * Don't want principals to be together as they probably
            // see each other a lot already
            if (!string.Equals(
                srcPersonData.Seniority,
                personScoreIsForData.Seniority,
                StringComparison.InvariantCultureIgnoreCase))
            {
                score += 10;
            }

            if (string.Equals(
                srcPersonData.Discipline,
                personScoreIsForData.Discipline,
                StringComparison.InvariantCultureIgnoreCase))
            {
                score += 6;
            }

            if (string.Equals(
                srcPersonData.Gender,
                personScoreIsForData.Gender,
                StringComparison.InvariantCultureIgnoreCase))
            {
                score += 2;
            }

            return score;
        }

        private class PersonScore
        {
            public long Score { get; set; }

            public Person<ChannelAccount> Person { get; set; }
        }
    }
}