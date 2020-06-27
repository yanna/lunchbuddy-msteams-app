//----------------------------------------------------------------------------------------------
// <copyright file="ChannelAccountExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Match
{
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;

    /// <summary>
    /// Extensions for ChannelAcount
    /// </summary>
    public static class ChannelAccountExtensions
    {
        /// <summary>
        /// Gets the user AAD id
        /// </summary>
        /// <param name="this">this</param>
        /// <returns>the id</returns>
        public static string GetUserId(this Person<ChannelAccount> @this)
        {
            return @this.Data.AsTeamsChannelAccount().ObjectId;
        }
    }
}