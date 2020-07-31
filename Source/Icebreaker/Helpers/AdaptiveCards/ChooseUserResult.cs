//----------------------------------------------------------------------------------------------
// <copyright file="ChooseUserResult.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers.AdaptiveCards
{
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
        /// Gets or sets the serialized User string. Adaptive card doesn't treat it as an object.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public string UserJson { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized TeamContext string. Adaptive card doesn't treat it as an object.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public TeamContext TeamContext { get; set; }

        /// <summary>
        /// Gets the user id
        /// </summary>
        /// <returns>User id</returns>
        public string GetUserId() => JsonConvert.DeserializeObject<ChooseUserAdaptiveCard.User>(this.UserJson).AadId;

        /// <summary>
        /// Gets the user name
        /// </summary>
        /// <returns>Name</returns>
        public string GetUserName() => JsonConvert.DeserializeObject<ChooseUserAdaptiveCard.User>(this.UserJson).Name;
    }
}