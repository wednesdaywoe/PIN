using System;
using System.Collections.Generic;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Data;

public static class HardcodedCharacterData
{
    public static string ArmyTag = "ARMY";
    public static ulong ArmyGUID = 1u;
    public static int SelectedLoadout = 184538131;
    public static byte Level = 45;
    public static byte EffectiveLevel = 45;

    // 19192 is what the 2016 capture's player entity reported, so this one is a reading rather than a guess.
    // Don't "fix" it from dbitems::Battleframe.base_health, which is ~1000 — that column is the pre-scaling
    // base, and the level and attribute scaling that turns it into 19192 isn't implemented.
    public static int MaxHealth = 19192;
    public static int MonsterMaxHealth = 2500;

    // The dump happened (MinimalSDB dump, prod-1962): build 1962 had no shields. base_shields is non-zero on
    // 5 of 1676 Battleframe rows, and the 2016 capture agrees — MaxShields reads 0 across every
    // Character_BaseController message for the player. So the pool below is a deliberate divergence kept
    // because the absorb-with-overflow path in TakeDamage is worth being able to see; 3000 at least appears
    // in the table, unlike the 4800 it replaces. Set it to 0 to match retail exactly.
    // The recharge pair is not invented: 150/sec and 10000ms are the values ~780 rows actually shipped with.
    // Monsters stay shieldless so the divergence only lands on players.
    public static int MaxShields = 3000;
    public static int MonsterMaxShields = 0;
    public static int ShieldRechargePerSec = 150;
    public static int ShieldRechargeDelayMs = 10000;
    public static int GeneratedLoadoutCounter = 20001;
    public static HashSet<uint> HostileFactionIds = [2, 3, 5, 6, 7, 8, 17, 22, 42, 43, 45, 46, 47, 48];

    public static BasicCharacterData FallbackData = new()
    {
        CharacterInfo = new BasicCharacterInfo()
        {
            Name = "Fallback",
            Gender = (uint)CharacterGender.Male,
            Race = (uint)CharacterRace.Human,
            TitleId = 135,
            CurrentBattleframeSDBId = 76331,
            ArmyTag = ArmyTag,
            ArmyGuid = ArmyGUID,
            ArmyIsOfficer = true,
        },
        CharacterVisuals = new BasicCharacterVisuals()
        {
            Head = 10002,
            Eyes = 0,
            VoiceSet = 1000,
            Vehicle = 1000,
            Glider = 1000,
            HeadAccessories = [10089, 10106],
            Ornaments = [10224, 10270, 10061],

            SkinColor = 0x52680000u,
            EyeColor = 0x6a2440e0u,
            LipColor = 0xffff0000u,
            HairColor = 0x320D0021u,
            FacialHairColor = 0x320D0021u,
        }
    };

    public static Dictionary<uint, uint> TempCharCreateLoadouts = new()
    {
        // Accord
        { 287, 75772 }, // Dreadnaught
        { 286, 76164 }, // Assault
        { 288, 75774 }, // Biotech
        { 289, 75775 }, // Engineer
        { 290, 75773 }, // Recon

        // Advanced
        { 299, 76133 }, // Firecat
        { 300, 76132 }, // Tigerclaw
        { 295, 76337 }, // Electron
        { 296, 76338 }, // Bastion
        { 294, 76331 }, // Mammoth
        { 293, 76332 }, // Rhino
        { 297, 76335 }, // Dragonfly
        { 298, 76336 }, // Recluse
        { 291, 76333 }, // Nighthawk
        { 292, 76334 }, // Raptor

        // Advanced 2
        { 47, 82359 }, // Graviton
        { 48, 82360 }, // Arsenal
        { 49, 82394 }, // Archangel

        // Social
        { 246, 124356 }, // Beach Party
        { 247, 77733 }, // BattleLab Trainee
    };

