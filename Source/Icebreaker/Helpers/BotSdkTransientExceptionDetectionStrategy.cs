//----------------------------------------------------------------------------------------------
// <copyright file="BotSdkTransientExceptionDetectionStrategy.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
    using Microsoft.Rest;

    /// <summary>
    /// Error detection strategy for specific error codes.
    /// This is the recommended solution for hitting the bot rate limit in Teams as described:
    /// https://docs.microsoft.com/en-us/microsoftteams/platform/bots/how-to/rate-limit
    /// </summary>
    public class BotSdkTransientExceptionDetectionStrategy : ITransientErrorDetectionStrategy
    {
        // List of error codes to retry on
        // 429 - Too Many Requests.
        private List<int> transientErrorStatusCodes = new List<int>() { 429 };

        /// <inheritdoc/>
        public bool IsTransient(Exception ex)
        {
            if (ex.Message.Contains("429"))
            {
                return true;
            }

            var httpOperationException = ex as HttpOperationException;
            if (httpOperationException != null)
            {
                return httpOperationException.Response != null &&
                        this.transientErrorStatusCodes.Contains((int)httpOperationException.Response.StatusCode);
            }

            return false;
        }
    }
}