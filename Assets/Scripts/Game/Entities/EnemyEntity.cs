// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Player;

namespace DaggerfallWorkshop.Game.Entity
{
    /// <summary>
    /// Implements DaggerfallEntity with properties specific to enemies.
    /// Currently enemy setup is bridging between old "demo" components and newer "game" systems.
    /// TODO: Migrate completely to "game" methods and simplify enemy instantiation and setup.
    /// </summary>
    public class EnemyEntity : DaggerfallEntity
    {
        #region Fields

        int careerIndex = -1;
        EntityTypes entityType = EntityTypes.None;
        MobileEnemy mobileEnemy;
        bool pickpocketByPlayerAttempted = false;

        // From FALL.EXE offset 0x1C0F14
        static byte[] ImpSpells            = { 0x07, 0x0A, 0x1D, 0x2C };
        static byte[] GhostSpells          = { 0x22 };
        static byte[] OrcShamanSpells      = { 0x06, 0x07, 0x16, 0x19, 0x1F };
        static byte[] WraithSpells         = { 0x1C, 0x1F };
        static byte[] FrostDaedraSpells    = { 0x10, 0x14 };
        static byte[] FireDaedraSpells     = { 0x0E, 0x19 };
        static byte[] DaedrothSpells       = { 0x16, 0x17, 0x1F };
        static byte[] VampireSpells        = { 0x33 };
        static byte[] SeducerSpells        = { 0x34, 0x43 };
        static byte[] VampireAncientSpells = { 0x08, 0x32 };
        static byte[] DaedraLordSpells     = { 0x08, 0x0A, 0x0E, 0x3C, 0x43 };
        static byte[] LichSpells           = { 0x08, 0x0A, 0x0E, 0x22, 0x3C };
        static byte[] AncientLichSpells    = { 0x08, 0x0A, 0x0E, 0x1D, 0x1F, 0x22, 0x3C };
        static byte[][] EnemyClassSpells   = { FrostDaedraSpells, DaedrothSpells, OrcShamanSpells, VampireAncientSpells, DaedraLordSpells, LichSpells, AncientLichSpells };

        #endregion

        #region Properties

        public EntityTypes EntityType
        {
            get { return entityType; }
        }

        public int CareerIndex
        {
            get { return careerIndex; }
        }

        public MobileEnemy MobileEnemy
        {
            get { return mobileEnemy; }
        }

        public bool PickpocketByPlayerAttempted
        {
            get { return (pickpocketByPlayerAttempted); }
            set { pickpocketByPlayerAttempted = value; }
        }

        #endregion

        #region Constructors

        public EnemyEntity(DaggerfallEntityBehaviour entityBehaviour)
            : base(entityBehaviour)
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Assigns default entity settings.
        /// </summary>
        public override void SetEntityDefaults()
        {
        }

