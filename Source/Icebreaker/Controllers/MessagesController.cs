//----------------------------------------------------------------------------------------------
// <copyright file="MessagesController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------

namespace Icebreaker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.UI.WebControls;
    using Icebreaker.Helpers;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.Bot.Connector;
    using Microsoft.Bot.Connector.Teams;
    using Microsoft.Bot.Connector.Teams.Models;
    using Newtonsoft.Json;
    using Properties;

    /// <summary>
    /// Controller for the bot messaging endpoint
    /// </summary>
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private readonly IcebreakerBot bot;
        private readonly TelemetryClient telemetryClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesController"/> class.
        /// </summary>
        /// <param name="bot">The Icebreaker bot instance</param>
        /// <param name="telemetryClient">The telemetry client instance</param>
        public MessagesController(IcebreakerBot bot, TelemetryClient telemetryClient)
        {
            this.bot = bot;
            this.telemetryClient = telemetryClient;
        }

        /// <summary>
        /// POST: api/messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// <param name="activity">The incoming activity</param>
        /// <returns>Task that resolves to the HTTP response message</returns>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            this.LogActivityTelemetry(activity);

            using (var connectorClient = new ConnectorClient(new Uri(activity.ServiceUrl)))
            {
                if (activity.Type == ActivityTypes.Message)
                {
                    await this.HandleMessageActivity(connectorClient, activity);
                }
                else
                {
                    await this.HandleSystemActivity(connectorClient, activity);
                }
            }

            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task HandleMessageActivity(ConnectorClient connectorClient, Activity activity)
        {
            try
            {
                var senderAadId = activity.From.Properties["aadObjectId"].ToString();
                var teamChannelData = activity.GetChannelData<TeamsChannelData>();
                var tenantId = teamChannelData.Tenant.Id;
                var hasTeamContext = teamChannelData.Team != null;
                var msg = activity.Text.ToLowerInvariant();

                if (msg == MessageIds.OptOut)
                {
                    await this.HandleOptOut(connectorClient, activity, senderAadId, tenantId);
                }
                else if (msg == MessageIds.OptIn)
                {
                    await this.HandleOptIn(connectorClient, activity, senderAadId, tenantId);
                }
                else if (msg == MessageIds.MakePairs)
                {
                    await this.HandleMakePairs(connectorClient, activity, senderAadId);
                }
                else if (msg == MessageIds.NotifyPairs)
                {
                    if (activity.Value != null && activity.Value.ToString().TryParseJson(out MakePairsResult result))
                    {
                        await this.HandleNotifyPairs(connectorClient, activity, senderAadId, result.TeamId);
                    }
                } // TODO: handle messages to change the notify mode
                else
                {
                    if (hasTeamContext)
                    {
                        // Unknown input
                        this.telemetryClient.TrackTrace($"Cannot process the following: {activity.Text}");
                        var replyActivity = activity.CreateReply();
                        await this.bot.SendUnrecognizedInputMessage(connectorClient, replyActivity);
                    }
                    else
                    {
                        // TODO provide pause/unpause, edit profile etc
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error while handling message activity: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }
        }

        private async Task HandleMakePairs(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            if (activity.Value != null && activity.Value.ToString().TryParseJson(out TeamContext request))
            {
                var team = await this.bot.GetInstalledTeam(request.TeamId);
                await this.HandleMakePairsForTeam(connectorClient, activity, senderAadId, team, request.TeamName);
            }
            else
            {
                await this.HandleMakePairsNoTeam(connectorClient, activity, senderAadId);
            }
        }

        private async Task HandleOptIn(ConnectorClient connectorClient, Activity activity, string senderAadId, string tenantId)
        {
            // User opted in
            this.telemetryClient.TrackTrace($"User {senderAadId} opted in");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", senderAadId },
                { "OptInStatus", "true" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            await this.bot.OptInUser(tenantId, senderAadId, activity.ServiceUrl);

            var optInReply = activity.CreateReply();
            optInReply.Attachments = new List<Attachment>
            {
                new HeroCard()
                {
                    Text = Resources.OptInConfirmation,
                    Buttons = new List<CardAction>()
                    {
                        new CardAction()
                        {
                            Title = Resources.PausePairingsButtonText,
                            DisplayText = Resources.PausePairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.OptOut
                        }
                    }
                }.ToAttachment(),
            };

            await connectorClient.Conversations.ReplyToActivityAsync(optInReply);
        }

        private async Task HandleOptOut(ConnectorClient connectorClient, Activity activity, string senderAadId, string tenantId)
        {
            // User opted out
            this.telemetryClient.TrackTrace($"User {senderAadId} opted out");

            var properties = new Dictionary<string, string>
            {
                { "UserAadId", senderAadId },
                { "OptInStatus", "false" },
            };
            this.telemetryClient.TrackEvent("UserOptInStatusSet", properties);

            await this.bot.OptOutUser(tenantId, senderAadId, activity.ServiceUrl);

            var optOutReply = activity.CreateReply();
            optOutReply.Attachments = new List<Attachment>
            {
                new HeroCard()
                {
                    Text = Resources.OptOutConfirmation,
                    Buttons = new List<CardAction>()
                    {
                        new CardAction()
                        {
                            Title = Resources.ResumePairingsButtonText,
                            DisplayText = Resources.ResumePairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.OptIn
                        }
                    }
                }.ToAttachment(),
            };

            await connectorClient.Conversations.ReplyToActivityAsync(optOutReply);
        }

        private async Task HandleMakePairsNoTeam(ConnectorClient connectorClient, Activity activity, string senderAadId)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered make pairs with no team specified");

            var teamsAllowingAdminActionsByUser = await this.bot.GetTeamsAllowingAdminActionsByUser(senderAadId);

            if (teamsAllowingAdminActionsByUser.Count == 0)
            {
                var noTeamMsg = string.Format(Resources.AdminActionNoTeamMsg, Resources.AdminActionGeneratePairs);
                var noTeamReply = activity.CreateReply(noTeamMsg);
                await connectorClient.Conversations.ReplyToActivityAsync(noTeamReply);
            }
            else if (teamsAllowingAdminActionsByUser.Count == 1)
            {
                var team = teamsAllowingAdminActionsByUser.First();
                var teamName = await this.bot.GetTeamNameAsync(connectorClient, team.Id);
                await this.HandleMakePairsForTeam(connectorClient, activity, senderAadId, team, teamName);
            }
            else
            {
                var teamActions = new List<CardAction>();

                foreach (var team in teamsAllowingAdminActionsByUser)
                {
                    var teamName = await this.bot.GetTeamNameAsync(connectorClient, team.Id);
                    var teamCardAction = new CardAction()
                    {
                        Title = teamName,
                        DisplayText = teamName,
                        Type = ActionTypes.MessageBack,
                        Text = MessageIds.MakePairs,
                        Value = JsonConvert.SerializeObject(new TeamContext { TeamId = team.Id, TeamName = teamName })
                    };
                    teamActions.Add(teamCardAction);
                }

                var pickTeamReply = activity.CreateReply();
                pickTeamReply.Attachments = new List<Attachment>
                {
                    new HeroCard()
                    {
                        Text = string.Format(Resources.AdminActionWhichTeamText, Resources.AdminActionGeneratePairs),
                        Buttons = teamActions
                    }.ToAttachment(),
                };

                await connectorClient.Conversations.ReplyToActivityAsync(pickTeamReply);
            }
        }

        private async Task HandleMakePairsForTeam(ConnectorClient connectorClient, Activity activity, string senderAadId, TeamInstallInfo team, string teamName)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered make pairs");

            var matchResult = await this.bot.MakePairsForTeam(team);
            var pairs = matchResult.Pairs;
            var pairsStrs = pairs.Select((pair, i) => $"{i + 1}. {pair.Item1.Name} - {pair.Item2.Name}").ToList();
            var allPairsStr = string.Join("<p/>", pairsStrs);
            if (matchResult.OddPerson != null)
            {
                allPairsStr += $"<p/>Odd person: {matchResult.OddPerson.Name}";
            }

            var idPairs = pairs.Select(pair => new Tuple<string, string>(pair.Item1.Id, pair.Item2.Id)).ToList();
            var makePairsResult = new MakePairsResult()
            {
                PairChannelAccountIds = idPairs,
                TeamId = team.Id
            };

            Activity reply = activity.CreateReply();
            reply.Attachments = new List<Attachment>
            {
                new HeroCard()
                {
                    Title = string.Format(Resources.NewPairingsTitle, teamName),
                    Text = allPairsStr,
                    Buttons = new List<CardAction>()
                    {
                        new CardAction
                        {
                            Title = Resources.SendPairingsButtonText,
                            DisplayText = Resources.SendPairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.NotifyPairs,
                            Value = JsonConvert.SerializeObject(makePairsResult)
                        },
                        new CardAction
                        {
                            Title = Resources.RegeneratePairingsButtonText,
                            DisplayText = Resources.RegeneratePairingsButtonText,
                            Type = ActionTypes.MessageBack,
                            Text = MessageIds.MakePairs,
                            Value = JsonConvert.SerializeObject(new TeamContext { TeamId = team.Id, TeamName = teamName })
                        }
                    }
                }.ToAttachment()
            };
            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleNotifyPairs(ConnectorClient connectorClient, Activity activity, string senderAadId, string teamId)
        {
            this.telemetryClient.TrackTrace($"User {senderAadId} triggered notify pairs");

            string replyMessage = string.Empty;

            try
            {
                var makePairsResult = JsonConvert.DeserializeObject<MakePairsResult>(activity.Value.ToString());

                var members = await connectorClient.Conversations.GetConversationMembersAsync(teamId);
                var membersByChannelAccountId = members.ToDictionary(key => key.Id, value => value);

                // Evaluate all values so we can fail early if someone no longer exists
                var pairs = makePairsResult.PairChannelAccountIds.Select(pair => new Tuple<ChannelAccount, ChannelAccount>(
                    membersByChannelAccountId[pair.Item1],
                    membersByChannelAccountId[pair.Item2])).ToList();

                var team = await this.bot.GetInstalledTeam(teamId);
                var numPairsNotified = await this.bot.NotifyAllPairs(team, pairs);
                replyMessage = string.Format(Resources.ManualNotifiedUsersMessage, numPairsNotified);
            }
            catch (Exception ex)
            {
                replyMessage = Resources.ManualNotifiedUsersErrorMessage;
                this.telemetryClient.TrackTrace($"Error while notifying pairs: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
            }

            Activity reply = activity.CreateReply(replyMessage);
            await connectorClient.Conversations.ReplyToActivityAsync(reply);
        }

        private async Task HandleSystemActivity(ConnectorClient connectorClient, Activity message)
        {
            this.telemetryClient.TrackTrace("Processing system message");

            try
            {
                var teamsChannelData = message.GetChannelData<TeamsChannelData>();
                var tenantId = teamsChannelData.Tenant.Id;

                if (message.Type == ActivityTypes.ConversationUpdate)
                {
                    // conversation-update fires whenever a new 1:1 gets created between us and someone else as well
                    // only process the Teams ones.
                    if (string.IsNullOrEmpty(teamsChannelData?.Team?.Id))
                    {
                        // conversation-update is for 1:1 chat. Just ignore.
                        return;
                    }

                    string myBotId = message.Recipient.Id;
                    string teamId = message.Conversation.Id;

                    if (message.MembersAdded?.Count() > 0)
                    {
                        foreach (var member in message.MembersAdded)
                        {
                            if (member.Id == myBotId)
                            {
                                this.telemetryClient.TrackTrace($"Bot installed to team {teamId}");

                                var properties = new Dictionary<string, string>
                                {
                                    { "Scope", message.Conversation?.ConversationType },
                                    { "TeamId", teamId },
                                    { "InstallerId", message.From.Id },
                                };
                                this.telemetryClient.TrackEvent("AppInstalled", properties);

                                // Try to determine the name of the person that installed the app, which is usually the sender of the message (From.Id)
                                // Note that in some cases we cannot resolve it to a team member, because the app was installed to the team programmatically via Graph
                                var teamMembers = await connectorClient.Conversations.GetConversationMembersAsync(teamId);

                                var personThatAddedBot = teamMembers.FirstOrDefault(x => x.Id == message.From.Id);
                                var personName = personThatAddedBot?.Name;
                                var personAadId = personThatAddedBot?.AsTeamsChannelAccount().ObjectId;

                                await this.bot.SaveAddedToTeam(message.ServiceUrl, teamId, tenantId, personName, personAadId);

                                // Turn this off for now because constantly installing/uninstalling to try new version of the app.
                                // TODO: not big deal to not have it. Perhaps turn it off permanently.
                                // await this.bot.WelcomeTeam(connectorClient, teamId, personName);
                            }
                            else
                            {
                                this.telemetryClient.TrackTrace($"New member {member.Id} added to team {teamsChannelData.Team.Id}");

                                var installedTeam = await this.bot.GetInstalledTeam(teamsChannelData.Team.Id);
                                await this.bot.WelcomeUser(connectorClient, member.Id, tenantId, teamsChannelData.Team.Id, installedTeam.InstallerName);
                            }
                        }
                    }

                    if (message.MembersRemoved?.Any(x => x.Id == myBotId) == true)
                    {
                        this.telemetryClient.TrackTrace($"Bot removed from team {teamId}");

                        var properties = new Dictionary<string, string>
                        {
                            { "Scope", message.Conversation?.ConversationType },
                            { "TeamId", teamId },
                            { "UninstallerId", message.From.Id },
                        };
                        this.telemetryClient.TrackEvent("AppUninstalled", properties);

                        // we were just removed from a team
                        await this.bot.SaveRemoveFromTeam(message.ServiceUrl, teamId, tenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                this.telemetryClient.TrackTrace($"Error while handling system activity: {ex.Message}", SeverityLevel.Warning);
                this.telemetryClient.TrackException(ex);
                throw;
            }
        }

        /// <summary>
        /// Log telemetry about the incoming activity.
        /// </summary>
        /// <param name="activity">The activity</param>
        private void LogActivityTelemetry(Activity activity)
        {
            var fromObjectId = activity.From?.Properties["aadObjectId"]?.ToString();
            var clientInfoEntity = activity.Entities?.Where(e => e.Type == "clientInfo")?.FirstOrDefault();
            var channelData = activity.GetChannelData<TeamsChannelData>();

            var properties = new Dictionary<string, string>
            {
                { "ActivityId", activity.Id },
                { "ActivityType", activity.Type },
                { "UserAadObjectId", fromObjectId },
                {
                    "ConversationType",
                    string.IsNullOrWhiteSpace(activity.Conversation?.ConversationType) ? "personal" : activity.Conversation.ConversationType
                },
                { "ConversationId", activity.Conversation?.Id },
                { "TeamId", channelData?.Team?.Id },
                { "Locale", clientInfoEntity?.Properties["locale"]?.ToString() },
                { "Platform", clientInfoEntity?.Properties["platform"]?.ToString() }
            };
            this.telemetryClient.TrackEvent("UserActivity", properties);
        }

        private struct MakePairsResult
        {
            public List<Tuple<string, string>> PairChannelAccountIds
            {
                get; set;
            }

            public string TeamId { get; set; }
        }

        private struct TeamContext
        {
            public string TeamId { get; set; }

            public string TeamName { get; set; }
        }

        private static class MessageIds
        {
            public const string OptIn = "optin";
            public const string OptOut = "optout";
            public const string MakePairs = "makepairs";
            public const string NotifyPairs = "notifypairs";
        }
    }
}