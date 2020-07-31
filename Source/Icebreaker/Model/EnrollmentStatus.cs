//----------------------------------------------------------------------------------------------
// <copyright file="EnrollmentStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Model
{
    /// <summary>
    /// User enrollment status into the team
    /// </summary>
    public enum EnrollmentStatus
    {
        /// <summary>
        /// Not joined
        /// </summary>
        NotJoined,

        /// <summary>
        /// Joined and is active
        /// </summary>
        Active,

        /// <summary>
        /// Joined and paused matches
        /// </summary>
        Paused,

        /// <summary>
        /// Left the team
        /// </summary>
        LeftTeam,
    }
}