//----------------------------------------------------------------------------------------------
// <copyright file="MessageIds.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Controllers
{
    /// <summary>
    /// The accepted messages for the bot
    /// </summary>
    public static class MessageIds
    {
        /// <summary>
        /// Opt in to being paired up with someone
        /// </summary>
        public const string OptIn = "optin";

        /// <summary>
        /// Opt out to being paired up with someone
        /// </summary>
        public const string OptOut = "optout";

        /// <summary>
        /// Edit user profile
        /// </summary>
        public const string EditProfile = "editprofile";

        /// <summary>
        /// Generate pairs from opted in users in the team
        /// </summary>
        public const string AdminMakePairs = "makepairs";

        /// <summary>
        /// Notify each person in the pairing of who they are paired up with
        /// </summary>
        public const string AdminNotifyPairs = "notifypairs";

        /// <summary>
        /// The timer triggered pairing will send the pairings immediately after generating them.
        /// </summary>
        public const string AdminChangeNotifyModeNoApproval = "notifynoapproval";

        /// <summary>
        /// The timer triggered pairing will send the pairings to the admin for approval.
        /// </summary>
        public const string AdminChangeNotifyModeNeedApproval = "notifyneedapproval";

        /// <summary>
        /// Edit team settings
        /// </summary>
        public const string AdminEditTeamSettings = "editteamsettings";

        /// <summary>
        /// Debug the notify user card
        /// </summary>
        public const string DebugNotifyUser = "notifyme";

        /// <summary>
        /// Debug the welcome card
        /// </summary>
        public const string DebugWelcomeUser = "welcomeme";
    }
}