        /// <summary>
        /// Sets enemy career and prepares entity settings.
        /// </summary>
        public void SetEnemyCareer(MobileEnemy mobileEnemy, EntityTypes entityType)
        {
            if (entityType == EntityTypes.EnemyMonster)
            {
                careerIndex = (int)mobileEnemy.ID;
                career = GetMonsterCareerTemplate((MonsterCareers)careerIndex);
                stats.SetPermanentFromCareer(career);

                // Enemy monster has predefined level, health and armor values.
                // Armor values can be modified below by equipment.
                level = mobileEnemy.Level;
                maxHealth = UnityEngine.Random.Range(mobileEnemy.MinHealth, mobileEnemy.MaxHealth + 1);
                for (int i = 0; i < ArmorValues.Length; i++)
                {
                    ArmorValues[i] = (sbyte)(mobileEnemy.ArmorValue * 5);
                }
            }
            else if (entityType == EntityTypes.EnemyClass)
            {
                careerIndex = (int)mobileEnemy.ID - 128;
                career = GetClassCareerTemplate((ClassCareers)careerIndex);
                stats.SetPermanentFromCareer(career);

                // Enemy class is levelled to player and uses similar health rules
                // City guards are 3 to 6 levels above the player
                level = GameManager.Instance.PlayerEntity.Level;
                if (careerIndex == (int)MobileTypes.Knight_CityWatch)
                    level += UnityEngine.Random.Range(3, 7);

                maxHealth = FormulaHelper.RollEnemyClassMaxHealth(level, career.HitPointsPerLevel);
            }
            else
            {
                career = new DFCareer();
                careerIndex = -1;
                return;
            }

            this.mobileEnemy = mobileEnemy;
            this.entityType = entityType;
            name = career.Name;
            minMetalToHit = mobileEnemy.MinMetalToHit;

            short skillsLevel = (short)((level * 5) + 30);
            if (skillsLevel > 100)
            {
                skillsLevel = 100;
            }

            for (int i = 0; i <= DaggerfallSkills.Count; i++)
            {
                skills.SetPermanentSkillValue(i, skillsLevel);
            }

            // Generate loot table items
            DaggerfallLoot.GenerateItems(mobileEnemy.LootTableKey, items);

            // Enemy classes and some monsters use equipment
            if (careerIndex == (int)MonsterCareers.Orc || careerIndex == (int)MonsterCareers.OrcShaman)
            {
                SetEnemyEquipment(0);
            }
            else if (careerIndex == (int)MonsterCareers.Centaur || careerIndex == (int)MonsterCareers.OrcSergeant)
            {
                SetEnemyEquipment(1);
            }
            else if (careerIndex == (int)MonsterCareers.OrcWarlord)
            {
                SetEnemyEquipment(2);
            }
            else if (entityType == EntityTypes.EnemyClass)
            {
                SetEnemyEquipment(UnityEngine.Random.Range(0, 2)); // 0 or 1
            }

            // Assign spell lists
            if (careerIndex == (int)MonsterCareers.Imp)
                SetEnemySpells(ImpSpells);
            else if (careerIndex == (int)MonsterCareers.Ghost)
                SetEnemySpells(GhostSpells);
            else if (careerIndex == (int)MonsterCareers.OrcShaman)
                SetEnemySpells(OrcShamanSpells);
            else if (careerIndex == (int)MonsterCareers.Wraith)
                SetEnemySpells(WraithSpells);
            else if (careerIndex == (int)MonsterCareers.FrostDaedra)
                SetEnemySpells(FrostDaedraSpells);
            else if (careerIndex == (int)MonsterCareers.FireDaedra)
                SetEnemySpells(FireDaedraSpells);
            else if (careerIndex == (int)MonsterCareers.Daedroth)
                SetEnemySpells(DaedrothSpells);
            else if (careerIndex == (int)MonsterCareers.Vampire)
                SetEnemySpells(VampireSpells);
            else if (careerIndex == (int)MonsterCareers.DaedraSeducer)
                SetEnemySpells(SeducerSpells);
            else if (careerIndex == (int)MonsterCareers.VampireAncient)
                SetEnemySpells(VampireAncientSpells);
            else if (careerIndex == (int)MonsterCareers.DaedraLord)
                SetEnemySpells(DaedraLordSpells);
            else if (careerIndex == (int)MonsterCareers.Lich)
                SetEnemySpells(LichSpells);
            else if (careerIndex == (int)MonsterCareers.AncientLich)
                SetEnemySpells(AncientLichSpells);
            else if (entityType == EntityTypes.EnemyClass && mobileEnemy.CastsMagic)
            {
                int spellListLevel = level / 3;
                if (spellListLevel > 6)
                    spellListLevel = 6;
                SetEnemySpells(EnemyClassSpells[spellListLevel]);
            }

            // Chance of adding map
            DaggerfallLoot.RandomlyAddMap(mobileEnemy.MapChance, items);

            if (!string.IsNullOrEmpty(mobileEnemy.LootTableKey))
            {
                // Chance of adding potion
                DaggerfallLoot.RandomlyAddPotion(3, items);
                // Chance of adding potion recipe
                DaggerfallLoot.RandomlyAddPotionRecipe(2, items);
            }

            FillVitalSigns();
        }

