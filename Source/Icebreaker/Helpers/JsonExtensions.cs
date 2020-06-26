//----------------------------------------------------------------------------------------------
// <copyright file="JsonExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using Newtonsoft.Json;

    /// <summary>
    /// Static extension methods for Json
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// Try parsing a Json object
        /// </summary>
        /// <typeparam name="T">object type to deserialize to</typeparam>
        /// <param name="this">string to deserialize from</param>
        /// <param name="result">deserialized object</param>
        /// <returns>whether parsing was successful</returns>
        public static bool TryParseJson<T>(this string @this, out T result)
        {
            bool success = true;
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    success = false;
                    args.ErrorContext.Handled = true;
                },
                MissingMemberHandling = MissingMemberHandling.Error
            };
            result = JsonConvert.DeserializeObject<T>(@this, settings);
            return success;
        }
    }
}