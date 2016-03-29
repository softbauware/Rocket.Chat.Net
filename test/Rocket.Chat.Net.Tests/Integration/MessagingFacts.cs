﻿namespace Rocket.Chat.Net.Tests.Integration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using FluentAssertions;

    using Ploeh.AutoFixture;

    using Rocket.Chat.Net.Driver;
    using Rocket.Chat.Net.Interfaces;
    using Rocket.Chat.Net.Models;
    using Rocket.Chat.Net.Tests.Helpers;

    using Xunit;
    using Xunit.Abstractions;

    [Trait("Category", "Driver")]
    [Collection("Driver")]
    public class MessagingFacts : IDisposable
    {
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private readonly MessagingFixture _fixture;
        private static readonly Fixture AutoFixture = new Fixture();

        public MessagingFacts(ITestOutputHelper helper)
        {
            var roomName = AutoFixture.Create<string>();
            _fixture = new MessagingFixture(helper, roomName);
        }

        [Fact]
        public async Task Can_send_messages()
        {
            var text = AutoFixture.Create<string>();
            var messageReceived = new AutoResetEvent(false);

            RocketMessage message = null;
            _fixture.Master.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                message = rocketMessage;
                messageReceived.Set();
            };

            await _fixture.Master.InitAsync(Constants.Username, Constants.Password);

            // Act
            await _fixture.Master.Driver.SendMessageAsync(text, _fixture.RoomId);

            messageReceived.WaitOne(_timeout);

            // Assert
            message.Should().NotBeNull();
        }

        [Fact]
        public async Task When_sending_messages_bot_flag_should_be_set()
        {
            var text = AutoFixture.Create<string>();
            var messageReceived = new AutoResetEvent(false);

            RocketMessage message = null;
            _fixture.Master.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                message = rocketMessage;
                messageReceived.Set();
            };

            await _fixture.Master.InitAsync(Constants.Username, Constants.Password);

            // Act
            await _fixture.Master.Driver.SendMessageAsync(text, _fixture.RoomId);

            messageReceived.WaitOne(_timeout);

            // Assert
            message.Should().NotBeNull();
            message.IsBot.Should().BeTrue();
        }

        [Fact]
        public async Task When_bot_is_mentioned_set_mentioned_flag()
        {
            var text = AutoFixture.Create<string>() + " @" + Constants.Username;

            var masterReceived = new AutoResetEvent(false);
            RocketMessage masterMessage = null;
            _fixture.Master.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                masterMessage = rocketMessage;
                masterReceived.Set();
            };

            var slaveReceived = new AutoResetEvent(false);
            RocketMessage slaveMessage = null;
            _fixture.Slave.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                slaveMessage = rocketMessage;
                slaveReceived.Set();
            };

            await _fixture.Master.InitAsync(Constants.Username, Constants.Password);
            await _fixture.Slave.InitAsync(Constants.TestUsername, Constants.Password);

            // Act
            await _fixture.Master.Driver.SendMessageAsync(text, _fixture.RoomId);

            masterReceived.WaitOne(_timeout);
            slaveReceived.WaitOne(_timeout);

            // Assert
            masterMessage.Should().NotBeNull();
            masterMessage.IsBotMentioned.Should().BeTrue();

            slaveMessage.Should().NotBeNull();
            slaveMessage.IsBotMentioned.Should().BeFalse();
        }

        [Fact]
        public async Task When_bot_sends_message_on_receive_set_myself_flag()
        {
            var text = AutoFixture.Create<string>() + " @" + Constants.Username;

            var masterReceived = new AutoResetEvent(false);
            RocketMessage masterMessage = null;
            _fixture.Master.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                masterMessage = rocketMessage;
                masterReceived.Set();
            };

            var slaveReceived = new AutoResetEvent(false);
            RocketMessage slaveMessage = null;
            _fixture.Slave.Driver.MessageReceived += rocketMessage =>
            {
                if (rocketMessage.RoomId != _fixture.RoomId)
                {
                    return;
                }
                slaveMessage = rocketMessage;
                slaveReceived.Set();
            };

            await _fixture.Master.InitAsync(Constants.Username, Constants.Password);
            await _fixture.Slave.InitAsync(Constants.TestUsername, Constants.Password);

            // Act
            await _fixture.Master.Driver.SendMessageAsync(text, _fixture.RoomId);

            masterReceived.WaitOne(_timeout);
            slaveReceived.WaitOne(_timeout);

            // Assert
            masterMessage.Should().NotBeNull();
            masterMessage.IsFromMyself.Should().BeTrue();

            slaveMessage.Should().NotBeNull();
            slaveMessage.IsFromMyself.Should().BeFalse();
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }
    }

    public class MessagingFixture : IDisposable
    {
        private readonly XUnitLogger _logger;
        public RocketChatDriverFixture Fixture { get; }
        public RocketChatDriverFixture Master { get; }
        public RocketChatDriverFixture Slave { get; }

        public string RoomId { get; }
        public string RoomName { get; }

        public MessagingFixture(ITestOutputHelper helper, string roomName)
        {
            RoomName = roomName;
            _logger = new XUnitLogger(helper);
            Master = new RocketChatDriverFixture(_logger);
            Slave = new RocketChatDriverFixture(_logger);

            Fixture = new RocketChatDriverFixture(_logger);
            Fixture.InitAsync(Constants.Username, Constants.Password).Wait();
            RoomId = Fixture.Driver.CreateRoomAsync(roomName)?.Result?.Result?.RoomId;
        }

        public void Dispose()
        {
            _logger.Dispose();

            Fixture.Driver.EraseRoomAsync(RoomId).Wait();

            Fixture.Dispose();
            Master.Dispose();
            Slave.Dispose();
        }
    }

    public class RocketChatDriverFixture : IDisposable
    {
        public IRocketChatDriver Driver { get; }

        public RocketChatDriverFixture(XUnitLogger helper)
        {
            Driver = new RocketChatDriver(Constants.RocketServer, false, helper);
        }

        public async Task InitAsync(string username, string password)
        {
            await Driver.ConnectAsync();
            await Driver.LoginWithUsernameAsync(username, password);
            await Driver.SubscribeToRoomAsync();
        }

        public void Dispose()
        {
            Driver.Dispose();
        }
    }
}