    public static List<LoadoutReferenceData> TempHardcodedLoadouts = 
    [
        /*
        // Accord
        new LoadoutReferenceData
        {
            ChassisId = 75772, // Dreadnaught
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 86851 },
                { LoadoutSlotType.Secondary, 87800 },
                { LoadoutSlotType.AbilityHKM, 125199 },
                { LoadoutSlotType.Ability1, 90493 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 75873 },
                { LoadoutSlotType.GearTorso, 126000 },
                { LoadoutSlotType.GearAuxWeapon, 129505 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 127501 },
                { LoadoutSlotType.GearLegs, 128271 },
                { LoadoutSlotType.GearReactor, 126731 },
                { LoadoutSlotType.GearOS, 129067 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76164, // Assault
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 86742 },
                { LoadoutSlotType.Secondary, 87741 },
                { LoadoutSlotType.AbilityHKM, 88491 },
                { LoadoutSlotType.Ability1, 88039 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 75877 },
                { LoadoutSlotType.GearTorso, 126000 },
                { LoadoutSlotType.GearAuxWeapon, 129213 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 127501 },
                { LoadoutSlotType.GearLegs, 128271 },
                { LoadoutSlotType.GearReactor, 126731 },
                { LoadoutSlotType.GearOS, 129067 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 75774, // Biotech
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 87028 },
                { LoadoutSlotType.Secondary, 87918 },
                { LoadoutSlotType.AbilityHKM, 89124 },
                { LoadoutSlotType.Ability1, 123271 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 75874 },
                { LoadoutSlotType.GearTorso, 126000 },
                { LoadoutSlotType.GearAuxWeapon, 129213 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 127501 },
                { LoadoutSlotType.GearLegs, 128271 },
                { LoadoutSlotType.GearReactor, 126731 },
                { LoadoutSlotType.GearOS, 129067 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 75775, // Engineer
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 87386 },
                { LoadoutSlotType.Secondary, 87978 },
                { LoadoutSlotType.AbilityHKM, 91559 },
                { LoadoutSlotType.Ability1, 91394 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 31344 },
                { LoadoutSlotType.GearTorso, 126000 },
                { LoadoutSlotType.GearAuxWeapon, 129359 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 127501 },
                { LoadoutSlotType.GearLegs, 128271 },
                { LoadoutSlotType.GearReactor, 126731 },
                { LoadoutSlotType.GearOS, 129067 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 75773, // Recon
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 86969 },
                { LoadoutSlotType.Secondary, 87918 },
                { LoadoutSlotType.AbilityHKM, 91770 },
                { LoadoutSlotType.Ability1, 91662 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 75876 },
                { LoadoutSlotType.GearTorso, 126000 },
                { LoadoutSlotType.GearAuxWeapon, 129359 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 127501 },
                { LoadoutSlotType.GearLegs, 128271 },
                { LoadoutSlotType.GearReactor, 126731 },
                { LoadoutSlotType.GearOS, 129067 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        

        // Advanced
        new LoadoutReferenceData
        {
            ChassisId = 76133, // Firecat
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 78448 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76132, // Tigerclaw
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 76022 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76337, // Electron
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 75875 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76338, // Bastion
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 76020 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76331, // Mammoth
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 134616 },
                { LoadoutSlotType.Secondary, 114316 },
                { LoadoutSlotType.AbilityHKM, 113931 },
                { LoadoutSlotType.Ability1, 143330 },
                { LoadoutSlotType.Ability2, 136056 },
                { LoadoutSlotType.Ability3, 113552 },
                { LoadoutSlotType.Backpack, 78041 },
                { LoadoutSlotType.GearTorso, 126575 },
                { LoadoutSlotType.GearAuxWeapon, 129458 },
                { LoadoutSlotType.GearMedicalSystem, 129056 },
                { LoadoutSlotType.GearHead, 125845 },
                { LoadoutSlotType.GearArms, 128036 },
                { LoadoutSlotType.GearLegs, 128766 },
                { LoadoutSlotType.GearReactor, 127306 },
                { LoadoutSlotType.GearOS, 129202 },
                { LoadoutSlotType.GearGadget1, 142078 },
                { LoadoutSlotType.GearGadget2, 130419 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76332, // Rhino
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 76018 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76335, // Dragonfly
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 78449 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76336, // Recluse
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 76019 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76333, // Nighthawk
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 76021 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        new LoadoutReferenceData
        {
            ChassisId = 76334, // Raptor
            SlottedItemsPvE = new Dictionary<LoadoutSlotType, uint>()
            {
                { LoadoutSlotType.Primary, 0 },
                { LoadoutSlotType.Secondary, 0 },
                { LoadoutSlotType.AbilityHKM, 0 },
                { LoadoutSlotType.Ability1, 0 },
                { LoadoutSlotType.Ability2, 0 },
                { LoadoutSlotType.Ability3, 0 },
                { LoadoutSlotType.Backpack, 78040 },
                { LoadoutSlotType.GearTorso, 0 },
                { LoadoutSlotType.GearAuxWeapon, 0 },
                { LoadoutSlotType.GearMedicalSystem, 0 },
                { LoadoutSlotType.GearHead, 0 },
                { LoadoutSlotType.GearArms, 0 },
                { LoadoutSlotType.GearLegs, 0 },
                { LoadoutSlotType.GearReactor, 0 },
                { LoadoutSlotType.GearOS, 0 },
                { LoadoutSlotType.GearGadget1, 0 },
                { LoadoutSlotType.GearGadget2, 0 },
            }
        },
        */
    ];

