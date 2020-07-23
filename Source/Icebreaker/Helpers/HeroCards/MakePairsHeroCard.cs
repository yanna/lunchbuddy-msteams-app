//----------------------------------------------------------------------------------------------
// <copyright file="MakePairsHeroCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Icebreaker.Controllers;
    using Icebreaker.Match;
    using Icebreaker.Properties;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json;

    /// <summary>
    /// Make pairs hero card
    /// </summary>
    public static class MakePairsHeroCard
    {
        private const string WasMatchedSymbol = "*";

        /// <summary>
        /// Show a list of pairs and show the ability to send the pairings or generate a new pair
        /// </summary>
        /// <param name="matchResult">match result</param>
        /// <param name="teamId">team id of the match the result is for</param>
        /// <param name="teamName">team name of the match the result is for</param>
        /// <returns>attachment</returns>
        public static HeroCard GetCard(MatchResult matchResult, string teamId, string teamName)
        {
            var allPairsStr = GetPairingText(matchResult);

            var idPairs = matchResult.Pairs.Select(pair => new Tuple<string, string>(pair.Person1.Id, pair.Person2.Id)).ToList();
            var makePairsResult = new MakePairsResult()
            {
                PairChannelAccountIds = idPairs,
                TeamId = teamId
            };

            var matchCard = new HeroCard()
            {
                Title = string.Format(Resources.NewPairingsTitle, teamName),
                Text = allPairsStr,
                Buttons = new List<CardAction>()
                    {
                        new CardAction
                        {
                            Title = Resources.SendPairingsButtonText,
                            DisplayText = Resources.SendPairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.AdminNotifyPairs,
                            Value = JsonConvert.SerializeObject(makePairsResult)
                        },
                        new CardAction
                        {
                            Title = Resources.RegeneratePairingsButtonText,
                            DisplayText = Resources.RegeneratePairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.AdminMakePairs,
                            Value = JsonConvert.SerializeObject(new TeamContext { TeamId = teamId, TeamName = teamName })
                        }
                    }
            };

            return matchCard;
        }

        /// <summary>
        /// Get the text that lists all the pairings
        /// </summary>
        /// <param name="matchResult">Result of a match event</param>
        /// <returns>All pairings text</returns>
        private static string GetPairingText(MatchResult matchResult)
        {
            var pairsStrs = matchResult.Pairs.Select((pair, i) => $"{i + 1}. {pair.Person1.Name} - {pair.Person2.Name} {GetPreviouslyMatchedIndicator(pair.IsPreviouslyMatched)}").ToList();
            var allPairsStr = string.Join("<br/>", pairsStrs);

            var emptyLine = "<br/><br/>";

            if (matchResult.OddPerson != null)
            {
                allPairsStr += $"{emptyLine}<b>{Resources.NewPairingsOddPerson}</b>: {matchResult.OddPerson.Name}";
            }

            if (matchResult.HasAnyPreviouslyMatchedPair)
            {
                allPairsStr += $"{emptyLine}{WasMatchedSymbol} means has been matched before";
            }

            return Resources.NewPairingsDescription + emptyLine + allPairsStr + emptyLine;
        }

        private static string GetPreviouslyMatchedIndicator(bool wasMatchedBefore) => wasMatchedBefore ? WasMatchedSymbol : string.Empty;
    }
}