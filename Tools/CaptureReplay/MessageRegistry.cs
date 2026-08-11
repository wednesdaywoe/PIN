using System;
using System.Collections.Generic;
using System.Linq;
using Aero.Gen;
using Aero.Gen.Attributes;
using Controllers = GameServer.Enums.GSS.Controllers;

namespace CaptureReplay;

/// <summary>
///     Maps a wire (controllerId, messageId) pair onto the AeroMessages type that describes it,
///     built by reflecting over the <see cref="AeroMessageIdAttribute" /> the message definitions
///     already carry. This is the same metadata the Aero source generator reads, so the tool
///     stays correct as AeroMessages grows without needing its own hand-written table.
/// </summary>
public sealed class MessageRegistry
{
    private readonly Dictionary<(AeroMessageIdAttribute.MsgSrc Src, int Controller, int Message), Type> _gss = new();
    private readonly Dictionary<(AeroMessageIdAttribute.MsgType Typ, int Message), Type> _flat = new();

    private MessageRegistry()
    {
    }

    public int GssCount => _gss.Count;

    public int FlatCount => _flat.Count;

    public static MessageRegistry Build()
    {
        var registry = new MessageRegistry();
        var assembly = typeof(AeroMessages.GSS.V66.Character.Controller.LocalEffectsController).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(IAero).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var attribute in type.GetCustomAttributes(typeof(AeroMessageIdAttribute), false).Cast<AeroMessageIdAttribute>())
            {
                if (attribute.Typ == AeroMessageIdAttribute.MsgType.GSS)
                {
                    foreach (var src in Sources(attribute.Src))
                    {
                        registry._gss.TryAdd((src, attribute.ControllerId, attribute.MessageId), type);
                    }
                }
                else
                {
                    registry._flat.TryAdd((attribute.Typ, attribute.MessageId), type);
                }
            }
        }

        return registry;
    }

    /// <summary>
    ///     A message tagged <c>Both</c> is valid in either direction, so it is registered twice.
    /// </summary>
    private static IEnumerable<AeroMessageIdAttribute.MsgSrc> Sources(AeroMessageIdAttribute.MsgSrc src)
        => src == AeroMessageIdAttribute.MsgSrc.Both
               ? [AeroMessageIdAttribute.MsgSrc.Command, AeroMessageIdAttribute.MsgSrc.Message]
               : [src];

    public Type LookupGss(AeroMessageIdAttribute.MsgSrc src, byte controllerId, byte messageId)
        => _gss.GetValueOrDefault((src, controllerId, messageId));

    public Type LookupFlat(AeroMessageIdAttribute.MsgType typ, byte messageId)
        => _flat.GetValueOrDefault((typ, messageId));

    /// <summary>
    ///     Controller ids PIN names in <see cref="Controllers" />, which is the flat wire id space --
    ///     not the entity-type ids in <c>AeroMessages.Common.Controller</c>.
    /// </summary>
    public static string ControllerName(byte controllerId)
        => Enum.IsDefined(typeof(Controllers), controllerId)
               ? ((Controllers)controllerId).ToString()
               : $"Unknown_0x{controllerId:X2}";
}