        public void SetEnemyEquipment(int variant)
        {
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            int itemLevel = player.Level;
            Genders gender = player.Gender;
            Races race = player.Race;
            int chance = 0;
            if (variant == 0)
            {
                // right-hand weapon
                int item = UnityEngine.Random.Range((int)Game.Items.Weapons.Broadsword, (int)(Game.Items.Weapons.Longsword) + 1);
                Items.DaggerfallUnityItem weapon = Game.Items.ItemBuilder.CreateWeapon((Items.Weapons)item, Game.Items.ItemBuilder.RandomMaterial(itemLevel));
                ItemEquipTable.EquipItem(weapon, true, false);
                items.AddItem(weapon);

                chance = 50;

                // left-hand shield
                item = UnityEngine.Random.Range((int)Game.Items.Armor.Buckler, (int)(Game.Items.Armor.Round_Shield) + 1);
                if (UnityEngine.Random.Range(1, 101) <= chance)
                {
                    Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, (Items.Armor)item, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                    ItemEquipTable.EquipItem(armor, true, false);
                    items.AddItem(armor);
                }
                // left-hand weapon
                else if (UnityEngine.Random.Range(1, 101) <= chance)
                {
                    item = UnityEngine.Random.Range((int)Game.Items.Weapons.Dagger, (int)(Game.Items.Weapons.Shortsword) + 1);
                    weapon = Game.Items.ItemBuilder.CreateWeapon((Items.Weapons)item, Game.Items.ItemBuilder.RandomMaterial(itemLevel));
                    ItemEquipTable.EquipItem(weapon, true, false);
                    items.AddItem(weapon);
                }
            }
            else
            {
                // right-hand weapon
                int item = UnityEngine.Random.Range((int)Game.Items.Weapons.Claymore, (int)(Game.Items.Weapons.Battle_Axe) + 1);
                Items.DaggerfallUnityItem weapon = Game.Items.ItemBuilder.CreateWeapon((Items.Weapons)item, Game.Items.ItemBuilder.RandomMaterial(itemLevel));
                ItemEquipTable.EquipItem(weapon, true, false);
                items.AddItem(weapon);

                if (variant == 1)
                    chance = 75;
                else if (variant == 2)
                    chance = 90;
            }
            // helm
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Helm, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }
            // right pauldron
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Right_Pauldron, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }
            // left pauldron
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Left_Pauldron, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }
            // cuirass
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Cuirass, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }
            // greaves
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Greaves, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }
            // boots
            if (UnityEngine.Random.Range(1, 101) <= chance)
            {
                Items.DaggerfallUnityItem armor = Game.Items.ItemBuilder.CreateArmor(gender, race, Game.Items.Armor.Boots, Game.Items.ItemBuilder.RandomArmorMaterial(itemLevel));
                ItemEquipTable.EquipItem(armor, true, false);
                items.AddItem(armor);
            }

            // Initialize armor values to 100 (no armor)
            for (int i = 0; i < ArmorValues.Length; i++)
            {
                ArmorValues[i] = 100;
            }
            // Calculate armor values from equipment
            for (int i = (int)Game.Items.EquipSlots.Head; i < (int)Game.Items.EquipSlots.Feet; i++)
            {
                Items.DaggerfallUnityItem item = ItemEquipTable.GetItem((Items.EquipSlots)i);
                if (item != null && item.ItemGroup == Game.Items.ItemGroups.Armor)
                {
                    UpdateEquippedArmorValues(item, true);
                }
            }

            if (entityType == EntityTypes.EnemyClass)
            {
                // Clamp to maximum armor value of 60. In classic this also applies for monsters.
                // Note: Classic sets the value to 60 if it is > 50, which seems like an oversight.
                for (int i = 0; i < ArmorValues.Length; i++)
                {
                    if (ArmorValues[i] > 60)
                    {
                        ArmorValues[i] = 60;
                    }
                }
            }
            else
            {
                // Note: In classic, the above applies for equipment-using monsters as well as enemy classes.
                // The resulting armor values are often 60. Due to the +40 to hit against monsters this makes
                // monsters with equipment very easy to hit, and 60 is a worse value than any value monsters
                // have in their definition. To avoid this, in DF Unity the equipment values are only used if
                // they are better than the value in the definition.
                for (int i = 0; i < ArmorValues.Length; i++)
                {
                    if (ArmorValues[i] > (sbyte)(mobileEnemy.ArmorValue * 5))
                    {
                        ArmorValues[i] = (sbyte)(mobileEnemy.ArmorValue * 5);
                    }
                }
            }

            // Chance for poisoned weapon
            if (player.Level > 1)
            {
                Items.DaggerfallUnityItem weapon = ItemEquipTable.GetItem(Game.Items.EquipSlots.RightHand);
                if (weapon != null && (entityType == EntityTypes.EnemyClass || mobileEnemy.ID == (int)MobileTypes.Orc
                        || mobileEnemy.ID == (int)MobileTypes.Centaur || mobileEnemy.ID == (int)MobileTypes.OrcSergeant))
                {
                    int chanceToPoison = 5;
                    if (mobileEnemy.ID == (int)MobileTypes.Assassin)
                        chanceToPoison = 60;

                    if (UnityEngine.Random.Range(1, 101) < chanceToPoison)
                    {
                        // Apply poison
                        weapon.poisonType = (Items.Poisons)UnityEngine.Random.Range(128, 136);
                    }
                }
            }
        }

        public void SetEnemySpells(byte[] spellList)
        {
            //MaxMagicka = 10 * level + 100; TODO: Enemies should be able to set maximum spell points independent of the rules used by the player.
            currentMagicka = MaxMagicka;
            skills.SetPermanentSkillValue(DFCareer.Skills.Destruction, 80);
            skills.SetPermanentSkillValue(DFCareer.Skills.Restoration, 80);
            skills.SetPermanentSkillValue(DFCareer.Skills.Illusion, 80);
            skills.SetPermanentSkillValue(DFCareer.Skills.Alteration, 80);
            skills.SetPermanentSkillValue(DFCareer.Skills.Thaumaturgy, 80);
            skills.SetPermanentSkillValue(DFCareer.Skills.Mysticism, 80);

            // Iterate over spellList and assign data from SPELLS.STD

            return;
        }

        public DFCareer.EnemyGroups GetEnemyGroup()
        {
            switch (careerIndex)
            {
                case (int)MonsterCareers.Rat:
                case (int)MonsterCareers.GiantBat:
                case (int)MonsterCareers.GrizzlyBear:
                case (int)MonsterCareers.SabertoothTiger:
                case (int)MonsterCareers.Spider:
                case (int)MonsterCareers.Slaughterfish:
                case (int)MonsterCareers.GiantScorpion:
                case (int)MonsterCareers.Dragonling:
                case (int)MonsterCareers.Horse_Invalid:             // (grouped as undead in classic)
                case (int)MonsterCareers.Dragonling_Alternate:      // (grouped as undead in classic)
                    return DFCareer.EnemyGroups.Animals;
                case (int)MonsterCareers.Imp:
                case (int)MonsterCareers.Spriggan:
                case (int)MonsterCareers.Orc:
                case (int)MonsterCareers.Centaur:
                case (int)MonsterCareers.Werewolf:
                case (int)MonsterCareers.Nymph:
                case (int)MonsterCareers.OrcSergeant:
                case (int)MonsterCareers.Harpy:
                case (int)MonsterCareers.Wereboar:
                case (int)MonsterCareers.Giant:
                case (int)MonsterCareers.OrcShaman:
                case (int)MonsterCareers.Gargoyle:
                case (int)MonsterCareers.OrcWarlord:
                case (int)MonsterCareers.Dreugh:                    // (grouped as undead in classic)
                case (int)MonsterCareers.Lamia:                     // (grouped as undead in classic)
                    return DFCareer.EnemyGroups.Humanoid;
                case (int)MonsterCareers.SkeletalWarrior:
                case (int)MonsterCareers.Zombie:                    // (grouped as animal in classic)
                case (int)MonsterCareers.Ghost:
                case (int)MonsterCareers.Mummy:
                case (int)MonsterCareers.Wraith:
                case (int)MonsterCareers.Vampire:
                case (int)MonsterCareers.VampireAncient:
                case (int)MonsterCareers.Lich:
                case (int)MonsterCareers.AncientLich:
                    return DFCareer.EnemyGroups.Undead;
                case (int)MonsterCareers.FrostDaedra:
                case (int)MonsterCareers.FireDaedra:
                case (int)MonsterCareers.Daedroth:
                case (int)MonsterCareers.DaedraSeducer:
                case (int)MonsterCareers.DaedraLord:
                    return DFCareer.EnemyGroups.Daedra;
                case (int)MonsterCareers.FireAtronach:
                case (int)MonsterCareers.IronAtronach:
                case (int)MonsterCareers.FleshAtronach:
                case (int)MonsterCareers.IceAtronach:
                    return DFCareer.EnemyGroups.None;

                default:
                    return DFCareer.EnemyGroups.None;
            }
        }

        public DFCareer.Skills GetLanguageSkill()
        {
            if (entityType == EntityTypes.EnemyClass)
            {
                switch (careerIndex)
                {   // BCHG: classic uses Ettiquette for all
                    case (int)ClassCareers.Burglar:
                    case (int)ClassCareers.Rogue:
                    case (int)ClassCareers.Acrobat:
                    case (int)ClassCareers.Thief:
                    case (int)ClassCareers.Assassin:
                    case (int)ClassCareers.Nightblade:
                        return DFCareer.Skills.Streetwise;
                    default:
                        return DFCareer.Skills.Etiquette;
                }
            }

            switch (careerIndex)
            {
                case (int)MonsterCareers.Orc:
                case (int)MonsterCareers.OrcSergeant:
                case (int)MonsterCareers.OrcShaman:
                case (int)MonsterCareers.OrcWarlord:
                    return DFCareer.Skills.Orcish;

                case (int)MonsterCareers.Harpy:
                    return DFCareer.Skills.Harpy;

                case (int)MonsterCareers.Giant:
                case (int)MonsterCareers.Gargoyle:
                    return DFCareer.Skills.Giantish;

                case (int)MonsterCareers.Dragonling:
                case (int)MonsterCareers.Dragonling_Alternate:
                    return DFCareer.Skills.Dragonish;

                case (int)MonsterCareers.Nymph:
                case (int)MonsterCareers.Lamia:
                    return DFCareer.Skills.Nymph;

                case (int)MonsterCareers.FrostDaedra:
                case (int)MonsterCareers.FireDaedra:
                case (int)MonsterCareers.Daedroth:
                case (int)MonsterCareers.DaedraSeducer:
                case (int)MonsterCareers.DaedraLord:
                    return DFCareer.Skills.Daedric;

                case (int)MonsterCareers.Spriggan:
                    return DFCareer.Skills.Spriggan;

                case (int)MonsterCareers.Centaur:
                    return DFCareer.Skills.Centaurian;

                case (int)MonsterCareers.Imp:
                case (int)MonsterCareers.Dreugh:
                    return DFCareer.Skills.Impish;

                case (int)MonsterCareers.Vampire:
                case (int)MonsterCareers.VampireAncient:
                case (int)MonsterCareers.Lich:
                case (int)MonsterCareers.AncientLich:
                    return DFCareer.Skills.Etiquette;

                default:
                    return DFCareer.Skills.None;
            }
        }

        public int GetWeightInClassicUnits()
        {
            int itemWeightsClassic = (int)(Items.GetWeight() * 4);
            int baseWeight;

            if (entityType == EntityTypes.EnemyMonster)
                baseWeight = mobileEnemy.Weight;
            else if (mobileEnemy.Gender == MobileGender.Female)
                baseWeight = 240;
            else
                baseWeight = 350;

            return itemWeightsClassic + baseWeight;
        }

        #endregion
    }
}