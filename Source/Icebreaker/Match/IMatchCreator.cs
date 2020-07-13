//----------------------------------------------------------------------------------------------
// <copyright file="IMatchCreator.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using System.Collections.Generic;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Interface for matching a pair of users
    /// </summary>
    public interface IMatchCreator
    {
        /// <summary>
        /// Create matches for the provided users
        /// </summary>
        /// <param name="users">set of users</param>
        /// <returns>pairs and optional odd person</returns>
        MatchResult CreateMatches(List<ChannelAccount> users);
    }
}