    public static void GenerateCharCreateLoadoutAndItems(CharacterInventory inventory, uint charCreateLoadoutId, uint chassisId)
    {
        LoadoutReferenceData refData = new()
        {
            ChassisId = chassisId,
        };

        Dictionary<byte, CharCreateLoadoutSlots> defaultSlots = SDBUtils.GetDefaultLoadoutSlots(charCreateLoadoutId);

        if (defaultSlots != null)
        {
            foreach (LoadoutSlotType slot in defaultSlots.Keys)
            {
                if (CharacterLoadout.LoadoutAbilitySlots.Contains(slot) || CharacterLoadout.LoadoutChassisSlots.Contains(slot) || CharacterLoadout.LoadoutWeaponSlots.Contains(slot))
                {
                    CharCreateLoadoutSlots record = defaultSlots.GetValueOrDefault((byte)slot);
                    if (record.DefaultPveModule != 0)
                    {
                        refData.SlottedItemsPvE.Add(slot, record.DefaultPveModule);
                    }

                    if (record.DefaultPvpModule != 0)
                    {
                        refData.SlottedItemsPvP.Add(slot, record.DefaultPvpModule);
                    }
                }
            }
        }

        GenerateLoadoutAndItems(inventory, refData);
    }

    public static void GenerateLoadoutAndItems(CharacterInventory inventory, LoadoutReferenceData sourceData)
    {
        var loadoutId = sourceData.LoadoutId == 0 ? GeneratedLoadoutCounter++ : sourceData.LoadoutId;
        var loadout = new Loadout()
        {
            FrameLoadoutId = loadoutId,
            ChassisID = sourceData.ChassisId,
            LoadoutName = $"Loadout {loadoutId}",
            LoadoutType = "battleframe"
        };

        var chassisGuid = inventory.CreateItem(sourceData.ChassisId);

        var pveConfig = new LoadoutConfig()
        {
            ConfigID = 0,
            ConfigName = "pve",
            Items = [],
            Visuals = [],
            Perks = [],
            Unk1 = 0,
            PerkBandwidth = 0,
            PerkRespecLockRemainingSeconds = 0,
            HaveExtraData = 0
        };

        var pveItems = new List<LoadoutConfig_Item>();
        foreach (var (slot, typeId) in sourceData.SlottedItemsPvE)
        {
            if (typeId == 0)
            {
                continue;
            }

            var guid = inventory.CreateItem(typeId);
            pveItems.Add(new LoadoutConfig_Item() { ItemGUID = guid, SlotIndex = (byte)slot });
        }

        pveConfig.Items = [.. pveItems];

        var pvpConfig = new LoadoutConfig()
        {
            ConfigID = 1,
            ConfigName = "pvp",
            Items = [],
            Visuals = [],
            Perks = [],
            Unk1 = 0,
            PerkBandwidth = 0,
            PerkRespecLockRemainingSeconds = 0,
            HaveExtraData = 0
        };

        var pvpItems = new List<LoadoutConfig_Item>();
        foreach (var (slot, typeId) in sourceData.SlottedItemsPvP)
        {
            if (typeId == 0)
            {
                continue;
            }

            var guid = inventory.CreateItem(typeId);
            pvpItems.Add(new LoadoutConfig_Item() { ItemGUID = guid, SlotIndex = (byte)slot });
        }

        pvpConfig.Items = [.. pvpItems];

        loadout.LoadoutConfigs =
        [
            pveConfig,
            pvpConfig
        ];

        inventory.AddLoadout(loadout);
    }
}

public class BasicCharacterInfo
{
    public string Name { get; set; }
    public uint Gender { get; set; }
    public uint Race { get; set; }
    public ushort TitleId { get; set; }
    public uint CurrentBattleframeSDBId { get; set; }
    public string ArmyTag { get; set; }
    public ulong ArmyGuid { get; set; }
    public bool ArmyIsOfficer { get; set; }
    public int TimePlayed { get; set; }
}

public class BasicCharacterVisuals
{
    public uint Head { get; set; }
    public uint Eyes { get; set; }
    public uint VoiceSet { get; set; }
    public uint Vehicle { get; set; }
    public uint Glider { get; set; }
    public uint[] HeadAccessories { get; set; }
    public uint[] Ornaments { get; set; }
    public uint SkinColor { get; set; }
    public uint EyeColor { get; set; }
    public uint LipColor { get; set; }
    public uint HairColor { get; set; }
    public uint FacialHairColor { get; set; }
}

public class BasicCharacterData
{
    public BasicCharacterInfo CharacterInfo { get; set; }
    public BasicCharacterVisuals CharacterVisuals { get; set; }
}

public class LoadoutReferenceData
{
    public int LoadoutId;
    public uint ChassisId;
    public Dictionary<LoadoutSlotType, uint> SlottedItemsPvE = [];
    public Dictionary<LoadoutSlotType, uint> SlottedItemsPvP = [];
}