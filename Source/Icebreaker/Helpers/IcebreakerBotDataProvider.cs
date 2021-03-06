//----------------------------------------------------------------------------------------------
// <copyright file="IcebreakerBotDataProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Icebreaker.Model;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Azure;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Data provider routines
    /// </summary>
    public class IcebreakerBotDataProvider
    {
        // Request the minimum throughput by default
        private const int DefaultRequestThroughput = 400;

        private readonly TelemetryClient telemetryClient;
        private readonly Lazy<Task> initializeTask;
        private DocumentClient documentClient;
        private Database database;
        private DocumentCollection teamsCollection;
        private DocumentCollection usersCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="IcebreakerBotDataProvider"/> class.
        /// </summary>
        /// <param name="telemetryClient">The telemetry client to use</param>
        public IcebreakerBotDataProvider(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.initializeTask = new Lazy<Task>(() => this.InitializeAsync());
        }

        /// <summary>
        /// Remove the team installation status in store.
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>Whether delete succeeded</returns>
        public async Task<bool> RemoveTeamInstallInfoAsync(string teamId)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var documentUri = UriFactory.CreateDocumentUri(this.database.Id, this.teamsCollection.Id, teamId);
                await this.documentClient.DeleteDocumentAsync(documentUri, new RequestOptions { PartitionKey = new PartitionKey(teamId) });

                return true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error deleting team info for {teamId}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Update the team install info
        /// </summary>
        /// <param name="team">team info to update</param>
        /// <returns>Whether the update was successful</returns>
        public async Task<bool> UpdateTeamInstallInfoAsync(TeamInstallInfo team)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var result = await this.documentClient.UpsertDocumentAsync(this.teamsCollection.SelfLink, team);
                return true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error updating team install info for {team.TeamId}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
            }

            return false;
        }

        /// <summary>
        /// Get the list of teams to which the app was installed.
        /// </summary>
        /// <returns>List of installed teams</returns>
        public async Task<IList<TeamInstallInfo>> GetInstalledTeamsAsync()
        {
            await this.EnsureInitializedAsync();

            var installedTeams = new List<TeamInstallInfo>();

            try
            {
                using (var lookupQuery = this.documentClient
                    .CreateDocumentQuery<TeamInstallInfo>(this.teamsCollection.SelfLink, new FeedOptions { EnableCrossPartitionQuery = true })
                    .AsDocumentQuery())
                {
                    while (lookupQuery.HasMoreResults)
                    {
                        var response = await lookupQuery.ExecuteNextAsync<TeamInstallInfo>();
                        installedTeams.AddRange(response);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace("Error getting all installed teams", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
            }

            return installedTeams;
        }

        /// <summary>
        /// Gets the active user ids for a team
        /// </summary>
        /// <param name="teamId">Team id</param>
        /// <returns>List of user ids</returns>
        public async Task<IList<string>> GetActiveUserIdsForTeam(string teamId)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var activeStatus = Enum.GetName(typeof(EnrollmentStatus), EnrollmentStatus.Active);

                var activeUserIdsQuery = this.documentClient.CreateDocumentQuery<UserIdResult>(
                    this.usersCollection.SelfLink,
                    $"SELECT c.id FROM c WHERE ARRAY_CONTAINS(c.statusInTeam, {{'teamId':'{teamId}', 'status': '{activeStatus}'}}, true)",
                    new FeedOptions { EnableCrossPartitionQuery = true }).ToList();

                return activeUserIdsQuery.Select(result => result.Id).ToList();
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error getting active user ids for team {teamId}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
                return new List<string>();
            }
        }

        /// <summary>
        /// Returns the installed team info given the team id. Can return null if we have no info about the team id.
        /// </summary>
        /// <param name="teamId">The team id</param>
        /// <returns>Team information</returns>
        public async Task<TeamInstallInfo> GetInstalledTeamAsync(string teamId)
        {
            await this.EnsureInitializedAsync();

            // Get team install info
            try
            {
                var documentUri = UriFactory.CreateDocumentUri(this.database.Id, this.teamsCollection.Id, teamId);
                return await this.documentClient.ReadDocumentAsync<TeamInstallInfo>(documentUri, new RequestOptions { PartitionKey = new PartitionKey(teamId) });
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error getting team info for {teamId}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <summary>
        /// Get the stored information about the given user. If the user doesn't exist, return null.
        /// </summary>
        /// <param name="userAadId">User id</param>
        /// <returns>User information or null</returns>
        public async Task<UserInfo> GetUserInfoAsync(string userAadId)
        {
            await this.EnsureInitializedAsync();

            try
            {
                var documentUri = UriFactory.CreateDocumentUri(this.database.Id, this.usersCollection.Id, userAadId);
                return await this.documentClient.ReadDocumentAsync<UserInfo>(documentUri, new RequestOptions { PartitionKey = new PartitionKey(userAadId) });
            }
            catch (Exception ex)
            {
                // This is typical for when we do an existence check on the user document. Downgrade to a warning.
                this.telemetryClient.TrackTrace($"Error getting user info for {userAadId}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex.InnerException);
                return null;
            }
        }

        /// <summary>
        /// Set the user info for the given user
        /// </summary>
        /// <param name="userInfo">User info</param>
        /// <returns>Whether the update was successful</returns>
        public async Task<bool> SetUserInfoAsync(UserInfo userInfo)
        {
            try
            {
                await this.EnsureInitializedAsync();
                var doc = await this.documentClient.UpsertDocumentAsync(this.usersCollection.SelfLink, userInfo);
                return true;
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error setting user info for {userInfo?.Id}", SeverityLevel.Error);
                this.telemetryClient.TrackException(ex.InnerException);
                return false;
            }
        }

        /// <summary>
        /// Initializes the database connection.
        /// </summary>
        /// <returns>Tracking task</returns>
        private async Task InitializeAsync()
        {
            this.telemetryClient.TrackTrace("Initializing data store");

            var endpointUrl = CloudConfigurationManager.GetSetting("CosmosDBEndpointUrl");
            var primaryKey = CloudConfigurationManager.GetSetting("CosmosDBKey");
            var databaseName = CloudConfigurationManager.GetSetting("CosmosDBDatabaseName");
            var teamsCollectionName = CloudConfigurationManager.GetSetting("CosmosCollectionTeams");
            var usersCollectionName = CloudConfigurationManager.GetSetting("CosmosCollectionUsers");

            this.documentClient = new DocumentClient(new Uri(endpointUrl), primaryKey);

            var requestOptions = new RequestOptions { OfferThroughput = DefaultRequestThroughput };
            bool useSharedOffer = true;

            // Create the database if needed
            try
            {
                this.database = await this.documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName }, requestOptions);
            }
            catch (DocumentClientException ex)
            {
                if (ex.Error?.Message?.Contains("SharedOffer is Disabled") ?? false)
                {
                    this.telemetryClient.TrackTrace("Database shared offer is disabled for the account, will provision throughput at container level", SeverityLevel.Information);
                    useSharedOffer = false;

                    this.database = await this.documentClient.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });
                }
                else
                {
                    throw;
                }
            }

            // Get a reference to the Teams collection, creating it if needed
            var teamsCollectionDefinition = new DocumentCollection
            {
                Id = teamsCollectionName,
            };
            teamsCollectionDefinition.PartitionKey.Paths.Add("/id");
            this.teamsCollection = await this.documentClient.CreateDocumentCollectionIfNotExistsAsync(this.database.SelfLink, teamsCollectionDefinition, useSharedOffer ? null : requestOptions);

            // Get a reference to the Users collection, creating it if needed
            var usersCollectionDefinition = new DocumentCollection
            {
                Id = usersCollectionName
            };
            usersCollectionDefinition.PartitionKey.Paths.Add("/id");
            this.usersCollection = await this.documentClient.CreateDocumentCollectionIfNotExistsAsync(this.database.SelfLink, usersCollectionDefinition, useSharedOffer ? null : requestOptions);

            this.telemetryClient.TrackTrace("Data store initialized");
        }

        private async Task EnsureInitializedAsync()
        {
            await this.initializeTask.Value;
        }

        private class UserIdResult
        {
            /// <summary>
            /// Gets or sets the message id the choose user result was for
            /// </summary>
            [JsonProperty(Required = Required.Always)]
            public string Id { get; set; } = string.Empty;
        }
    }
}