using System;
using System.Text;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character.Command;
using AeroMessages.GSS.V66.Generic;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.Systems.SystemEvents;
using Serilog;

namespace GameServer.Systems.Chat;

public class ChatService
{
    public static readonly ChatChannel[] PublicBroadcastChannels =
    [
        ChatChannel.Zone,
        ChatChannel.ZoneLang,
        ChatChannel.Say,
        ChatChannel.Yell,
    ];

    private readonly Shard _shard;
    private readonly EventBus _eventBus;
    private readonly ChatCommandService _commandService;
    private readonly ILogger _logger;

    public ChatService(Shard shard, EventBus eventBus)
    {
        _shard = shard;
        _eventBus = eventBus;
        _commandService = new ChatCommandService(shard);
        _logger = shard.Logger.ForContext<ChatService>();
        _eventBus.Subscribe<DebugChatDirectMessageEvent>(OnDebugChatDirectMessage);
        _eventBus.Subscribe<DebugChatBroadcastMessageEvent>(OnDebugChatBroadcastMessage);
    }

    public void CharacterPerformTextChat(INetworkClient client, IEntity entity, PerformTextChat query)
    {
        if (query.Message.Length == 0 || query.AlternateData.AlternateType != ChatMessageAlternateData.ChatMessageAlternateType.NONE)
        {
            // TODO: AlternateType messages
            return;
        }

        ChatChannel queryChannel = (ChatChannel)query.Channel;

        var trimmed = query.Message.Trim();
        if (trimmed.StartsWith('\\'))
        {
            _commandService.ExecuteCommand(trimmed[1..], ((CharacterEntity)entity).Player);
            _logger.Information("Chat Command Executed: {message}", query.Message);
        }
        else if (PublicBroadcastChannels.Contains(queryChannel))
        {
            SendToAll(query.Message, queryChannel, entity);
        }
        else if (queryChannel == ChatChannel.Admin)
        {
            _shard.Admin.ExecuteCommand(query.Message, ((CharacterEntity)entity).Player);
        }
        else
        {
            var player = ((CharacterEntity)entity).Player;
            player?.SendDebugChat("This channel is not available");
        }
    }

    public void SendToPlayer(string message, ChatChannel channel, INetworkClient player)
    {
        var response = PrepareSingleMessage(message, channel, null);
        player.NetChannels[ChannelType.UnreliableGss].SendMessage(response, _shard.InstanceId);
    }

    public void SendToAll(string message, ChatChannel channel, IEntity sender)
    {
        var response = PrepareSingleMessage(message, channel, sender);
        foreach (var client in _shard.Clients.Values)
        {
            if (client.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                client.NetChannels[ChannelType.UnreliableGss].SendMessage(response, _shard.InstanceId);
            }
        }
    }

    public string GetCommandList()
    {
        return _commandService.GetCommandList();
    }

    /// <summary>
    ///     Flattens a chat string to ASCII, because a single non-ASCII character throws the whole send
    ///     away.
    /// </summary>
    /// <remarks>
    ///     Aero's generated <c>ChatMessageList</c> sizes the buffer from <c>Message.Length</c> — a count
    ///     of characters — and then writes <c>Encoding.UTF8.GetBytes(Message)</c> into it. For ASCII the
    ///     two agree. For anything else the write is longer than the buffer and <c>Pack</c> throws
    ///     IndexOutOfRange, which is not caught anywhere useful: it unwinds the whole admin command, so a
    ///     command that printed one em dash on its third line loses every line after the second and looks
    ///     like it silently stopped working. An accented character in a player's name or message does the
    ///     same to ordinary chat.
    ///     <para>
    ///     The generator is the NuGet package Aero.Gen, not our source, so the size calculation cannot be
    ///     corrected here. Transliterating at the boundary is the fix available: the punctuation that
    ///     actually shows up gets a plain equivalent, and anything else becomes '?' rather than taking the
    ///     message down with it.
    ///     </para>
    /// </remarks>
    internal static string ToWireSafe(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var needsWork = false;
        foreach (var c in text)
        {
            if (c > 0x7F)
            {
                needsWork = true;
                break;
            }
        }

        if (!needsWork)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            switch (c)
            {
                case <= (char)0x7F: builder.Append(c); break;
                case '\u2014': case '\u2013': builder.Append('-'); break;
                case '\u2018': case '\u2019': builder.Append('\''); break;
                case '\u201C': case '\u201D': builder.Append('"'); break;
                case '\u2026': builder.Append("..."); break;
                case '\u00A0': builder.Append(' '); break;
                default: builder.Append('?'); break;
            }
        }

        return builder.ToString();
    }

    private ChatMessageList PrepareSingleMessage(string message, ChatChannel channel, IEntity sender)
    {
        var senderId = sender != null ? sender.AeroEntityId : new EntityId() { Backing = _shard.InstanceId };
        var senderName = sender != null ? ((CharacterEntity)sender).StaticInfo.DisplayName : "Server";
        byte chatIconFlags = (byte)(sender != null ? 0 : 1);

        var response = new ChatMessageList()
        {
            Messages =
            [
                new()
                {
                    SenderId = senderId,
                    SenderName = ToWireSafe(senderName),
                    Message = ToWireSafe(message),
                    Channel = (byte)channel,
                    ChatIconFlags = chatIconFlags,
                    AltData = new()
                    {
                        AlternateType = ChatMessageAlternateData.ChatMessageAlternateType.NONE,
                        HaveAltData = 0,
                    },
                    HaveAltEntity = 0,
                    HaveAltString = 0,
                },
            ],
        };
        return response;
    }

    private void OnDebugChatDirectMessage(DebugChatDirectMessageEvent evt)
    {
        SendToPlayer(evt.Message, ChatChannel.Debug, evt.Target);
    }

    private void OnDebugChatBroadcastMessage(DebugChatBroadcastMessageEvent evt)
    {
        SendToAll(evt.Message, ChatChannel.Debug, evt.Source);
    }
}