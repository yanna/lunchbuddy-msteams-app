//----------------------------------------------------------------------------------------------
// <copyright file="PeopleDataCreator.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Icebreaker.Match;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Creates people data user for the stable marriage algorithm
    /// </summary>
    public class PeopleDataCreator
    {
        private readonly IcebreakerBotDataProvider dataProvider;
        private readonly List<ChannelAccount> users;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeopleDataCreator"/> class.
        /// </summary>
        /// <param name="dataProvider">data provider for the bot</param>
        /// <param name="users">list of users to get info for</param>
        public PeopleDataCreator(IcebreakerBotDataProvider dataProvider, List<ChannelAccount> users)
        {
            this.dataProvider = dataProvider;
            this.users = users;
        }

        /// <summary>
        /// Returns a dictionary of userId to person data
        /// </summary>
        /// <returns>the people data</returns>
        public async Task<IDictionary<string, PersonData>> Get()
        {
            var tasks = this.users.Select(m => this.dataProvider.GetUserInfoAsync(m.GetUserId()));
            var userInfos = await Task.WhenAll(tasks);
            var validUserInfos = userInfos.Where(userInfo => userInfo != null);

            var peopleData = validUserInfos.ToDictionary(
                userInfo => userInfo.UserId,
                userInfo => new PersonData(this.ToPastMatches(userInfo.Matches), userInfo.Discipline, userInfo.Gender, userInfo.Seniority, userInfo.Teams));

            return peopleData;
        }

        private List<PastMatch> ToPastMatches(List<UserMatch> userMatches) => userMatches.Select(m => new PastMatch(m.UserId, m.MatchedAt)).ToList();
    }
}