//----------------------------------------------------------------------------------------------
// <copyright file="ChooseUserResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Result of choosing a user and team from the ChooseUserAdaptiveCard
    /// </summary>
    public class ChooseUserResult
    {
        /// <summary>
        /// Gets or sets the message id the choose user result was for
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized User string when dropdown option was used. Adaptive card doesn't treat AdaptiveChoice as an object.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string UserJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the User name when the text input option was used.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string UserNameInput { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized TeamContext string.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public TeamContext TeamContext { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the name and id are available, otherwise only the name is
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public bool HasNameAndId { get; set; }

        /// <summary>
        /// Gets the user id
        /// </summary>
        /// <returns>User id</returns>
        public Tuple<string, string> GetUserNameAndId()
        {
            var user = JsonConvert.DeserializeObject<ChooseUserAdaptiveCard.User>(this.UserJson);
            return new Tuple<string, string>(user.Name, user.AadId);
        }
    }
}