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
    using Icebreaker.Match;
    using Icebreaker.Properties;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Newtonsoft.Json;

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
                using (var connectorClient = new ConnectorClient(new Uri(team.ServiceUrl)))
                {
                    var optedInUsers = await this.GetOptedInUsers(connectorClient, team);

                    this.telemetryClient.TrackTrace($"Team {team.Id} has {optedInUsers.Count} opted in users.");

                    matchResult = await this.MakePairs(optedInUsers);
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
        /// Notify the pairs of the pair up
        /// </summary>
        /// <param name="team">team info</param>
        /// <param name="pairs">member pairs</param>
        /// <returns>Count of pairs notified</returns>
        public async Task<int> NotifyAllPairs(TeamInstallInfo team, IList<Tuple<ChannelAccount, ChannelAccount>> pairs)
        {
            this.telemetryClient.TrackTrace($"Notify pairs for team {team.Id}");

            int usersNotifiedCount = 0;
            int pairsNotifiedCount = 0;

            try
            {
                // Need to trust the service url for sending proactive messages
                MicrosoftAppCredentials.TrustServiceUrl(team.ServiceUrl);

                using (var connectorClient = new ConnectorClient(new Uri(team.ServiceUrl)))
                {
                    var teamName = await this.GetTeamNameAsync(connectorClient, team.TeamId);
 
                    var matchDate = DateTime.UtcNow;
                    foreach (var pair in pairs)
                    {
                        usersNotifiedCount += await this.NotifyPair(connectorClient, team.TenantId, teamName, pair, matchDate);
                        pairsNotifiedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error notifying pairs for team {team.Id}: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            this.telemetryClient.TrackTrace($"Made {pairsNotifiedCount} pairups, {usersNotifiedCount} notifications sent");

            return pairsNotifiedCount;
        }

        /// <summary>
        /// Return all teams where the specified user can perform admin actions
        /// </summary>
        /// <param name="userAadId">user id</param>
        /// <returns>a list of teams</returns>
        public async Task<IList<TeamInstallInfo>> GetTeamsAllowingAdminActionsByUser(string userAadId)
        {
            var teams = await this.dataProvider.GetInstalledTeamsAsync();
            return teams.Where(team => team.AdminUserId == userAadId).ToList();
        }

        /// <summary>
        /// Generate pairups and send pairup notifications.
        /// </summary>
        /// <returns>The number of pairups that were made</returns>
        public async Task<int> MakePairsAndNotify()
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
                    pairsNotifiedCount += await this.NotifyAllPairs(team, matchResult.Pairs);
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

            this.telemetryClient.TrackTrace($"Made {pairsNotifiedCount} pairups");
            return pairsNotifiedCount;
        }

        /// <summary>
        /// Method that will return the information of the installed team
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>The team that the bot has been installed to</returns>
        public Task<TeamInstallInfo> GetInstalledTeam(string teamId)
        {
            return this.dataProvider.GetInstalledTeamAsync(teamId);
        }

        /// <summary>
        /// Send a welcome message to the user that was just added to a team.
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="memberAddedId">The id of the added user</param>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="teamId">The id of the team the user was added to</param>
        /// <param name="botInstaller">The person that installed the bot</param>
        /// <returns>Tracking task</returns>
        public async Task WelcomeUser(ConnectorClient connectorClient, string memberAddedId, string tenantId, string teamId, string botInstaller)
        {
            this.telemetryClient.TrackTrace($"Sending welcome message for user {memberAddedId}");

            var teamName = await this.GetTeamNameAsync(connectorClient, teamId);
            var allMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);

            ChannelAccount userThatJustJoined = null;
            foreach (var m in allMembers)
            {
                // both values are 29: values
                if (m.Id == memberAddedId)
                {
                    userThatJustJoined = m;
                    break;
                }
            }

            if (userThatJustJoined != null)
            {
                // If you optout, leave the team, then join the team again, we should optin the user automatically.
                var userInfo = await this.dataProvider.GetUserInfoAsync(userThatJustJoined.GetUserId());
                if (userInfo != null && !userInfo.OptedIn)
                {
                    userInfo.OptedIn = true;
                    await this.dataProvider.SetUserInfoAsync(userInfo);
                }

                var welcomeMessageCard = WelcomeNewMemberAdaptiveCard.GetCard(teamName, userThatJustJoined.Name, this.botDisplayName, botInstaller);
                await this.NotifyUser(connectorClient, welcomeMessageCard, userThatJustJoined, tenantId);
            }
            else
            {
                this.telemetryClient.TrackTrace($"Member {memberAddedId} was not found in team {teamId}, skipping welcome message.", SeverityLevel.Warning);
            }
        }

        /// <summary>
        /// Sends a welcome message to the General channel of the team that this bot has been installed to
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="teamId">The id of the team that the bot is installed to</param>
        /// <param name="botInstaller">The installer of the application</param>
        /// <returns>Tracking task</returns>
        public async Task WelcomeTeam(ConnectorClient connectorClient, string teamId, string botInstaller)
        {
            this.telemetryClient.TrackTrace($"Sending welcome message for team {teamId}");

            var teamName = await this.GetTeamNameAsync(connectorClient, teamId);
            var welcomeTeamMessageCard = WelcomeTeamAdaptiveCard.GetCard(teamName, this.botDisplayName, botInstaller);
            await this.NotifyTeam(connectorClient, welcomeTeamMessageCard, teamId);
        }

        /// <summary>
        /// Sends a message whenever there is unrecognized input into the bot
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">The activity for replying to a message</param>
        /// <returns>Tracking task</returns>
        public async Task SendUnrecognizedInputMessage(ConnectorClient connectorClient, Activity replyActivity)
        {
            var unrecognizedInputAdaptiveCard = UnrecognizedInputAdaptiveCard.GetCard();
            replyActivity.Attachments = new List<Attachment>()
            {
                this.CreateAdaptiveCardAttachment(unrecognizedInputAdaptiveCard)
            };
            await connectorClient.Conversations.ReplyToActivityAsync(replyActivity);
        }

        /// <summary>
        /// Sends a message to edit the user profile
        /// </summary>
        /// <param name="connectorClient">The connector client</param>
        /// <param name="replyActivity">Activity for replying</param>
        /// <param name="tenantId">Tenant id of the user</param>
        /// <param name="userAadId">User AAD id</param>
        /// <returns>Empty task</returns>
        public async Task EditUserProfile(ConnectorClient connectorClient, Activity replyActivity, string tenantId, string userAadId)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            var card = EditUserProfileAdaptiveCard.GetCard(userInfo.Discipline, userInfo.Gender, userInfo.Seniority, userInfo.Teams);
            replyActivity.Attachments = new List<Attachment>()
            {
                this.CreateAdaptiveCardAttachment(card)
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
        /// <param name="teams">Teams of the user to store in the database. Can be empty list.</param>
        /// <returns>Empty task</returns>
        public async Task SaveUserProfile(
            ConnectorClient connectorClient,
            Activity activity,
            string tenantId,
            string userAadId,
            string discipline,
            string gender,
            string seniority,
            List<string> teams)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.Discipline = discipline;
            userInfo.Gender = gender;
            userInfo.Seniority = seniority;
            userInfo.Teams = teams;

            var isSuccess = await this.dataProvider.SetUserInfoAsync(userInfo);

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
                    this.CreateAdaptiveCardAttachment(ViewUserProfileAdaptiveCard.GetCard(
                        this.GetUITextForProfileData(discipline),
                        this.GetUITextForProfileData(gender),
                        this.GetUITextForProfileData(seniority),
                        teams))
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
        /// <param name="botInstallerUserName">Name of the person that added the bot to the team</param>
        /// <param name="botInstallerUserAadId">User AAD id of the person that has added the bot to the team</param>
        /// <returns>Tracking task</returns>
        public Task SaveAddedToTeam(string serviceUrl, string teamId, string tenantId, string botInstallerUserName, string botInstallerUserAadId)
        {
            var teamInstallInfo = new TeamInstallInfo
            {
                ServiceUrl = serviceUrl,
                TeamId = teamId,
                TenantId = tenantId,
                InstallerName = botInstallerUserName,
                AdminUserId = botInstallerUserAadId,
                NotifyPairsMode = TeamInstallInfo.NotifyMode.Automatic
            };
            return this.dataProvider.UpdateTeamInstallStatusAsync(teamInstallInfo, true);
        }

        /// <summary>
        /// Save information about the team from which the bot was removed.
        /// </summary>
        /// <param name="serviceUrl">The service url</param>
        /// <param name="teamId">The team id</param>
        /// <param name="tenantId">The tenant id</param>
        /// <returns>Tracking task</returns>
        public Task SaveRemoveFromTeam(string serviceUrl, string teamId, string tenantId)
        {
            var teamInstallInfo = new TeamInstallInfo
            {
                TeamId = teamId,
                TenantId = tenantId,
            };
            return this.dataProvider.UpdateTeamInstallStatusAsync(teamInstallInfo, false);
        }

        /// <summary>
        /// Opt out the user from further pairups
        /// </summary>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="userAadId">The user AAD id</param>
        /// <returns>Whether the opt out was successful</returns>
        public async Task<bool> OptOutUser(string tenantId, string userAadId)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.OptedIn = false;
            return await this.dataProvider.SetUserInfoAsync(userInfo);
        }

        /// <summary>
        /// Opt in the user to pairups
        /// </summary>
        /// <param name="tenantId">The tenant id</param>
        /// <param name="userAadId">The user AAD id</param>
        /// <returns>Whether the opt in was successful</returns>
        public async Task<bool> OptInUser(string tenantId, string userAadId)
        {
            var userInfo = await this.GetOrCreateUnpersistedUserInfo(tenantId, userAadId);
            userInfo.OptedIn = true;
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
        /// Get the UI text of the value stored in the database.
        /// </summary>
        /// <param name="dbValue">Usually lowercase without spaces version of the UI value</param>
        /// <returns>UI text</returns>
        private string GetUITextForProfileData(string dbValue)
        {
            // TODO: Should probably do a proper mapping but just uppercase for now.
            return string.IsNullOrEmpty(dbValue) ? dbValue : dbValue[0].ToString().ToUpperInvariant() + dbValue.Substring(1);
        }

        private async Task<UserInfo> GetOrCreateUnpersistedUserInfo(string tenantId, string userAadId)
        {
            var userInfo = await this.dataProvider.GetUserInfoAsync(userAadId);
            return userInfo ?? new UserInfo { TenantId = tenantId, UserId = userAadId };
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
            var cardForPerson1 = PairUpNotificationAdaptiveCard.GetCard(teamName, teamsPerson1, teamsPerson2, this.botDisplayName);

            // Fill in person1's info in the card for person2
            var cardForPerson2 = PairUpNotificationAdaptiveCard.GetCard(teamName, teamsPerson2, teamsPerson1, this.botDisplayName);

            // Send notifications and return the number that was successful
            var notifyResults = await Task.WhenAll(
                this.NotifyUser(connectorClient, cardForPerson1, teamsPerson1, tenantId),
                this.NotifyUser(connectorClient, cardForPerson2, teamsPerson2, tenantId));
            var successfulNotifyCount = notifyResults.Count(wasNotified => wasNotified);
            if (successfulNotifyCount > 0)
            {
                // As long as one person gets the notification we'll consider it a match for both.
                await Task.WhenAll(
                    this.SavePastMatch(tenantId, teamsPerson1.GetUserId(), teamsPerson2.GetUserId(), matchDate),
                    this.SavePastMatch(tenantId, teamsPerson2.GetUserId(), teamsPerson1.GetUserId(), matchDate));
            }

            return successfulNotifyCount;
        }

        private Attachment CreateAdaptiveCardAttachment(string cardJSON)
        {
            return new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = JsonConvert.DeserializeObject(cardJSON),
            };
        }

        private async Task<bool> NotifyUser(ConnectorClient connectorClient, string cardToSend, ChannelAccount user, string tenantId)
        {
            var userId = user.GetUserId();
            this.telemetryClient.TrackTrace($"Sending notification to user {userId}");

            try
            {
                // ensure conversation exists
                var bot = new ChannelAccount { Id = this.botId };
                var response = connectorClient.Conversations.CreateOrGetDirectConversation(bot, user, tenantId);
                this.telemetryClient.TrackTrace($"Received conversation {response.Id}");

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
                        this.CreateAdaptiveCardAttachment(cardToSend)
                    }
                };

                if (!this.isTesting)
                {
                    await connectorClient.Conversations.SendToConversationAsync(activity);
                }

                return true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error sending notification to user: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
                return false;
            }
        }

        private async Task<bool> SavePastMatch(string tenantId, string userAadId, string matchedUserAadId, DateTime matchDate)
        {
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
        private async Task NotifyTeam(ConnectorClient connectorClient, string cardToSend, string teamId)
        {
            this.telemetryClient.TrackTrace($"Sending notification to team {teamId}");

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
                        this.CreateAdaptiveCardAttachment(cardToSend)
                    }
                };

                await connectorClient.Conversations.SendToConversationAsync(activity);
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error sending notification to team: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }
        }

        private async Task<List<ChannelAccount>> GetOptedInUsers(ConnectorClient connectorClient, TeamInstallInfo teamInfo)
        {
            // Pull the roster of specified team and then remove everyone who has opted out explicitly
            var members = await connectorClient.Conversations.GetConversationMembersAsync(teamInfo.TeamId);
            this.telemetryClient.TrackTrace($"Found {members.Count} in team {teamInfo.TeamId}");

            // UserInfo only exists if the user has entered some info in the first place (eg optout or profile details)
            // Optin is presumed if no UserInfo is found.
            var tasks = members.Select(m => this.dataProvider.GetUserInfoAsync(m.GetUserId()));
            var results = await Task.WhenAll(tasks);

            return members
                .Zip(results, (member, userInfo) => ((userInfo == null) || userInfo.OptedIn) ? member : null)
                .Where(m => m != null)
                .ToList();
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
                return new MatchResult(new List<Tuple<ChannelAccount, ChannelAccount>>(), users.First());
            }

            Random random = new Random(Guid.NewGuid().GetHashCode());

            var useStableMarriageAlgorithmUserCount = 4;

            if (userCount < useStableMarriageAlgorithmUserCount)
            {
                var randomAlgorithm = new RandomAlgorithm(random);
                return randomAlgorithm.CreateMatches(users);
            }
            else
            {
                var peopleData = await new PeopleDataCreator(this.dataProvider, users).Get();
                return new StableMarriageAlgorithm(random, peopleData).CreateMatches(users);
            }
        }
    }
}