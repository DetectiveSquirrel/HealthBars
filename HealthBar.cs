using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using SharpDX;

namespace HealthBars
{
    public class DebuffPanelConfig
    {
        public Dictionary<string, int> Bleeding { get; set; }
        public Dictionary<string, int> Corruption { get; set; }
        public Dictionary<string, int> Poisoned { get; set; }
        public Dictionary<string, int> Frozen { get; set; }
        public Dictionary<string, int> Chilled { get; set; }
        public Dictionary<string, int> Burning { get; set; }
        public Dictionary<string, int> Shocked { get; set; }
        public Dictionary<string, int> WeakenedSlowed { get; set; }
    }

    public class HealthBar
    {
        private const int DPS_CHECK_TIME = 1000;
        private const int DPS_FAST_CHECK_TIME = 200;
        private const int DPS_POP_TIME = 2000;
        private static readonly List<string> IgnoreEntitiesList = new List<string> {"MonsterFireTrap2", "MonsterBlastRainTrap", "Volatile"};
        private readonly Stopwatch dpsStopwatch = Stopwatch.StartNew();
        private readonly TimeCache<float> _distance;
        private bool _init;
        private int _lastHp;
        public RectangleF BackGround;
        public bool CanNotDie;
        public double DiedFrames = 0;
        private bool isHostile;
        private readonly Action OnHostileChange = delegate { };
        public bool Skip = false;

        public HealthBar(Entity entity, HealthBarsSettings settings)
        {
            Entity = entity;
            _distance = new TimeCache<float>(() => entity.DistancePlayer, 200);

            // If ignored entity found, skip
            foreach (var _entity in IgnoreEntitiesList)
            {
                if (entity.Path.Contains(_entity))
                    return;
            }

            Update(entity, settings);

            //CanNotDie = entity.GetComponent<Stats>().StatDictionary.ContainsKey(GameStat.CannotDie);
            CanNotDie = entity.Path.StartsWith("Metadata/Monsters/Totems/Labyrinth");

            if (entity.HasComponent<ObjectMagicProperties>() && entity.GetComponent<ObjectMagicProperties>().Mods.Contains("MonsterConvertsOnDeath_"))
            {
                OnHostileChange = () =>
                {
                    if (_init) Update(Entity, settings);
                };
            }
        }

        public bool IsHostile
        {
            get
            {
                var entityIsHostile = Entity.IsHostile;

                if (isHostile != entityIsHostile)
                {
                    isHostile = entityIsHostile;
                    OnHostileChange?.Invoke();
                }

                return entityIsHostile;
            }
        }
        public float HpPercent => Life != null ? Life.HPPercentage : 0;
        public float Distance => _distance.Value;
        public Life Life => Entity.HasComponent<Life>() ? Entity.GetComponent<Life>() : null;
        public Entity Entity { get; }
        public UnitSettings Settings { get; private set; }
        public CreatureType Type { get; private set; }
        public LinkedList<int> DpsQueue { get; } = new LinkedList<int>();

        public Color Color
        {
            get
            {
                if (IsHidden(Entity))
                    return Color.LightGray;

                if (HpPercent <= 0.1f)
                    return Settings.Under10Percent;

                return Settings.Color;
            }
        }

        private bool IsHidden(Entity entity)
        {
            try
            {
                return entity.IsHidden;
            }
            catch
            {
                return false;
            }
        }



        public float HpWidth { get; set; }
        public float EsWidth { get; set; }

        public void Update(Entity entity, HealthBarsSettings settings)
        {
            if (entity.HasComponent<Player>())
            {
                Type = CreatureType.Player;
                Settings = settings.Players;
            }
            else if (entity.HasComponent<Monster>())
            {
                var objectMagicProperties = entity.GetComponent<ObjectMagicProperties>();
                if (entity.IsHostile && objectMagicProperties != null)
                {
                    switch (objectMagicProperties.Rarity)
                    {
                        case MonsterRarity.White:
                            Type = CreatureType.Normal;
                            Settings = settings.NormalEnemy;
                            break;

                        case MonsterRarity.Magic:
                            Type = CreatureType.Magic;
                            Settings = settings.MagicEnemy;
                            break;

                        case MonsterRarity.Rare:
                            Settings = settings.RareEnemy;
                            Type = CreatureType.Rare;
                            break;

                        case MonsterRarity.Unique:
                            Settings = settings.UniqueEnemy;
                            Type = CreatureType.Unique;
                            break;
                        default:
                            Settings = settings.Minions;
                            Type = CreatureType.Minion;
                            break;
                    }
                }
                else
                {
                    Type = CreatureType.Minion;
                    Settings = settings.Minions;
                }
            }

            _lastHp = GetFullHp();
            _init = true;
        }

        public bool IsShow(bool showEnemy)
        {
            if (Settings == null)
                return false;

            return !IsHostile ? Settings.Enable.Value : Settings.Enable.Value && showEnemy && IsHostile;
        }

        public void DpsRefresh()
        {
            var checkTime = DpsQueue.Count > 0 ? DPS_CHECK_TIME : DPS_FAST_CHECK_TIME;

            if (dpsStopwatch.ElapsedMilliseconds >= checkTime)
            {
                var hp = GetFullHp();

                if (hp > -1000000 && hp < 10000000 && _lastHp != hp)
                {
                    DpsQueue.AddFirst(-(_lastHp - hp));

                    if (DpsQueue.Count > Settings.FloatingCombatStackSize)
                    {
                        DpsQueue.RemoveLast();
                        dpsStopwatch.Restart();
                    }

                    _lastHp = hp;
                }
            }
        }

        public void DpsDequeue()
        {
            if (dpsStopwatch.ElapsedMilliseconds >= DPS_POP_TIME)
            {
                if (DpsQueue.Count > 0) DpsQueue.RemoveLast();
                dpsStopwatch.Restart();
            }
        }

        private int GetFullHp()
        {
            if (Life == null) return 0;
            return Life.CurHP + Life.CurES;
        }
    }
}
