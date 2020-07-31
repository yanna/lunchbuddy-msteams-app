//----------------------------------------------------------------------------------------------
// <copyright file="ChooseTeamHeroCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.HeroCards
{
    using System.Collections.Generic;
    using Icebreaker.Controllers;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json;

    /// <summary>
    /// Choose team hero card. Presents buttons for all teams.
    /// </summary>
    public static class ChooseTeamHeroCard
    {
        public static HeroCard GetCard(string text, List<TeamContext> teams, string actionMessage)
        {
            var teamActions = new List<CardAction>();

            foreach (var team in teams)
            {
                var teamName = team.TeamName;

                var teamCardAction = new CardAction()
                {
                    Title = teamName,
                    DisplayText = teamName,
                    Type = ActionTypes.MessageBack,
                    Text = actionMessage,
                    Value = JsonConvert.SerializeObject(team)
                };
                teamActions.Add(teamCardAction);
            }

            var card = new HeroCard()
            {
                Text = text,
                Buttons = teamActions
            };

            return card;
        }
    }
}