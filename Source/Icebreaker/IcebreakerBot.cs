//----------------------------------------------------------------------------------------------
// <copyright file="IcebreakerBot.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Helpers;
    using Helpers.AdaptiveCards;
    using Icebreaker.Controllers;
    using Icebreaker.Helpers.HeroCards;
    using Icebreaker.Match;
    using Icebreaker.Model;
    using Icebreaker.Properties;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure;
    using Microsoft.Bot.Builder.Internals.Fibers;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

    /// <summary>
    /// Implements the core logic for Icebreaker bot
    /// </summary>
    public class IcebreakerBot
    {
        private readonly IcebreakerBotDataProvider dataProvider;
        private readonly TelemetryClient telemetryClient;
        private readonly int maxPairUpsPerTeam;
        private readonly string botDisplayName;
        private readonly string botId;
        private readonly bool isTesting;

        /// <summary>
        /// Initializes a new instance of the <see cref="IcebreakerBot"/> class.
        /// </summary>
        /// <param name="dataProvider">The data provider to use</param>
        /// <param name="telemetryClient">The telemetry client to use</param>
        public IcebreakerBot(IcebreakerBotDataProvider dataProvider, TelemetryClient telemetryClient)
        {
            this.dataProvider = dataProvider;
            this.telemetryClient = telemetryClient;
            this.maxPairUpsPerTeam = Convert.ToInt32(CloudConfigurationManager.GetSetting("MaxPairUpsPerTeam"));
            this.botDisplayName = CloudConfigurationManager.GetSetting("BotDisplayName");
            this.botId = CloudConfigurationManager.GetSetting("MicrosoftAppId");
            this.isTesting = Convert.ToBoolean(CloudConfigurationManager.GetSetting("Testing"));

            this.telemetryClient.TrackTrace($"Starting bot with IsTesting: {this.isTesting} MaxPairUpsPerTeam: {this.maxPairUpsPerTeam}");
        }

        /// <summary>
        /// Make pairing for the specified team
        /// </summary>
        /// <param name="team">team info</param>
        /// <returns>Randomized pairs</returns>
        public async Task<MatchResult> MakePairsForTeam(TeamInstallInfo team)
        {
            this.telemetryClient.TrackTrace($"Pairing members of team {team.Id}");

            var matchResult = new MatchResult();

            try
            {
                // We need to call TrustServiceUrl because this function can be
                // initiated by the Logic App and therefore does not come from a request
                // that is already authenticated by BotAuthentication
                // https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-howto-proactive-message?view=azure-bot-service-4.0&tabs=csharp#avoiding-401-unauthorized-errors
                MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);

                using (var connectorClient = new ConnectorClient(new Uri(team.ServiceUrl)))
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var optedInUsers = await this.GetOptedInUsers(connectorClient, team);
                    watch.Stop();
                    this.telemetryClient.TrackTrace($"Team {team.Id} took {watch.ElapsedMilliseconds} ms to GetOptedInUsers");

                    watch = System.Diagnostics.Stopwatch.StartNew();
                    matchResult = await this.MakePairs(optedInUsers);
                    watch.Stop();
                    this.telemetryClient.TrackTrace($"Team {team.Id} took {watch.ElapsedMilliseconds} ms to MakePairs for {optedInUsers.Count} opted in users.");

                    matchResult.Pairs.Take(this.maxPairUpsPerTeam).ToList();
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error pairing up team members: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            return matchResult;
        }

        /// <summary>
        /// Create the card attachment for showing match result
        /// </summary>
        /// <param name="matchResult">match result</param>
        /// <param name="teamId">team id of the match the result is for</param>
        /// <param name="teamName">team name of the match the result is for</param>
        /// <returns>attachment</returns>
        public Attachment CreateMatchAttachment(MatchResult matchResult, string teamId, string teamName)
        {
            var matchCard = MakePairsHeroCard.GetCard(matchResult, teamId, teamName);
            return matchCard.ToAttachment();
        }

        /// <summary>
        /// Notify the pairing to the admin user
        /// </summary>
        /// <param name="team">team info</param>
        /// <param name="matchResult">match result</param>
        /// <returns>Task</returns>
        public async Task NotifyAdminPairings(TeamInstallInfo team, MatchResult matchResult)
        {
            if (team.AdminUser == null)
            {
                this.telemetryClient.TrackTrace($"Trying to notify admin pairings for team {team.Id} but there is no admin user", SeverityLevel.Error);
                return;
            }

            this.telemetryClient.TrackTrace($"Notify admin {team.AdminUser?.UserId} for team {team.Id}");

            try
            {
                // Need to trust the service url for sending proactive messages
                MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);

                using (var connectorClient = new ConnectorClient(new Uri(team.ServiceUrl)))
                {
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var matchAttachment = this.CreateMatchAttachment(matchResult, team.TeamId, teamName);
                    var adminUser = new TeamsChannelAccount { Id = team.AdminUser?.ChannelAccountId, ObjectId = team.AdminUser?.UserId };

                    await this.NotifyUser(connectorClient, matchAttachment, adminUser, team.TenantId);
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error notifying admin userId: {team.AdminUser?.UserId} channelAccountId: {team.AdminUser?.ChannelAccountId} of pairing for team {team.Id}: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }
        }

        /// <summary>
        /// Notify the pairs of the pair up
        /// </summary>
        /// <param name="team">team info</param>
        /// <param name="pairs">member pairs</param>
        /// <returns>Count of people notified</returns>
        public async Task<int> NotifyAllPairs(TeamInstallInfo team, IList<Tuple<ChannelAccount, ChannelAccount>> pairs)
        {
            this.telemetryClient.TrackTrace($"Notify pairs for team {team.Id}");

            int usersNotifiedCount = 0;

            try
            {
                // Need to trust the service url for sending proactive messages
                MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);

                using (var connectorClient = new ConnectorClient(new Uri(team.ServiceUrl)))
                {
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);

                    var matchDate = DateTime.UtcNow;

                    // Tried to take this out of a foreach and use Select with a Task.WhenAll but this caused us
                    // to have too many requests in a short amount of time and we couldn't send the cards to the users
                    // because it hit a Teams bot limit. In our case we had about 40 users.
                    // So keep it in a foreach even though this is more "synchronous".
                    foreach (var pair in pairs)
                    {
                        usersNotifiedCount += await this.NotifyPair(connectorClient, team.TenantId, teamName, pair, matchDate);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error notifying pairs for team {team.Id}: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            var expectedUserCount = pairs.Count * 2;
            this.telemetryClient.TrackTrace($"Notify results: {usersNotifiedCount} out of {expectedUserCount} notifications sent");

            return usersNotifiedCount;
        }

        /// <summary>
        /// Return all team ids where the specified user can perform admin actions
        /// </summary>
        /// <param name="userAadId">user AAD id</param>
        /// <returns>a list of teams</returns>
        public async Task<IList<string>> GetTeamsAllowingAdminActionsByUser(string userAadId)
        {
            var userInfo = await this.dataProvider.GetUserInfoAsync(userAadId);
            return userInfo == null ? new List<string>() : userInfo.AdminForTeams;
        }

        /// <summary>
        /// Generate pairups and send pairup notifications.
        /// </summary>
        /// <returns>The number of pairups that were made</returns>
        public async Task<int> MakePairsAndNotifyForAllTeams()
        {
            this.telemetryClient.TrackTrace("Making pairups");

            // Recall all the teams where we have been added
            // For each team where bot has been added:
            //     Pull the roster of the team
            //     Remove the members who have opted out of pairups
            //     Match each member with someone else
            //     Save this pair
            // Now notify each pair found in 1:1 and ask them to reach out to the other person
            // When contacting the user in 1:1, give them the button to opt-out
            var installedTeamsCount = 0;
            var pairsNotifiedCount = 0;

            try
            {
                var teams = await this.dataProvider.GetInstalledTeamsAsync();
                installedTeamsCount = teams.Count;
                this.telemetryClient.TrackTrace($"Generating pairs for {installedTeamsCount} teams");

                foreach (var team in teams)
                {
                    var matchResult = await this.MakePairsForTeam(team);

                    if (!matchResult.Pairs.Any())
                    {
                        continue;
                    }

                    if (team.NotifyMode == TeamInstallInfo.NotifyModeNoApproval)
                    {
                        var pairs = matchResult.Pairs.Select(p => new Tuple<ChannelAccount, ChannelAccount>(p.Person1, p.Person2)).ToList();
                        pairsNotifiedCount += await this.NotifyAllPairs(team, pairs);
                    }
                    else if (team.NotifyMode == TeamInstallInfo.NotifyModeNeedApproval)
                    {
                        await this.NotifyAdminPairings(team, matchResult);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error making pairups: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            // Log telemetry about the pairups
            var properties = new Dictionary<string, string>
            {
                { "InstalledTeamsCount", installedTeamsCount.ToString() },
                { "PairsNotifiedCount", pairsNotifiedCount.ToString() }
            };
            this.telemetryClient.TrackEvent("ProcessedPairups", properties);

            return pairsNotifiedCount;
        }

        /// <summary>
        /// Returns the install team info about the team. Can return null.
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>The team that the bot has been installed to</returns>
        public Task<TeamInstallInfo> GetInstalledTeam(string teamId)
        {
            return this.dataProvider.GetInstalledTeamAsync(teamId);
        }

        /// <summary>
        /// Send a welcome message to the user.
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="userChannelAccount">The ChannelAccount of the user</param>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="teamContext">The team the welcome message is for.</param>
        /// <param name="userStatus">User's status in the team</param>
        /// <param name="botInstallerName">Who installed the app in the team. Can be empty.</param>
        /// <param name="isAdminUser">Is the user admin for the team</param>
        /// <returns>Tracking task</returns>
        public async Task WelcomeUser(ConnectorClient connectorClient, ChannelAccount userChannelAccount, string tenantId, TeamContext teamContext, EnrollmentStatus userStatus, string botInstallerName, bool isAdminUser)
        {
            this.telemetryClient.TrackTrace($"Sending welcome message for user {userChannelAccount.GetUserId()}");

            var welcomeMessageCard = WelcomeNewMemberAdaptiveCard.GetCard(teamContext, userStatus, botInstallerName, isAdminUser);

            await this.NotifyUser(connectorClient, AdaptiveCardHelper.CreateAdaptiveCardAttachment(welcomeMessageCard), userChannelAccount, tenantId);
        }

        /// <summary>
        /// Notify a user of failure to install the bot to the team
        /// </summary>
        /// <param name="connectorClient">Connector client</param>
        /// <param name="userChannelAccount">User account to message</param>
        /// <param name="tenantId">User tenant id</param>
        /// <returns>Task</returns>
        public async Task SendFailedToInstall(ConnectorClient connectorClient, ChannelAccount userChannelAccount, string tenantId)
        {
            var installFailedCard = new HeroCard()
            {
                Text = "Failed to install LunchBuddy"
            };

            await this.NotifyUser(connectorClient, installFailedCard.ToAttachment(), userChannelAccount, tenantId);
        }

        /// <summary>
        /// Sends a welcome message to the General channel of the team that this bot has been installed to
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">Activity to reply to the original message</param>
        /// <param name="teamId">The id of the team that the bot is installed to</param>
        /// <param name="botChannelAccountId">The id of the bot so we can create a deeplink to chat with it</param>
        /// <param name="botInstaller">The installer of the application</param>
        /// <returns>Tracking task</returns>
        public async Task WelcomeTeam(ConnectorClient connectorClient, Activity replyActivity, string teamId, string botChannelAccountId, string botInstaller)
        {
            this.telemetryClient.TrackTrace($"Sending welcome message for team {teamId}");

            var teamName = await this.GetTeamNameAsync(connectorClient, teamId);
            var welcomeTeamMessageCard = WelcomeTeamAdaptiveCard.GetCardJson(teamName, teamId, botChannelAccountId, botInstaller);
            var wasSuccessful = await this.NotifyTeam(connectorClient, welcomeTeamMessageCard, teamId);
            replyActivity.Text = wasSuccessful ? string.Format(Resources.SendWelcomeCardSuccess, teamName) : string.Format(Resources.SendWelcomeCardFail, teamName);
            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Sends unrecognized message that occured in a one on one chat. See SendUnrecognizedChannelMessage for unknown message in channel.
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">The activity for replying to a message</param>
        /// <param name="actionsTeamContext">Team info for the context</param>
        /// <param name="userStatusForTeam">User status for the team</param>
        /// <param name="isUserAdminOfTeam">Whether user is admin of the team</param>
        /// <returns>Tracking task</returns>
        public async Task SendUnrecognizedOneOnOneMessage(
            ConnectorClient connectorClient,
            Activity replyActivity,
            TeamContext actionsTeamContext,
            EnrollmentStatus userStatusForTeam,
            bool isUserAdminOfTeam)
        {
            var unrecognizedInputAdaptiveCard = UnrecognizedInputAdaptiveCard.GetCardJson(actionsTeamContext, userStatusForTeam, showAdminActions: isUserAdminOfTeam);
            replyActivity.Attachments = new List<Attachment>()
            {
                AdaptiveCardHelper.CreateAdaptiveCardAttachment(unrecognizedInputAdaptiveCard)
            };
            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Send unrecognized message that occurred in a team channel. See SendUnrecognizedOneOnOneMessage for unknown message in a one on one chat
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="activity">original activity</param>
        /// <param name="teamId">team id of the channel</param>
        /// <returns>Unrecognized message in a channel card</returns>
        public Task SendUnrecognizedChannelMessage(ConnectorClient connectorClient, Activity activity, string teamId)
        {
            var card = UnrecognizedInputInChannelAdaptiveCard.GetCard(activity.Recipient.Id, teamId);
            return ActivityHelper.ReplyWithAdaptiveCard(connectorClient, activity, card);
        }

        /// <summary>
        /// Sends a message to edit the user profile
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">Activity for replying</param>
        /// <param name="tenantId">Tenant id of the user</param>
        /// <param name="userAndTeam">User and team info. Team info is for the subteam names hint</param>
        /// <param name="isOnBehalfOfAnotherUser">whether this card is displayed for a user that's not the sender of the activity</param>
        /// <returns>Empty task</returns>
        public async Task EditUserProfile(ConnectorClient connectorClient, Activity replyActivity, string tenantId, UserAndTeam userAndTeam, bool isOnBehalfOfAnotherUser)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAndTeam.User.UserAadId);
            var subteamsHint = await this.GetSubteamNamesHintForUser(userAndTeam.Team.TeamId);

            var card = EditUserProfileAdaptiveCard.GetCardJson(
                userAndTeam.User.UserAadId,
                userAndTeam.User.UserName,
                userAndTeam.Team.TeamName,
                userInfo.Discipline,
                userInfo.Gender,
                userInfo.Seniority,
                userInfo.Subteams,
                subteamsHint,
                userInfo.LowPreferences,
                isOnBehalfOfAnotherUser);
            replyActivity.Attachments = new List<Attachment>()
            {
                AdaptiveCardHelper.CreateAdaptiveCardAttachment(card)
            };
            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Saves the user profile to the database
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="activity">Activity of the user data submission</param>
        /// <param name="tenantId">Tenant id of the user</param>
        /// <param name="userAadId">AAD id of the user</param>
        /// <param name="discipline">Discipline of the user to store in the database. Can be empty string.</param>
        /// <param name="gender">Gender of the user to store in the database. Can be empty string.</param>
        /// <param name="seniority">Seniority of the user to store in the database. Can be empty string.</param>
        /// <param name="subteams">Subteam names of the user to store in the database. Can be empty list.</param>
        /// <param name="lowPreferenceNames">Full names of low preference matches. Can be empty list.</param>
        /// <returns>Empty task</returns>
        public async Task SaveUserProfile(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string userAadId,
            string discipline,
            string gender,
            string seniority,
            List<string> subteams,
            List<string> lowPreferenceNames)
        {
            var isSuccess = await this.SaveUserInfo(tenantId, userAadId, discipline, gender, seniority, subteams, lowPreferenceNames, userStatus: null);

            // After you do the card submission, the card resets to the old values even though the new values are saved.
            // This is just the default behaviour of Adaptive Cards.
            // So we can do this a couple of ways:
            // 1) manually update the message to update the card to the new values but this requires storing the original activity id
            //    because it's not the submit activity but the activity that triggered the submit activity.
            // 2) let it happen and reply with a readonly card representing the new state.
            // Picking option 2 because I want to avoid storing extra state.
            var replyActivity = activity.CreateReply();
            if (isSuccess)
            {
                replyActivity.Attachments = new List<Attachment>
                {
                    AdaptiveCardHelper.CreateAdaptiveCardAttachment(EditUserProfileAdaptiveCard.GetResultCard(
                        this.GetUITextForProfileData(discipline),
                        this.GetUITextForProfileData(gender),
                        this.GetUITextForProfileData(seniority),
                        subteams,
                        lowPreferenceNames))
                };
            }
            else
            {
                replyActivity.Text = Resources.SaveProfileFailText;
            }

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Saves the team settings to the database
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="activity">Activity of the user data submission</param>
        /// <param name="tenantId">tenant id</param>
        /// <param name="teamId">Team id</param>
        /// <param name="adminUserId">Admin user id. Can be empty.</param>
        /// <param name="notifyMode">How the pairs will be notified</param>
        /// <param name="subteamNames">Subteam names to show in EditProfile page</param>
        /// <returns>Empty task</returns>
        public async Task SaveTeamSettings(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string teamId,
            string adminUserId,
            string notifyMode,
            string subteamNames)
        {
            var team = await this.GetInstalledTeam(teamId);
            var oldAdminUserId = team.AdminUser?.UserId;

            team.NotifyMode = notifyMode;
            team.SubteamNames = subteamNames;
            team.AdminUser = null;

            var adminUserName = string.Empty;
            var newAdminUserId = string.Empty;
            if (!string.IsNullOrEmpty(adminUserId))
            {
                var adminChannelAccount = await this.GetChannelAccountByAadId(adminUserId, connectorClient, teamId);
                if (adminChannelAccount != null)
                {
                    newAdminUserId = adminChannelAccount.GetUserId();

                    team.AdminUser = new TeamInstallInfo.User { ChannelAccountId = adminChannelAccount.Id, UserId = newAdminUserId };
                    adminUserName = adminChannelAccount.Name;
                }
            }

            var isSuccess = await this.dataProvider.UpdateTeamInstallInfoAsync(team);

            if (!string.IsNullOrEmpty(oldAdminUserId) && oldAdminUserId != newAdminUserId)
            {
                isSuccess = isSuccess && await this.RemoveTeamUserIsAdminFor(teamId, oldAdminUserId);
            }

            if (!string.IsNullOrEmpty(newAdminUserId) && oldAdminUserId != newAdminUserId)
            {
                isSuccess = isSuccess && await this.AddNewTeamUserIsAdminFor(tenantId, teamId, newAdminUserId);
            }

            // After you do the card submission, the card resets to the old values even though the new values are saved.
            // This is just the default behaviour of Adaptive Cards.
            // So we can do this a couple of ways:
            // 1) manually update the message to update the card to the new values but this requires storing the original activity id
            //    because it's not the submit activity but the activity that triggered the submit activity.
            // 2) let it happen and reply with a readonly card representing the new state.
            // Picking option 2 because I want to avoid storing extra state.
            var replyActivity = activity.CreateReply();
            if (isSuccess)
            {
                replyActivity.Attachments = new List<Attachment>
                {
                    AdaptiveCardHelper.CreateAdaptiveCardAttachment(EditTeamSettingsAdaptiveCard.GetResultCard(
                        adminUserName, notifyMode, subteamNames))
                };
            }
            else
            {
                replyActivity.Text = Resources.SaveProfileFailText;
            }

            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Save information about the team to which the bot was added.
        /// </summary>
        /// <param name="serviceUrl">The service url</param>
        /// <param name="teamId">The team id</param>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="botInstallerUserName">Name of the person that added the bot to the team. Can be empty</param>
        /// <param name="adminUserAadId">User AAD id of the bot admin for this team. Can be empty</param>
        /// <param name="adminUserChannelAccountId">User ChannelAccount id of the bot admin for this team. Can be empty</param>
        /// <returns>Tracking task</returns>
        public async Task<bool> SaveAddedBotToTeam(string serviceUrl, string teamId, string tenantId, string botInstallerUserName, string adminUserAadId, string adminUserChannelAccountId)
        {
            var adminUser = string.IsNullOrEmpty(adminUserAadId) ? null : new TeamInstallInfo.User { UserId = adminUserAadId, ChannelAccountId = adminUserChannelAccountId };
            var newTeamInstallInfo = new TeamInstallInfo
            {
                ServiceUrl = serviceUrl,
                TeamId = teamId,
                TenantId = tenantId,
                InstallerName = botInstallerUserName,
                AdminUser = adminUser,
                NotifyMode = TeamInstallInfo.NotifyModeNoApproval
            };

            // Preserve old settings if we find them. This may happen when we have a debug and live app at the same time.
            var alreadyInstalledTeamInfo = await this.dataProvider.GetInstalledTeamAsync(teamId);
            if (alreadyInstalledTeamInfo != null)
            {
                newTeamInstallInfo.NotifyMode = alreadyInstalledTeamInfo.NotifyMode;
                newTeamInstallInfo.SubteamNames = alreadyInstalledTeamInfo.SubteamNames;
            }

            var isSuccess = await this.dataProvider.UpdateTeamInstallInfoAsync(newTeamInstallInfo);

            // Create admin user so we can add the adminForTeams attribute.
            if (isSuccess && !string.IsNullOrEmpty(adminUserAadId))
            {
                isSuccess = isSuccess && await this.AddNewTeamUserIsAdminFor(tenantId, teamId, adminUserAadId);
            }

            return isSuccess;
        }

        /// <summary>
        /// Save information about the team from which the bot was removed.
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>Tracking task</returns>
        public async Task SaveRemoveBotFromTeam(string teamId)
        {
            var teamInfo = await this.dataProvider.GetInstalledTeamAsync(teamId);

            bool succeeded = await this.dataProvider.RemoveTeamInstallInfoAsync(teamId);

            if (succeeded && teamInfo != null && teamInfo.AdminUser != null)
            {
                await this.RemoveTeamUserIsAdminFor(teamId, teamInfo.AdminUser?.UserId);
            }

            // Also considered do we need to update all users for this team
            // and update their status for the team?
            // Decided against it because if I uninstalled this bot and reinstalled
            // it to upgrade to a newer version, I don't really want the user enrollment
            // to all switch back to not joined.
        }

        /// <summary>
        /// Log user added to team
        /// </summary>
        /// <param name="userAadId">user AAD id</param>
        /// <param name="teamId">team id the user was removed from</param>
        /// <returns>Task</returns>
        public async Task SaveAddedUserToTeam(string userAadId, string teamId)
        {
            this.telemetryClient.TrackTrace($"New member {userAadId} added to team {teamId}");

            var userInfo = await this.dataProvider.GetUserInfoAsync(userAadId);
            if (userInfo == null)
            {
                return;
            }

            var hadStatusForTeamId = userInfo.SetStatusInTeam(EnrollmentStatus.NotJoined, teamId);
            if (!hadStatusForTeamId)
            {
                // A status of NotJoined is assumed if we have no info for it so
                // we don't need to update the database if we didn't replace any previous status.
                return;
            }

            await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Log user left the team
        /// </summary>
        /// <param name="userAadId">user AAD id</param>
        /// <param name="teamId">team id the user was removed from</param>
        /// <returns>Task</returns>
        public async Task SaveRemovedUserFromTeam(string userAadId, string teamId)
        {
            this.telemetryClient.TrackTrace($"Member {userAadId} left the team {teamId}");

            var userInfo = await this.dataProvider.GetUserInfoAsync(userAadId);
            if (userInfo == null)
            {
                return;
            }

            userInfo.SetStatusInTeam(EnrollmentStatus.LeftTeam, teamId);
            await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Opt out the user from further pairups
        /// </summary>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="userAadId">The user AAD id</param>
        /// <param name="teamId">Team id the action is for</param>
        /// <returns>Whether the opt out was successful</returns>
        public async Task<bool> OptOutUser(string tenantId, string userAadId, string teamId)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.SetStatusInTeam(EnrollmentStatus.Paused, teamId);
            return await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Opt in the user to pairups
        /// </summary>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="userAadId">The user AAD id</param>
        /// <param name="teamId">Team id the action is for</param>
        /// <returns>Whether the opt in was successful</returns>
        public async Task<bool> OptInUser(string tenantId, string userAadId, string teamId)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.SetStatusInTeam(EnrollmentStatus.Active, teamId);
            return await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Get the name of a team.
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="teamId">The team id</param>
        /// <returns>The name of the team</returns>
        public async Task<string> GetTeamNameAsync(ConnectorClient connectorClient, string teamId)
        {
            var teamsConnectorClient = connectorClient.GetTeamsConnectorClient();
            var teamDetailsResult = await teamsConnectorClient.Teams.FetchTeamDetailsAsync(teamId);
            return teamDetailsResult.Name;
        }

        /// <summary>
        /// Get all installed team contexts
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <returns>List of team contexts</returns>
        public async Task<List<TeamContext>> GetAllTeams(ConnectorClient connectorClient)
        {
            var installedTeams = await this.dataProvider.GetInstalledTeamsAsync();
            var teamContexts = new List<TeamContext>();

            foreach (var installedTeam in installedTeams)
            {
                try
                {
                    var teamName = await this.GetTeamNameAsync(connectorClient, installedTeam.TeamId);
                    var teamContext = new TeamContext { TeamId = installedTeam.TeamId, TeamName = teamName };
                    teamContexts.Add(teamContext);
                }
                catch (Exception ex)
                {
                    // This may happen if the bot we're using is not installed in the other teams
                    this.telemetryClient.TrackTrace($"Could not get the team name for {installedTeam.TeamId}: {ex.Message}", SeverityLevel.Warning);
                    this.telemetryClient.TrackException(ex);
                }
            }

            return teamContexts;
        }

        /// <summary>
        /// Get team context objects by team ids
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="teamIds">team ids</param>
        /// <returns>teamcontext objects</returns>
        public async Task<List<TeamContext>> GetTeamContextsByIds(ConnectorClient connectorClient, IList<string> teamIds)
        {
            var teamNameTasks = teamIds.Select(teamId => this.GetTeamNameAsync(connectorClient, teamId));
            var teamNames = await Task.WhenAll(teamNameTasks);
            var teamContexts = teamIds.Zip(teamNames, (teamId, teamName) => new TeamContext { TeamId = teamId, TeamName = teamName }).ToList();
            return teamContexts;
        }

        /// <summary>
        /// Change whether the pairing will need admin approval before it is sent.
        /// </summary>
        /// <param name="needApproval">whether approval is needed</param>
        /// <param name="team">team document</param>
        /// <returns>success or failure</returns>
        public async Task<bool> ChangeTeamNotifyPairsMode(bool needApproval, TeamInstallInfo team)
        {
            var newApprovalMode = needApproval ? TeamInstallInfo.NotifyModeNeedApproval : TeamInstallInfo.NotifyModeNoApproval;
            if (team.NotifyMode == newApprovalMode)
            {
                return true;
            }

            var teamToUpdate = team.CloneJson<TeamInstallInfo>();
            teamToUpdate.NotifyMode = newApprovalMode;

            return await this.dataProvider.UpdateTeamInstallInfoAsync(teamToUpdate);
        }

        /// <summary>
        /// Edit team settings
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">Activity for replying</param>
        /// <param name="teamId">Team id of team to modify</param>
        /// <param name="teamName">Team name of the team to modify</param>
        /// <returns>Empty task</returns>
        public async Task EditTeamSettings(ConnectorClient connectorClient, Activity replyActivity, string teamId, string teamName)
        {
            var teamInfo = await this.GetInstalledTeam(teamId);

            var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);

            EditTeamSettingsAdaptiveCard.User adminUser = null;
            if (teamInfo.AdminUser != null)
            {
                var adminUserChannelAccount = allMembers.FirstOrDefault(account => account.Id == teamInfo.AdminUser.ChannelAccountId);
                if (adminUserChannelAccount != null)
                {
                    adminUser = new EditTeamSettingsAdaptiveCard.User { AadId = adminUserChannelAccount?.GetUserId(), Name = adminUserChannelAccount?.Name };
                }
            }

            var card = EditTeamSettingsAdaptiveCard.GetCard(teamId, teamName, adminUser, teamInfo.NotifyMode, teamInfo.SubteamNames);
            replyActivity.Attachments = new List<Attachment>()
            {
                AdaptiveCardHelper.CreateAdaptiveCardAttachment(card)
            };
            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Returns the user info if it exists, or a UserInfo object that is not persisted in the store yet.
        /// </summary>
        /// <param name="tenantId">tenant id</param>
        /// <param name="userAadId">user AAD id</param>
        /// <returns>UserInfo</returns>
        public async Task<UserInfo> GetOrCreateUnpersistedUserInfo(string tenantId, string userAadId)
        {
            var userInfo = await this.dataProvider.GetUserInfoAsync(userAadId);
            return userInfo ?? new UserInfo { TenantId = tenantId, UserId = userAadId };
        }

        private static bool IsUnknownUser(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            return name.ToLowerInvariant().Contains(Resources.UnknownUserName);
        }

        private async Task<bool> RemoveTeamUserIsAdminFor(string teamIdToRemove, string userId)
        {
            bool isSuccess = true;
            var userInfo = await this.dataProvider.GetUserInfoAsync(userId);
            if (userInfo == null)
            {
                return true;
            }

            var numRemoved = userInfo.AdminForTeams.RemoveAll(id => id == teamIdToRemove);
            if (numRemoved > 0)
            {
                isSuccess = isSuccess && await this.dataProvider.SetUserInfoAsync(userInfo);
            }

            return isSuccess;
        }

        private async Task<bool> AddNewTeamUserIsAdminFor(string tenantId, string newTeamId, string userId)
        {
            bool isSuccess = true;
            var newAdminUserInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userId);
            if (!newAdminUserInfo.AdminForTeams.Any(id => id == newTeamId))
            {
                newAdminUserInfo.AdminForTeams.Add(newTeamId);
                isSuccess = isSuccess && await this.dataProvider.SetUserInfoAsync(newAdminUserInfo);
            }

            return isSuccess;
        }

        private async Task<bool> SaveUserInfo(
           string tenantId,
           string userAadId,
           string discipline,
           string gender,
           string seniority,
           List<string> subteams,
           List<string> lowPreferenceNames,
           UserEnrollmentStatus userStatus)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.Discipline = discipline;
            userInfo.Gender = gender;
            userInfo.Seniority = seniority;
            userInfo.Subteams = subteams;
            userInfo.LowPreferences = lowPreferenceNames;
            if (userStatus != null)
            {
                userInfo.SetStatusInTeam(userStatus.Status, userStatus.TeamId);
            }

            var isSuccess = await this.dataProvider.SetUserInfoAsync(userInfo);
            return isSuccess;
        }

        private async Task<ChannelAccount> GetChannelAccountByAadId(string aadId, ConnectorClient connectorClient, string teamId)
        {
            var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
            return allMembers.FirstOrDefault(m => m.GetUserId() == aadId);
        }

        /// <summary>
        /// Get the UI text of the value stored in the database.
        /// </summary>
        /// <param name="dbValue">Usually lowercase without spaces version of the UI value</param>
        /// <returns>UI text</returns>
        private string GetUITextForProfileData(string dbValue)
        {
            // TODO: Should probably do a proper mapping but just uppercase for now.
            return string.IsNullOrEmpty(dbValue) ? dbValue : dbValue[0].ToString().ToUpperInvariant() + dbValue.Substring(1);
        }

        /// <summary>
        /// Notify a pairup.
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="teamName">The team name</param>
        /// <param name="pair">The pairup</param>
        /// <param name="matchDate">The date the match occurred</param>
        /// <returns>Number of users notified successfully</returns>
        private async Task<int> NotifyPair(ConnectorClient connectorClient, string tenantId, string teamName, Tuple<ChannelAccount, ChannelAccount> pair, DateTime matchDate)
        {
            var teamsPerson1 = pair.Item1.AsTeamsChannelAccount();
            var teamsPerson2 = pair.Item2.AsTeamsChannelAccount();

            this.telemetryClient.TrackTrace($"Sending pairup notification to {teamsPerson1.GetUserId()} and {teamsPerson2.GetUserId()}");

            // Fill in person2's info in the card for person1
            var cardForPerson1 = PairUpNotificationAdaptiveCard.GetCardJson(teamName, teamsPerson1, teamsPerson2, this.botDisplayName);

            // Fill in person1's info in the card for person2
            var cardForPerson2 = PairUpNotificationAdaptiveCard.GetCardJson(teamName, teamsPerson2, teamsPerson1, this.botDisplayName);

            // Send notifications and return the number that was successful
            var notifyResults = await Task.WhenAll(
                this.NotifyUserAndSavePastMatch(connectorClient, AdaptiveCardHelper.CreateAdaptiveCardAttachment(cardForPerson1), teamsPerson1, teamsPerson2.GetUserId(), tenantId, matchDate),
                this.NotifyUserAndSavePastMatch(connectorClient, AdaptiveCardHelper.CreateAdaptiveCardAttachment(cardForPerson2), teamsPerson2, teamsPerson1.GetUserId(), tenantId, matchDate));

            var successfulNotifyCount = notifyResults.Count(wasNotified => wasNotified);
            return successfulNotifyCount;
        }

        private async Task<bool> NotifyUserAndSavePastMatch(ConnectorClient connectorClient, Attachment cardToSend, ChannelAccount user, string matchedUserId, string tenantId, DateTime matchDate)
        {
            bool notifiedUser = await this.NotifyUser(connectorClient, cardToSend, user, tenantId);
            if (notifiedUser)
            {
                await this.SavePastMatch(tenantId, user.GetUserId(), matchedUserId, matchDate);
            }

            return notifiedUser;
        }

        private RetryPolicy CreateRetryPolicy()
        {
            // When NotifyUser is called to notify pairups we hit a Teams bot rate limit
            // The scenario was after 9 seconds with 12 NotifyUser calls (with max 2 in each sec), we got "too many requests" exceptions.
            //
            // Cannot tell which limit we're hitting as the most conservative limit on the page below for
            // Create/Send Conversation is 2 secs => Max 8, 30 secs => Max 60
            // but it does say they are subject to change. In any case use a retry policy to do the requests.
            // https://docs.microsoft.com/en-us/microsoftteams/platform/bots/how-to/rate-limit
            //
            // deltaBackoff is used to add a randomized  +/- 20% delta to avoid numerous clients retrying simultaneously.
            var exponentialBackoffRetryStrategy = new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(1));
            var retryPolicy = new RetryPolicy(new BotSdkTransientExceptionDetectionStrategy(), exponentialBackoffRetryStrategy);
            return retryPolicy;
        }

        private async Task<bool> NotifyUser(ConnectorClient connectorClient, Attachment cardToSend, ChannelAccount user, string tenantId)
        {
            var userId = user.GetUserId();
            this.telemetryClient.TrackTrace($"Sending notification to user {userId}");

            var retryPolicy = this.CreateRetryPolicy();
            try
            {
                // ensure conversation exists
                var bot = new ChannelAccount { Id = this.botId };
                var response = retryPolicy.ExecuteAction<ConversationResourceResponse>(() => connectorClient.Conversations.CreateOrGetDirectConversation(bot, user, tenantId));
                this.telemetryClient.TrackTrace($"Received conversation {response.Id} for {userId}");

                // construct the activity we want to post
                var activity = new Activity()
                {
                    Type = ActivityTypes.Message,
                    Conversation = new ConversationAccount()
                    {
                        Id = response.Id,
                    },
                    Attachments = new List<Attachment>()
                    {
                        cardToSend
                    }
                };

                if (!this.isTesting)
                {
                    await retryPolicy.ExecuteAsync(() => connectorClient.Conversations.SendToConversationAsync(activity));
                    this.telemetryClient.TrackTrace($"Sent notification to user {userId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error sending notification to user: {ex.Message}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex);
                return false;
            }
        }

        private async Task<bool> SavePastMatch(string tenantId, string userAadId, string matchedUserAadId, DateTime matchDate)
        {
            this.telemetryClient.TrackTrace($"Save new match {matchedUserAadId} for {userAadId}");

            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.Matches.Insert(0, new UserMatch { UserId = matchedUserAadId, MatchDateUtc = matchDate });
            return await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Method that will send out the message in the General channel of the team
        /// that this bot has been installed to
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="cardToSend">The actual welcome card (for the team)</param>
        /// <param name="teamId">The team id</param>
        /// <returns>A tracking task</returns>
        private async Task<bool> NotifyTeam(ConnectorClient connectorClient, string cardToSend, string teamId)
        {
            this.telemetryClient.TrackTrace($"Sending notification to team {teamId}");
            bool isSuccessful = false;

            try
            {
                var activity = new Activity()
                {
                    Type = ActivityTypes.Message,
                    Conversation = new ConversationAccount()
                    {
                        Id = teamId
                    },
                    Attachments = new List<Attachment>()
                    {
                        AdaptiveCardHelper.CreateAdaptiveCardAttachment(cardToSend)
                    }
                };

                await connectorClient.Conversations.SendToConversationAsync(activity);
                isSuccessful = true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error sending notification to team: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            return isSuccessful;
        }

        private async Task<List<ChannelAccount>> GetOptedInUsers(ConnectorClient connectorClient, TeamInstallInfo teamInfo)
        {
            var teamId = teamInfo.TeamId;

            // Pull the roster of specified team and only keep those who have opted in explicitly
            var members = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
            this.telemetryClient.TrackTrace($"There are {members.Count} total in team {teamId}");

            // Sometimes I see "Unknown User" in a team.
            // I suspect these are people who have their AAD account disabled eg when someone leaves the company.
            // Going to add a check for it to remove them but unsure if GetConversationMembersAsync actually includes them.
            var memberByUserId = members.Where(m => !IsUnknownUser(m.Name)).ToDictionary(m => m.GetUserId(), m => m);

            var activeUserIds = await this.dataProvider.GetActiveUserIdsForTeam(teamId);
            this.telemetryClient.TrackTrace($"There are {activeUserIds.Count} active users in team {teamId}");

            return activeUserIds.Where(userId => memberByUserId.ContainsKey(userId)).Select(userId => memberByUserId[userId]).ToList();
        }

        private async Task<MatchResult> MakePairs(List<ChannelAccount> users)
        {
            if (users.Count > 1)
            {
                this.telemetryClient.TrackTrace($"Making {users.Count / 2} pairs among {users.Count} users");
            }
            else
            {
                this.telemetryClient.TrackTrace($"Pairs could not be made because there is only 1 user in the team");
            }

            var userCount = users.Count;
            if (userCount == 0)
            {
                return new MatchResult();
            }

            if (userCount == 1)
            {
                return new MatchResult(new List<MatchResult.MatchPair>(), users.First());
            }

            var randomSeed = Guid.NewGuid().GetHashCode();
            this.telemetryClient.TrackTrace($"Random seed is {randomSeed}");
            Random random = new Random(randomSeed);

            var useStableMarriageAlgorithmUserCount = 4;

            if (userCount < useStableMarriageAlgorithmUserCount)
            {
                var randomMatchCreator = new RandomMatchCreator(random);
                return randomMatchCreator.CreateMatches(users);
            }
            else
            {
                var peopleData = await new PeopleDataCreator(this.dataProvider, users).Get();
                return new StableMarriageMatchCreator(random, peopleData, numRetryOnPreviouslyMatchedPair: 3).CreateMatches(users);
            }
        }

        private async Task<string> GetSubteamNamesIfUserExistsInTeam(ConnectorClient connectorClient, TeamInstallInfo team, string userAadId)
        {
            var members = await connectorClient.Conversations.GetConversationMembersAsync(team.Id);
            var foundUser = members.FirstOrDefault(member => member.GetUserId() == userAadId);
            return foundUser == null ? string.Empty : team.SubteamNames;
        }

        private async Task<string> GetSubteamNamesHintForUser(string teamId)
        {
            var team = await this.dataProvider.GetInstalledTeamAsync(teamId);
            var subteams = new List<string>();
            if (team != null)
            {
                subteams.Add(team.SubteamNames);
            }

            var subteamsSanitized = subteams.Where(names => !string.IsNullOrEmpty(names)).Select(names => names.Trim());
            var subteamsHint = string.Join(", ", subteamsSanitized);
            return subteamsHint;
        }
    }
}