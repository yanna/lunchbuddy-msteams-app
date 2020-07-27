//----------------------------------------------------------------------------------------------
// <copyright file="EnrollmentStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Model
{
    /// <summary>
    /// User enrollment status into the program
    /// </summary>
    public enum EnrollmentStatus
    {
        /// <summary>
        /// Has not opted in yet
        /// </summary>
        NotJoined,

        /// <summary>
        /// Joined and is active
        /// </summary>
        Active,

        /// <summary>
        /// Joined and paused matches
        /// </summary>
        Inactive
    }
}