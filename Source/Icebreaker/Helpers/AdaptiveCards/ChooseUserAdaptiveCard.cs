//----------------------------------------------------------------------------------------------
// <copyright file="ChooseUserAdaptiveCard.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System.Collections.Generic;
    using System.Linq;
    using global::AdaptiveCards;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Adaptive card to choose a user
    /// </summary>
    public static class ChooseUserAdaptiveCard
    {
        /// <summary>
        /// Returns the adaptive card for choosing a user
        /// </summary>
        /// <param name="users">List of users to display. If it's empty, show a text input</param>
        /// <param name="teamContext">Team the list of users is from</param>
        /// <param name="messageId">Message id the user picking is for</param>
        /// <returns>Card for choosing a user</returns>
        public static AdaptiveCard GetCard(List<User> users, TeamContext teamContext, string messageId)
        {
            var isDropdownVisible = users.Count > 0;

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 2))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock()
                    {
                        Text = "User Name",
                        Size = AdaptiveTextSize.Large,
                        Wrap = true,
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveChoiceSetInput()
                    {
                        Id = "UserJson",
                        Style = AdaptiveChoiceInputStyle.Compact,
                        Choices = users.Select(user => new AdaptiveChoice
                        {
                            Title = user.Name,
                            Value = JsonConvert.SerializeObject(user)
                        }).ToList(),
                        IsVisible = isDropdownVisible
                    },
                    new AdaptiveTextInput()
                    {
                        Id = "UserNameInput",
                        IsVisible = !isDropdownVisible
                    }
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveSubmitAction
                    {
                        Title = "Submit",
                        Data = JObject.FromObject(new { MessageId = messageId, TeamContext = teamContext, HasNameAndId = isDropdownVisible })
                    }
                }
            };

            return card;
        }

        /// <summary>
        /// User class to specify the AAD id and display name
        /// </summary>
        public class User
        {
            /// <summary>
            /// Gets or sets the AAD id
            /// </summary>
            public string AadId { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the Display name
            /// </summary>
            public string Name { get; set; } = string.Empty;
        }
    }
}