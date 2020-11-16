//----------------------------------------------------------------------------------------------
// <copyright file="ProactiveMessage.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
namespace Icebreaker.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Bot.Connector;

    /// <summary>
    /// Store information so the bot can send a proactive message
    /// </summary>
    public class ProactiveMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProactiveMessage"/> class.
        /// </summary>
        /// <param name="message">message</param>
        public ProactiveMessage(Activity message)
        {
            this.ToId = message.From.Id;
            this.ToName = message.From.Name;
            this.FromId = message.Recipient.Id;
            this.FromName = message.Recipient.Name;
            this.ServiceUrl = message.ServiceUrl;
            this.ChannelId = message.ChannelId;
            this.ConversationId = message.Conversation.Id;
        }

        /// <summary>
        /// Gets the service url
        /// </summary>
        public string ServiceUrl { get; private set; }

        private string ToId { get; set; }

        private string ToName { get; set; }

        private string FromId { get; set; }

        private string FromName { get; set; }

        private string ChannelId { get; set; }

        private string ConversationId { get; set; }

        /// <summary>
        /// Send the proactive message with the supplied text
        /// </summary>
        /// <param name="connectorClient">connector client</param>
        /// <param name="messageText">text to send</param>
        /// <param name="attachments">message attachments</param>
        /// <returns>Task</returns>
        public async Task Send(ConnectorClient connectorClient, string messageText, List<Attachment> attachments = null)
        {
            // https://docs.microsoft.com/en-us/previous-versions/azure/bot-service/dotnet/bot-builder-dotnet-proactive-messages?view=azure-bot-service-3.0
            // Use the data stored previously to create the required objects.
            var userAccount = new ChannelAccount(this.ToId, this.ToName);
            var botAccount = new ChannelAccount(this.FromId, this.FromName);

            // Create a new message.
            IMessageActivity message = Activity.CreateMessageActivity();
            message.ChannelId = this.ChannelId;
            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: this.ConversationId);
            message.Text = messageText;
            if (attachments != null)
            {
                message.Attachments = attachments;
            }

            await connectorClient.Conversations.SendToConversationAsync((Activity)message);
        }
    }
}