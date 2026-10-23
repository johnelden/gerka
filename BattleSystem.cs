using System.Collections.Generic;
using UnityEngine;

public class BattleSystem : MonoBehaviour
{
    public enum UnitType
    {
        Knight, Archer, Giant, Goblin,
        Guard, Crossbowman, Troll, Scout,
        Veteran, Hunter, Mage, GoblinSlinger, Skeleton,
        Cannon, Dragon, HogRider, Bat, MiniPekka
    }

    [Header("Movement")]
    public float BridgeX = 4.4f;
    public float RiverFarBankZ = 3.2f;
    public float BridgeNearBankZ = 2.55f;
    public float EnemyAggroRange = 3.0f;
    public float SeparationStrength = 1.25f;

    [Header("Deployment")]
    public float PlayerDeployMinZ = -14.4f;
    public float PlayerDeployMaxZ = -2.5f;
    public float DeployMinX = -7.8f;
    public float DeployMaxX = 7.8f;

    [Header("Enemy Spawning")]
    public int MaxLivingEnemies = 5;
    public float EnemyStartingElixir = 5f;
    public float EnemyElixirRegenSeconds = 2.5f;
    public float EnemyDeployMinZ = 3.0f;
    public float EnemyDeployMaxZ = 6.2f;
    public float EnemyDeployMinX = -7.2f;
    public float EnemyDeployMaxX = 7.2f;

    float enemyElixir;
    float enemyElixirRegenTimer;

    [Header("Visual Effects")]
    public float ProjectileSpeed = 10f;
    public float TowerProjectileSpeed = 12f;

    [Header("Arena Towers")]
    public float PrincessTowerX = 4.4f;
    public float PrincessTowerZ = 7.4f;
    public float KingTowerZ = 12.6f;

    class Unit
    {
        public GameObject root;
        public Transform healthBarRoot;
        public GameObject healthFill;
        public bool player;
        public bool crossedRiver;
        public bool ranged;
        public bool isBuilding;
        public bool flying;
        public bool buildingTargetsOnly;
        public bool targetTowerLocked;
        public Unit targetUnit;
        public Tower targetTower;
        public int routeStage; // 0 = approach, 1 = cross bridge, 2 = across
        public UnitType type;
        public float hp, maxHp, damage, speed, range, attackRate, attackTimer, radius;
        public float healthBarHeight;
        public float lifetime;
        public float flightHeight;
        public int lane;
        public GameObject weapon;
        public GameObject leftWing, rightWing;
        public float attackAnimTimer;
        public Vector3 previousPosition;
    }

    class Tower
    {
        public GameObject root;
        public Transform healthBarRoot;
        public GameObject healthFill;
        public bool player, king, destroyed;
        public float hp, maxHp, damage, range, attackRate, timer;
        public Vector3 position;
    }

    class Projectile
    {
        public GameObject root;
        public Unit targetUnit;
        public Tower targetTower;
        public Vector3 destination;
        public float damage, speed, life;
        public bool sourcePlayer, fireball, magical;
        public Material material;
    }

    class DamagePopup
    {
        public GameObject root;
        public TextMesh text;
        public TextMesh shadow;
        public Vector3 startPosition;
        public float remaining, duration;
    }

    class TemporaryEffect
    {
        public GameObject root;
        public float life;
    }

    readonly List<Unit> units = new List<Unit>();
    readonly List<Tower> towers = new List<Tower>();
    readonly List<Projectile> projectiles = new List<Projectile>();
    readonly List<TemporaryEffect> effects = new List<TemporaryEffect>();
    readonly List<DamagePopup> damagePopups = new List<DamagePopup>();

    ArenaSystem arena;
    GameManager manager;

    Material blue, red, stone, metal, gold, skin, dark, greenHp, yellowHp, redHp;
    Material blueProjectile, redProjectile, magicProjectile, fireballMaterial, impactMaterial, wood;

    public void Initialize(ArenaSystem arenaSystem, GameManager gameManager)
    {
        arena = arenaSystem;
        manager = gameManager;
        enemyElixir = EnemyStartingElixir;
        enemyElixirRegenTimer = 0f;
        CreateMaterials();

        // Towers are set farther apart to fit the extended arena.
        CreateTower(true, false, -PrincessTowerX, -PrincessTowerZ);
        CreateTower(true, false, PrincessTowerX, -PrincessTowerZ);
        CreateTower(true, true, 0f, -KingTowerZ);
        CreateTower(false, false, -PrincessTowerX, PrincessTowerZ);
        CreateTower(false, false, PrincessTowerX, PrincessTowerZ);
        CreateTower(false, true, 0f, KingTowerZ);
    }

    void CreateMaterials()
    {
        blue = Mat(new Color(0.12f, 0.38f, 0.86f));
        red = Mat(new Color(0.82f, 0.13f, 0.13f));
        stone = Mat(new Color(0.49f, 0.49f, 0.45f));
        metal = Mat(new Color(0.30f, 0.33f, 0.38f));
        gold = Mat(new Color(0.95f, 0.66f, 0.12f));
        skin = Mat(new Color(0.91f, 0.70f, 0.52f));
        dark = Mat(new Color(0.13f, 0.14f, 0.16f));
        greenHp = Mat(new Color(0.18f, 0.85f, 0.25f));
        yellowHp = Mat(new Color(1f, 0.72f, 0.12f));
        redHp = Mat(new Color(0.85f, 0.16f, 0.13f));
        blueProjectile = Mat(new Color(0.25f, 0.72f, 1f));
        redProjectile = Mat(new Color(1f, 0.27f, 0.18f));
        magicProjectile = Mat(new Color(0.72f, 0.30f, 1f));
        fireballMaterial = Mat(new Color(1f, 0.34f, 0.06f));
        impactMaterial = Mat(new Color(1f, 0.78f, 0.22f));
        wood = arena != null && arena.woodMaterial != null
            ? arena.woodMaterial
            : Mat(new Color(0.35f, 0.20f, 0.10f));
    }

    Material Mat(Color color)
    {
        // Standard is unavailable in URP projects and can render objects pink.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogError("BattleSystem: nie znaleziono zgodnego shadera do materiałów.");
            return null;
        }
        Material m = new Material(shader);
        m.color = color;
        return m;
    }

    public bool TryDeploy(UnitType type, Vector3 position, int count = 1)
    {
        if (type == UnitType.Cannon && CountCannons(true) >= 2)
            return false;
        if (!CanDeployAt(position, count))
            return false;

        count = Mathf.Clamp(count, 1, 5);
        SpawnUnitGroup(type, true, position, count);
        if (manager != null) manager.PlayDeploySound();
        return true;
    }

    public bool CanDeployAt(Vector3 position, int count = 1)
    {
        count = Mathf.Clamp(count, 1, 5);
        float formationCenter = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float memberX = position.x + (i - formationCenter) * 0.72f;
            if (memberX < DeployMinX || memberX > DeployMaxX ||
                position.z < PlayerDeployMinZ || position.z > PlayerDeployMaxZ)
                return false;
        }
        return true;
    }

    public int CountCannons(bool playerSide)
    {
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit.root != null && unit.type == UnitType.Cannon && unit.player == playerSide)
                count++;
        }
        return count;
    }

    public void SpawnEnemyWave()
    {
        // GameManager may request a wave every few seconds, but the enemy
        // must pay the same kind of elixir cost as the player.
        int livingEnemies = 0;
        for (int i = 0; i < units.Count; i++)
        {
            Unit existing = units[i];
            if (existing != null && existing.root != null && !existing.player)
                livingEnemies++;
        }

        if (livingEnemies >= MaxLivingEnemies)
            return;

        UnitType[] allTypes =
        {
            UnitType.Knight, UnitType.Archer, UnitType.Giant,
            UnitType.Crossbowman, UnitType.Mage, UnitType.Skeleton,
            UnitType.Cannon, UnitType.Dragon, UnitType.HogRider, UnitType.Bat, UnitType.MiniPekka
        };
        List<UnitType> affordable = new List<UnitType>();
        for (int i = 0; i < allTypes.Length; i++)
        {
            int unitCount = EnemyUnitCount(allTypes[i]);
            if (allTypes[i] == UnitType.Cannon && CountCannons(false) >= 2)
                continue;
            if (enemyElixir >= EnemyCost(allTypes[i]) &&
                livingEnemies + unitCount <= MaxLivingEnemies)
                affordable.Add(allTypes[i]);
        }

        if (affordable.Count == 0)
            return;

        // A single unit per wave call prevents endless swarms.
        UnitType type = ChooseEnemyUnit(affordable);
        Vector3 spawnPosition = FindEnemySpawnPosition();
        int spawnCount = EnemyUnitCount(type);
        enemyElixir -= EnemyCost(type);
        SpawnUnitGroup(type, false, spawnPosition, spawnCount);
    }

    float EnemyCost(UnitType type)
    {
        switch (type)
        {
            case UnitType.Goblin: return 2f;
            case UnitType.Knight: return 3f;
            case UnitType.Archer: return 3f;
            case UnitType.Giant: return 5f;
            case UnitType.Guard: return 3f;
            case UnitType.Crossbowman: return 4f;
            case UnitType.Troll: return 4f;
            case UnitType.Scout: return 2f;
            case UnitType.Veteran: return 4f;
            case UnitType.Hunter: return 4f;
            case UnitType.Mage: return 4f;
            case UnitType.GoblinSlinger: return 2f;
            case UnitType.Skeleton: return 2f;
            case UnitType.Cannon: return 3f;
            case UnitType.Dragon: return 4f;
            case UnitType.HogRider: return 4f;
            case UnitType.Bat: return 2f;
            case UnitType.MiniPekka: return 4f;
            default: return 3f;
        }
    }

    int EnemyUnitCount(UnitType type)
    {
        if (type == UnitType.Archer) return 2;
        if (type == UnitType.Skeleton) return 3;
        if (type == UnitType.Bat) return 5;
        return 1;
    }

    UnitType ChooseEnemyUnit(List<UnitType> affordable)
    {
        // Prefer cheaper troops most of the time; a giant is a rare, costly play.
        List<UnitType> weighted = new List<UnitType>();
        for (int i = 0; i < affordable.Count; i++)
        {
            UnitType type = affordable[i];
            int cost = (int)EnemyCost(type);
            int weight = cost == 2 ? 4 : cost == 3 ? 3 : cost == 4 ? 2 : 1;
            for (int j = 0; j < weight; j++)
                weighted.Add(type);
        }
        return weighted[Random.Range(0, weighted.Count)];
    }

    Vector3 FindEnemySpawnPosition()
    {
        // Spawn in front of the enemy towers, never behind the king tower.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float laneCenter = Random.value < 0.5f ? -BridgeX : BridgeX;
            float x = Mathf.Clamp(laneCenter + Random.Range(-0.65f, 0.65f),
                EnemyDeployMinX, EnemyDeployMaxX);
            float z = Random.Range(EnemyDeployMinZ, EnemyDeployMaxZ);
            bool nearTower = false;

            for (int i = 0; i < towers.Count; i++)
            {
                Tower tower = towers[i];
                if (tower == null || tower.destroyed || tower.player || tower.root == null)
                    continue;

                if (Vector2.Distance(new Vector2(x, z),
                    new Vector2(tower.position.x, tower.position.z)) < 2.1f)
                {
                    nearTower = true;
                    break;
                }
            }

            if (!nearTower)
                return new Vector3(x, 0f, z);
        }

        float fallbackZ = Mathf.Clamp(PrincessTowerZ - 2.4f,
            EnemyDeployMinZ, EnemyDeployMaxZ);
        return new Vector3(Random.value < 0.5f ? -BridgeX : BridgeX, 0f, fallbackZ);
    }

    void SpawnUnitGroup(UnitType type, bool isPlayer, Vector3 center, int count)
    {
        float formationCenter = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = center;
            position.x += (i - formationCenter) * 0.72f;
            SpawnUnit(type, isPlayer, position);
        }
    }

    void SpawnUnit(UnitType type, bool isPlayer, Vector3 position)
    {
        Unit u = new Unit();
        u.player = isPlayer;
        u.type = type;
        u.root = new GameObject((isPlayer ? "Player_" : "Enemy_") + type);
        u.root.transform.position = position;
        u.root.transform.rotation = Quaternion.identity;
        u.previousPosition = position;
        u.lane = Mathf.Abs(position.x - BridgeX) <= Mathf.Abs(position.x + BridgeX) ? 1 : -1;

        switch (type)
        {
            case UnitType.Knight:
                // Solidny, tani mini-tank. Średnie obrażenia, sporo zdrowia.
                u.maxHp = 350f; u.damage = 50f; u.speed = 1.55f; u.range = 1.25f; u.attackRate = 1.25f; u.radius = 0.48f; u.healthBarHeight = 2.15f;
                BuildKnight(u); break;
            case UnitType.Archer:
                // Wsparcie z tyłu. Mało HP, szybki atak.
                u.maxHp = 90f; u.damage = 25f; u.speed = 1.45f; u.range = 5.3f; u.attackRate = 1.1f; u.radius = 0.34f; u.healthBarHeight = 2.15f; u.ranged = true;
                BuildArcher(u); break;
            case UnitType.Giant:
                // Główny tank. Powolny, ignoruje jednostki, bardzo dużo HP.
                u.maxHp = 850f; u.damage = 65f; u.speed = 0.92f; u.range = 1.45f; u.attackRate = 1.8f; u.radius = 0.72f; u.healthBarHeight = 3.0f;
                u.buildingTargetsOnly = true;
                BuildGiant(u); break;
            case UnitType.Goblin:
                // Bardzo szybki dps z bliska. Ginie od byle czaru.
                u.maxHp = 45f; u.damage = 35f; u.speed = 2.45f; u.range = 0.95f; u.attackRate = 1.0f; u.radius = 0.28f; u.healthBarHeight = 1.55f;
                BuildGoblin(u); break;
            case UnitType.Bat:
                // Latający odpowiednik szkieletów/goblinów.
                u.maxHp = 35f; u.damage = 30f; u.speed = 2.5f; u.range = 0.75f; u.attackRate = 1.0f; u.radius = 0.18f; u.healthBarHeight = 1.35f;
                u.flying = true; u.flightHeight = 1.1f;
                BuildBat(u); break;
            case UnitType.MiniPekka:
                // Zabójca tanków. Bije rzadko, ale niszczyco.
                u.maxHp = 300f; u.damage = 180f; u.speed = 1.45f; u.range = 1.0f; u.attackRate = 1.6f; u.radius = 0.43f; u.healthBarHeight = 2.15f;
                BuildMiniPekka(u); break;
            case UnitType.Skeleton:
                // Mięso armatnie do odwracania uwagi.
                u.maxHp = 30f; u.damage = 25f; u.speed = 2.05f; u.range = 0.85f; u.attackRate = 1.0f; u.radius = 0.2f; u.healthBarHeight = 1.45f;
                BuildSkeleton(u); break;
            case UnitType.Guard:
                // Twardsza wersja szkieletu/goblina.
                u.maxHp = 180f; u.damage = 35f; u.speed = 1.35f; u.range = 1.05f; u.attackRate = 1.1f; u.radius = 0.48f; u.healthBarHeight = 2.05f;
                BuildGuard(u); break;
            case UnitType.Crossbowman:
                // Daleki zasięg, wolniejszy atak.
                u.maxHp = 120f; u.damage = 40f; u.speed = 1.05f; u.range = 6.2f; u.attackRate = 1.6f; u.radius = 0.36f; u.healthBarHeight = 2.0f; u.ranged = true;
                BuildCrossbowman(u); break;
            case UnitType.Troll:
                // Twardszy wojownik (pół-tank).
                u.maxHp = 450f; u.damage = 60f; u.speed = 1.2f; u.range = 1.2f; u.attackRate = 1.4f; u.radius = 0.56f; u.healthBarHeight = 2.45f;
                BuildTroll(u); break;
            case UnitType.Scout:
                // Bardzo szybki, tani zwiadowca.
                u.maxHp = 90f; u.damage = 20f; u.speed = 2.65f; u.range = 1.0f; u.attackRate = 0.8f; u.radius = 0.28f; u.healthBarHeight = 1.75f;
                BuildScout(u); break;
            case UnitType.Veteran:
                // Elitarny rycerz.
                u.maxHp = 400f; u.damage = 55f; u.speed = 1.25f; u.range = 1.3f; u.attackRate = 1.2f; u.radius = 0.55f; u.healthBarHeight = 2.35f;
                BuildVeteran(u); break;
            case UnitType.Hunter:
                // Duży dmg na bliski/średni dystans.
                u.maxHp = 180f; u.damage = 80f; u.speed = 1.4f; u.range = 4.4f; u.attackRate = 1.8f; u.radius = 0.4f; u.healthBarHeight = 2.2f; u.ranged = true;
                BuildHunter(u); break;
            case UnitType.Mage:
                // Musi ściągać małe jednostki "na strzała".
                u.maxHp = 160f; u.damage = 65f; u.speed = 1.2f; u.range = 4.8f; u.attackRate = 1.5f; u.radius = 0.36f; u.healthBarHeight = 2.25f; u.ranged = true;
                BuildMage(u); break;
            case UnitType.Cannon:
                // ZNACZNY BUFF: Strzela często (co 0.8s) i mocno. Zmieli szybko Hog Ridera.
                u.maxHp = 350f; u.damage = 50f; u.speed = 0f; u.range = 5.8f; u.attackRate = 0.8f; u.radius = 0.42f; u.healthBarHeight = 1.8f;
                u.ranged = true; u.isBuilding = true; u.lifetime = 15f;
                BuildCannon(u); break;
            case UnitType.Dragon:
                // Latający tank, powolny.
                u.maxHp = 420f; u.damage = 45f; u.speed = 1.35f; u.range = 4.8f; u.attackRate = 1.5f; u.radius = 0.48f; u.healthBarHeight = 2.65f;
                u.ranged = true; u.flying = true; u.flightHeight = 1.35f;
                BuildDragon(u); break;
            case UnitType.HogRider:
                // Szybko biegnie na budynki. Musi zignorować obrażenia, żeby zadać cios.
                u.maxHp = 320f; u.damage = 70f; u.speed = 2.55f; u.range = 1.0f; u.attackRate = 1.4f; u.radius = 0.48f; u.healthBarHeight = 2.2f;
                u.buildingTargetsOnly = true;
                BuildHogRider(u); break;
            default:
                u.maxHp = 80f; u.damage = 25f; u.speed = 1.9f; u.range = 4.0f; u.attackRate = 1.4f; u.radius = 0.28f; u.healthBarHeight = 1.7f; u.ranged = true;
                BuildGoblinSlinger(u); break;
        }
            if (u.flying)
            {
                position.y = u.flightHeight;
                u.root.transform.position = position;
            }
        u.hp = u.maxHp;
        CreateUnitHealthBar(u);
        if (u.type == UnitType.Cannon)
            CreateCannonRangeRing(u);
        units.Add(u);
    }

    void BuildKnight(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.78f, 0), new Vector3(0.66f, 0.78f, 0.66f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.65f, 0), new Vector3(0.52f, 0.52f, 0.52f), skin);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.72f, 0), new Vector3(0.59f, 0.36f, 0.59f), metal);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.38f, 0.93f, 0.12f), new Vector3(0.16f, 0.78f, 0.12f), gold);
        u.weapon = u.root.transform.GetChild(u.root.transform.childCount - 1).gameObject;
    }

    void BuildArcher(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.68f, 0), new Vector3(0.45f, 0.65f, 0.45f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.42f, 0), new Vector3(0.40f, 0.40f, 0.40f), skin);
        AddPart(u, PrimitiveType.Cylinder, new Vector3(0.32f, 0.94f, 0.12f), new Vector3(0.045f, 0.46f, 0.045f), gold);
        u.weapon = u.root.transform.GetChild(u.root.transform.childCount - 1).gameObject;
    }

    void BuildGiant(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 1.12f, 0), new Vector3(1.12f, 1.08f, 1.05f), Mat(new Color(0.55f, 0.30f, 0.17f)));
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 2.28f, 0), new Vector3(0.76f, 0.76f, 0.76f), skin);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.55f, 1.32f, 0.18f), new Vector3(0.48f, 0.52f, 0.48f), stone);
        u.weapon = u.root.transform.GetChild(u.root.transform.childCount - 1).gameObject;
    }

    void BuildGoblin(Unit u)
    {
        Material goblin = Mat(new Color(0.25f, 0.68f, 0.25f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.48f, 0), new Vector3(0.38f, 0.45f, 0.38f), goblin);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 0.98f, 0), new Vector3(0.36f, 0.36f, 0.36f), goblin);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.23f, 0.70f, 0.12f), new Vector3(0.09f, 0.42f, 0.09f), metal);
        u.weapon = u.root.transform.GetChild(u.root.transform.childCount - 1).gameObject;
    }

    void BuildSkeleton(Unit u)
    {
        Material bone = Mat(new Color(0.82f, 0.82f, 0.72f));
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.08f, 0), new Vector3(0.3f, 0.3f, 0.3f), bone);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.72f, 0), new Vector3(0.18f, 0.42f, 0.16f), bone);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.85f, 0.12f), new Vector3(0.42f, 0.1f, 0.1f), bone);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.12f, 0.35f, 0), new Vector3(0.09f, 0.38f, 0.1f), bone);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.12f, 0.35f, 0), new Vector3(0.09f, 0.38f, 0.1f), bone);
        AddPart(u, PrimitiveType.Sphere, new Vector3(-0.07f, 1.1f, 0.13f), Vector3.one * 0.045f, dark);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0.07f, 1.1f, 0.13f), Vector3.one * 0.045f, dark);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.28f, 0.72f, 0.08f), new Vector3(0.06f, 0.38f, 0.06f), stone);
    }

    void BuildGuard(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.76f, 0), new Vector3(0.72f, 0.78f, 0.66f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.56f, 0), new Vector3(0.5f, 0.5f, 0.5f), skin);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.43f, 0.88f, 0.2f), new Vector3(0.48f, 0.72f, 0.16f), metal);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.43f, 0.88f, 0.29f), new Vector3(0.24f, 0.42f, 0.035f), gold);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.36f, 0.98f, 0.18f), new Vector3(0.12f, 0.72f, 0.1f), gold);
    }

    void BuildCrossbowman(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.68f, 0), new Vector3(0.48f, 0.68f, 0.46f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.4f, 0), new Vector3(0.4f, 0.4f, 0.4f), skin);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.68f, -0.25f), new Vector3(0.55f, 0.18f, 0.16f), wood);
        AddPart(u, PrimitiveType.Cylinder, new Vector3(0, 0.7f, -0.34f), new Vector3(0.045f, 0.36f, 0.045f), gold);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.72f, -0.44f), new Vector3(0.06f, 0.06f, 0.42f), metal);
    }

    void BuildTroll(Unit u)
    {
        Material hide = Mat(new Color(0.32f, 0.48f, 0.22f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.8f, 0), new Vector3(0.74f, 0.76f, 0.64f), hide);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.58f, 0), new Vector3(0.58f, 0.5f, 0.56f), hide);
        AddPart(u, PrimitiveType.Capsule, new Vector3(-0.28f, 1.82f, 0.02f), new Vector3(0.13f, 0.3f, 0.13f), gold);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.38f, 0.78f, 0.03f), new Vector3(0.22f, 0.2f, 0.72f), stone);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.42f, 0.96f, 0.12f), new Vector3(0.22f, 0.7f, 0.22f), wood);
    }

    void BuildScout(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.52f, 0), new Vector3(0.38f, 0.5f, 0.38f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.08f, 0), new Vector3(0.34f, 0.34f, 0.34f), skin);
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 1.29f, 0), new Vector3(0.38f, 0.25f, 0.38f), dark);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.29f, 0.65f, 0.18f), new Vector3(0.06f, 0.48f, 0.08f), metal);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.29f, 0.65f, 0.18f), new Vector3(0.06f, 0.48f, 0.08f), metal);
    }

    void BuildVeteran(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.8f, 0), new Vector3(0.78f, 0.82f, 0.72f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.68f, 0), new Vector3(0.52f, 0.52f, 0.52f), skin);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.83f, 0), new Vector3(0.6f, 0.34f, 0.6f), gold);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.42f, 1.08f, 0), new Vector3(0.32f, 0.3f, 0.42f), metal);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.42f, 1.0f, 0.12f), new Vector3(0.16f, 0.9f, 0.12f), gold);
    }

    void BuildHunter(Unit u)
    {
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.68f, 0), new Vector3(0.5f, 0.68f, 0.46f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.42f, 0), new Vector3(0.4f, 0.4f, 0.4f), skin);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.28f, 0.92f, 0), new Vector3(0.16f, 0.76f, 0.18f), wood);
        AddPart(u, PrimitiveType.Cylinder, new Vector3(0.32f, 0.95f, 0.08f), new Vector3(0.045f, 0.52f, 0.045f), gold);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.32f, 0.95f, -0.03f), new Vector3(0.34f, 0.06f, 0.06f), wood);
    }

    void BuildMage(Unit u)
    {
        Material robe = Mat(new Color(0.28f, 0.22f, 0.58f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.86f, 0), new Vector3(0.7f, 0.82f, 0.64f), robe);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.72f, 0), new Vector3(0.48f, 0.5f, 0.46f), dark);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.62f, 0.32f), new Vector3(0.24f, 0.2f, 0.08f), skin);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.58f, -0.3f), new Vector3(0.66f, 0.64f, 0.12f), u.player ? blue : red);
        AddPart(u, PrimitiveType.Cylinder, new Vector3(0.52f, 1.05f, 0.12f), new Vector3(0.055f, 0.9f, 0.055f), wood);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0.52f, 2.0f, 0.12f), new Vector3(0.28f, 0.28f, 0.28f), magicProjectile);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.36f, 1.15f, 0.12f), new Vector3(0.12f, 0.48f, 0.12f), gold);
    }

    void BuildCannon(Unit u)
    {
        AddPart(u, PrimitiveType.Cylinder, new Vector3(0, 0.20f, 0), new Vector3(0.58f, 0.20f, 0.58f), dark);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.43f, 0), new Vector3(0.72f, 0.34f, 0.64f), stone);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 0.66f, 0), new Vector3(0.54f, 0.22f, 0.50f), metal);
        u.weapon = AddPart(u, PrimitiveType.Cylinder, new Vector3(0, 0.83f, 0.18f), new Vector3(0.16f, 0.42f, 0.16f), metal);
        u.weapon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void CreateCannonRangeRing(Unit u)
    {
        const int segments = 64;
        GameObject ringObject = new GameObject("Cannon Range");
        ringObject.transform.SetParent(u.root.transform, false);
        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = segments;
        ring.widthMultiplier = 0.055f;
        ring.numCapVertices = 2;
        ring.numCornerVertices = 2;
        ring.alignment = LineAlignment.View;
        ring.startColor = new Color(0.35f, 0.82f, 1f, 0.8f);
        ring.endColor = ring.startColor;
        ring.sharedMaterial = Mat(new Color(0.35f, 0.82f, 1f, 0.8f));

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * u.range, 0.035f,
                Mathf.Sin(angle) * u.range));
        }
    }

    void BuildDragon(Unit u)
    {
        Material scales = Mat(new Color(0.22f, 0.62f, 0.34f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 1.0f, 0), new Vector3(0.58f, 0.68f, 0.82f), scales);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.25f, 0.58f), new Vector3(0.42f, 0.38f, 0.42f), scales);
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.92f, -0.62f), new Vector3(0.20f, 0.18f, 0.70f), scales);
        u.leftWing = AddPart(u, PrimitiveType.Cube, new Vector3(-0.55f, 1.14f, -0.02f), new Vector3(0.82f, 0.08f, 0.48f), scales);
        u.rightWing = AddPart(u, PrimitiveType.Cube, new Vector3(0.55f, 1.14f, -0.02f), new Vector3(0.82f, 0.08f, 0.48f), scales);
        u.weapon = AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.2f, 0.83f), new Vector3(0.18f, 0.18f, 0.18f), fireballMaterial);
    }

    void BuildHogRider(Unit u)
    {
        Material hog = Mat(new Color(0.35f, 0.22f, 0.16f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.48f, 0), new Vector3(0.78f, 0.48f, 0.52f), hog);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 0.88f, 0.12f), new Vector3(0.46f, 0.42f, 0.42f), hog);
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 1.55f, -0.02f), new Vector3(0.42f, 0.52f, 0.42f), skin);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.83f, 0.12f), new Vector3(0.48f, 0.22f, 0.48f), dark);
        u.weapon = AddPart(u, PrimitiveType.Cylinder, new Vector3(0.34f, 1.55f, 0.18f), new Vector3(0.055f, 0.58f, 0.055f), metal);
    }

    void BuildMiniPekka(Unit u)
    {
        Material armor = Mat(new Color(0.16f, 0.22f, 0.31f));
        Material trim = Mat(new Color(0.30f, 0.66f, 0.82f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.8f, 0), new Vector3(0.7f, 0.78f, 0.62f), armor);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.62f, 0), new Vector3(0.56f, 0.5f, 0.54f), armor);
        AddPart(u, PrimitiveType.Cube, new Vector3(0, 1.66f, 0.28f), new Vector3(0.4f, 0.1f, 0.08f), trim);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.4f, 1.05f, 0), new Vector3(0.26f, 0.42f, 0.36f), metal);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.4f, 1.05f, 0), new Vector3(0.26f, 0.42f, 0.36f), metal);
        u.weapon = AddPart(u, PrimitiveType.Cube, new Vector3(0.46f, 1.0f, 0.2f), new Vector3(0.12f, 1.1f, 0.12f), trim);
    }

    void BuildBat(Unit u)
    {
        Material fur = Mat(new Color(0.22f, 0.18f, 0.32f));
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 0.95f, 0), new Vector3(0.3f, 0.24f, 0.36f), fur);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 1.08f, 0.24f), new Vector3(0.2f, 0.2f, 0.2f), fur);
        AddPart(u, PrimitiveType.Cube, new Vector3(-0.08f, 1.24f, 0.28f), new Vector3(0.055f, 0.2f, 0.05f), red);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.08f, 1.24f, 0.28f), new Vector3(0.055f, 0.2f, 0.05f), red);
        u.leftWing = AddPart(u, PrimitiveType.Cube, new Vector3(-0.28f, 1.02f, -0.02f), new Vector3(0.48f, 0.055f, 0.28f), fur);
        u.rightWing = AddPart(u, PrimitiveType.Cube, new Vector3(0.28f, 1.02f, -0.02f), new Vector3(0.48f, 0.055f, 0.28f), fur);
    }

    void BuildGoblinSlinger(Unit u)
    {
        Material goblin = Mat(new Color(0.25f, 0.68f, 0.25f));
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 0.48f, 0), new Vector3(0.38f, 0.45f, 0.38f), goblin);
        AddPart(u, PrimitiveType.Sphere, new Vector3(0, 0.98f, 0), new Vector3(0.36f, 0.36f, 0.36f), goblin);
        AddPart(u, PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), new Vector3(0.34f, 0.22f, 0.34f), gold);
        AddPart(u, PrimitiveType.Cube, new Vector3(0.24f, 0.68f, 0.08f), new Vector3(0.08f, 0.38f, 0.08f), wood);
        u.weapon = AddPart(u, PrimitiveType.Cylinder, new Vector3(0.28f, 0.92f, 0.1f), new Vector3(0.045f, 0.32f, 0.045f), gold);
    }

    GameObject AddPart(Unit u, PrimitiveType primitive, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = primitive.ToString();
        part.transform.SetParent(u.root.transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        Renderer r = part.GetComponent<Renderer>();
        if (r != null) r.material = material;
        Collider c = part.GetComponent<Collider>();
        if (c != null) Destroy(c);
        return part;
    }

    void CreateUnitHealthBar(Unit u)
    {
        GameObject barRoot = new GameObject("HealthBarRoot");
        barRoot.transform.SetParent(u.root.transform, false);
        barRoot.transform.localPosition = new Vector3(0, u.healthBarHeight, 0);
        u.healthBarRoot = barRoot.transform;

        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "HealthBar";
        bar.transform.SetParent(barRoot.transform, false);
        float width = u.radius > 0.65f ? 1.35f : 1.15f;
        bar.transform.localScale = new Vector3(width, 0.20f, 0.08f);
        SetMaterial(bar, dark);
        Collider c = bar.GetComponent<Collider>(); if (c != null) Destroy(c);

        u.healthFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        u.healthFill.name = "HealthFill";
        u.healthFill.transform.SetParent(barRoot.transform, false);
        u.healthFill.transform.localPosition = new Vector3(0, 0, -0.055f);
        u.healthFill.transform.localScale = new Vector3((width - 0.12f), 0.14f, 0.04f);
        SetMaterial(u.healthFill, greenHp);
        c = u.healthFill.GetComponent<Collider>(); if (c != null) Destroy(c);

    }

    void CreateTower(bool player, bool king, float x, float z)
    {
        Tower t = new Tower();
        t.player = player;
        t.king = king;
        t.position = new Vector3(x, 0f, z);
        t.maxHp = king ? 1800f : 950f;
        t.hp = t.maxHp;
        t.damage = king ? 50f : 36f;
        t.range = king ? 8.5f : 7.6f;
        t.attackRate = king ? 0.95f : 1.05f;
        t.root = new GameObject((player ? "Blue " : "Red ") + (king ? "King Tower" : "Princess Tower"));
        t.root.transform.position = t.position;

        // Restored classic tower silhouette: stone base, colored body, battlements,
        // roof and team banner. The wood trim keeps the rustic Clash-style look.
        Material team = player ? blue : red;
        float width = king ? 2.2f : 1.7f;
        float height = king ? 3.0f : 2.4f;

        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0f, 0.3f, 0f), new Vector3(width, 0.6f, width), stone);
        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0f, height * 0.5f, 0f), new Vector3(width * 0.72f, height, width * 0.72f), team);
        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0f, height + 0.1f, 0f), new Vector3(width, 0.32f, width), stone);

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                MakeTowerPart(t.root.transform, PrimitiveType.Cube,
                    new Vector3(sx * width * 0.34f, height + 0.38f, sz * width * 0.34f),
                    new Vector3(0.30f, 0.40f, 0.30f), stone);
            }
        }

        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0f, height + 0.63f, 0f), new Vector3(width * 0.76f, 0.30f, width * 0.76f),
            king ? gold : metal);
        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0f, height + 1.08f, 0f), new Vector3(0.10f, 0.70f, 0.10f), gold);
        MakeTowerPart(t.root.transform, PrimitiveType.Cube,
            new Vector3(0.20f, height + 1.15f, 0f), new Vector3(0.38f, 0.32f, 0.08f), team);

        // Small wooden planks under the battlements, matching the old arena style.
        float boardY = height * 0.58f;
        float bodyWidth = width * 0.72f;
        float boardLength = bodyWidth * 0.90f;
        float boardOffset = bodyWidth * 0.5f + 0.025f;
        for (int side = -1; side <= 1; side += 2)
        {
            MakeTowerPart(t.root.transform, PrimitiveType.Cube,
                new Vector3(0f, boardY, side * boardOffset), new Vector3(boardLength, 0.12f, 0.09f), wood);
            MakeTowerPart(t.root.transform, PrimitiveType.Cube,
                new Vector3(side * boardOffset, boardY, 0f), new Vector3(0.09f, 0.12f, boardLength), wood);
        }

        CreateTowerHealthBar(t);
        towers.Add(t);
    }

    void MakeTowerPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;
        SetMaterial(part, material);
    }

    void CreateTowerHealthBar(Tower t)
    {
        GameObject barRoot = new GameObject("TowerHealthBarRoot");
        barRoot.transform.SetParent(t.root.transform, false);
        barRoot.transform.localPosition = new Vector3(0, t.king ? 4.65f : 4.0f, 0);
        t.healthBarRoot = barRoot.transform;

        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = "TowerHealthBar";
        bar.transform.SetParent(barRoot.transform, false);
        float width = t.king ? 1.7f : 1.4f;
        bar.transform.localScale = new Vector3(width, 0.18f, 0.1f);
        SetMaterial(bar, dark);
        Collider c = bar.GetComponent<Collider>(); if (c != null) Destroy(c);
        t.healthFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        t.healthFill.name = "TowerHealthFill";
        t.healthFill.transform.SetParent(barRoot.transform, false);
        t.healthFill.transform.localPosition = new Vector3(0, 0, -0.065f);
        t.healthFill.transform.localScale = new Vector3(width - 0.15f, 0.11f, 0.04f);
        SetMaterial(t.healthFill, greenHp);
        c = t.healthFill.GetComponent<Collider>(); if (c != null) Destroy(c);

    }

    void SetMaterial(GameObject go, Material material)
    {
        if (go == null || material == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null) r.material = material;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float enemyRegenSeconds = IsDoubleElixirActive() ? EnemyElixirRegenSeconds * 0.5f : EnemyElixirRegenSeconds;
        enemyElixirRegenTimer += dt;
        while (enemyElixirRegenTimer >= enemyRegenSeconds)
        {
            enemyElixirRegenTimer -= enemyRegenSeconds;
            enemyElixir = Mathf.Min(10f, enemyElixir + 1f);
        }
        UpdateUnits(dt);
        UpdateTowers(dt);
        FaceHealthBars();
        UpdateProjectiles(dt);
        UpdateEffects(dt);
        UpdateDamagePopups(dt);
    }

    void FaceHealthBars()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Quaternion facing = cam.transform.rotation;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit.root != null && unit.healthBarRoot != null)
                unit.healthBarRoot.rotation = facing;
        }
        for (int i = 0; i < towers.Count; i++)
        {
            Tower tower = towers[i];
            if (!tower.destroyed && tower.root != null && tower.healthBarRoot != null)
                tower.healthBarRoot.rotation = facing;
        }
    }

    bool IsDoubleElixirActive()
    {
        if (manager == null) return false;
        string value = manager.GetTimeText();
        if (string.IsNullOrEmpty(value)) return false;
        string[] parts = value.Split(':');
        if (parts.Length != 2) return false;
        int minutes, seconds;
        if (!int.TryParse(parts[0], out minutes) || !int.TryParse(parts[1], out seconds)) return false;
        return minutes * 60 + seconds <= 60;
    }

    void UpdateUnits(float dt)
    {
        for (int i = units.Count - 1; i >= 0; i--)
        {
            Unit u = units[i];
            if (u.root == null) { units.RemoveAt(i); continue; }
            if (u.lifetime > 0f)
            {
                u.lifetime -= dt;
                if (u.isBuilding)
                {
                    u.hp = Mathf.Max(0f, u.hp - u.maxHp / 15f * dt);
                    UpdateUnitHealth(u);
                }
                if (u.lifetime <= 0f || u.hp <= 0f)
                {
                    Destroy(u.root);
                    units.RemoveAt(i);
                    continue;
                }
            }
            u.attackTimer -= dt;
            UpdateUnitAnimation(u, dt);
            if (u.isBuilding)
            {
                Unit buildingTarget = FindEnemyToFight(u);
                if (buildingTarget != null && buildingTarget.root != null &&
                    Vector3.Distance(Flat(u.root.transform.position), Flat(buildingTarget.root.transform.position)) <= u.range + buildingTarget.radius * 0.35f)
                {
                    Face(u, buildingTarget.root.transform.position);
                    if (u.attackTimer <= 0f) AttackUnit(u, buildingTarget);
                }
                continue;
            }
            if (u.buildingTargetsOnly)
            {
                if (u.targetUnit != null && (u.targetUnit.root == null || u.targetUnit.player == u.player || !u.targetUnit.isBuilding))
                {
                    u.targetUnit = null;
                }
                if (u.targetTower != null && (u.targetTower.destroyed || u.targetTower.root == null || u.targetTower.player == u.player))
                {
                    u.targetTower = null;
                    u.targetTowerLocked = false;
                }
                Unit nearbyCannon = FindEnemyCannon(u);
                float cannonAggroRange = nearbyCannon != null
                    ? Mathf.Max(EnemyAggroRange, u.range + nearbyCannon.radius + 0.5f)
                    : 0f;
                if (nearbyCannon != null && nearbyCannon.root != null &&
                    Vector3.Distance(Flat(u.root.transform.position), Flat(nearbyCannon.root.transform.position)) <= cannonAggroRange)
                {
                    u.targetUnit = nearbyCannon;
                    u.targetTower = null;
                    u.targetTowerLocked = false;
                }
                if (u.targetUnit == null && u.targetTower == null)
                {
                    Unit cannonTarget = FindEnemyCannon(u);
                    Tower towerTarget = GetTargetTowerForUnit(!u.player, u.root.transform.position, u.lane);
                    if (cannonTarget != null && cannonTarget.root != null &&
                        (towerTarget == null || Vector3.Distance(Flat(u.root.transform.position), Flat(cannonTarget.root.transform.position)) <
                            Vector3.Distance(Flat(u.root.transform.position), Flat(towerTarget.position))))
                        u.targetUnit = cannonTarget;
                    else
                        u.targetTower = towerTarget;
                }

                if (u.targetUnit != null && u.targetUnit.root != null)
                {
                    Vector3 targetPosition = u.targetUnit.root.transform.position;
                    Face(u, targetPosition);
                    if (Vector3.Distance(Flat(u.root.transform.position), Flat(targetPosition)) <= u.range + u.targetUnit.radius * 0.35f)
                    {
                        if (u.attackTimer <= 0f) AttackUnit(u, u.targetUnit);
                    }
                    else MoveUnit(u, GetRouteDestination(u, targetPosition), dt, false);
                    continue;
                }
                if (u.targetTower != null)
                {
                    float distance = Vector3.Distance(Flat(u.root.transform.position), Flat(u.targetTower.position));
                    if (distance <= u.range + (u.targetTower.king ? 0.8f : 0.65f))
                    {
                        Face(u, u.targetTower.position);
                        if (u.attackTimer <= 0f) AttackTower(u, u.targetTower);
                    }
                    else MoveUnit(u, GetRouteDestination(u, u.targetTower), dt, false);
                }
                continue;
            }

            if (u.targetUnit != null &&
                (u.targetUnit.root == null || u.targetUnit.player == u.player || u.targetUnit.isBuilding ||
                 (u.targetUnit.flying && !u.ranged)))
                u.targetUnit = null;
            if (u.targetTower != null && (u.targetTower.destroyed || u.targetTower.root == null || u.targetTower.player == u.player))
            {
                u.targetTower = null;
                u.targetTowerLocked = false;
            }

            if (u.targetUnit == null && (u.targetTower == null || !u.targetTowerLocked))
            {
                Unit enemyTarget = FindEnemyToFight(u);
                if (enemyTarget != null)
                {
                    u.targetUnit = enemyTarget;
                    u.targetTower = null;
                    u.targetTowerLocked = false;
                }
            }
            Unit enemy = u.targetUnit;
            if (enemy != null && enemy.root != null && u.root != null)
            {
                u.targetTower = null;
                Face(u, enemy.root.transform.position);
                if (Vector3.Distance(Flat(u.root.transform.position), Flat(enemy.root.transform.position)) <= u.range + enemy.radius * 0.35f)
                {
                    if (u.attackTimer <= 0f) AttackUnit(u, enemy);
                }
                else MoveUnit(u, enemy.root.transform.position, dt, true);
            }
            else
            {
                if (u.targetTower == null)
                    u.targetTower = GetTargetTowerForUnit(!u.player, u.root.transform.position, u.lane);
                Tower target = u.targetTower;
                if (target == null) continue;
                float distance = Vector3.Distance(Flat(u.root.transform.position), Flat(target.position));
                if (distance <= u.range + (target.king ? 0.8f : 0.65f))
                {
                    Face(u, target.position);
                    if (u.attackTimer <= 0f) AttackTower(u, target);
                }
                else
                {
                    Vector3 destination = GetRouteDestination(u, target);
                    MoveUnit(u, destination, dt, !u.buildingTargetsOnly);
                }
            }
        }
    }

    Unit FindEnemyToFight(Unit u)
    {
        if (u == null || u.root == null) return null;
        Unit best = null;
        float bestDistance = float.MaxValue;
        Vector3 p = Flat(u.root.transform.position);
        for (int i = 0; i < units.Count; i++)
        {
            Unit other = units[i];
            if (other == u || other.root == null || other.player == u.player) continue;
            if (other.isBuilding) continue;
            if (other.flying && (!u.ranged || u.isBuilding)) continue;
            if (u.buildingTargetsOnly) continue;
            float d = Vector3.Distance(p, Flat(other.root.transform.position));
            float allowed = Mathf.Max(EnemyAggroRange, u.range + other.radius + 0.5f);
            if (d <= allowed && d < bestDistance)
            {
                best = other;
                bestDistance = d;
            }
        }
        return best;
    }

    Unit FindEnemyCannon(Unit u)
    {
        Unit best = null;
        float bestDistance = float.MaxValue;
        Vector3 position = Flat(u.root.transform.position);
        for (int i = 0; i < units.Count; i++)
        {
            Unit other = units[i];
            if (other.root == null || other.player == u.player || !other.isBuilding)
                continue;
            float distance = Vector3.Distance(position, Flat(other.root.transform.position));
            if (distance < bestDistance)
            {
                best = other;
                bestDistance = distance;
            }
        }
        return best;
    }

    Vector3 GetRouteDestination(Unit u, Tower target)
    {
        return GetRouteDestination(u, target.position, target.king);
    }

    Vector3 GetRouteDestination(Unit u, Vector3 targetPosition, bool kingTarget = false)
    {
        Vector3 current = u.root.transform.position;
        float direction = u.player ? 1f : -1f;
        float nearZ = u.player ? -BridgeNearBankZ : BridgeNearBankZ;
        float farZ = u.player ? RiverFarBankZ : -RiverFarBankZ;
        float bridgeX = u.lane * BridgeX;

        if (u.routeStage == 0)
        {
            Vector3 nearBank = new Vector3(bridgeX, 0f, nearZ);
            if (Vector3.Distance(Flat(current), Flat(nearBank)) < 0.55f)
                u.routeStage = 1;
            else return nearBank;
        }
        if (u.routeStage == 1)
        {
            Vector3 farBank = new Vector3(bridgeX, 0f, farZ);
            if (Vector3.Distance(Flat(current), Flat(farBank)) < 0.65f)
            {
                u.routeStage = 2;
                u.crossedRiver = true;
            }
            else return farBank;
        }

        // Once across, units travel toward the nearest living tower on the opposing side.
        Vector3 approach = new Vector3(targetPosition.x, 0f,
            targetPosition.z - direction * (kingTarget ? 2.0f : 1.5f));
        return approach;
    }

    void MoveUnit(Unit u, Vector3 destination, float dt, bool useSeparation)
    {
        Vector3 current = u.root.transform.position;
        destination.y = 0f;
        Vector3 direction = destination - current;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0025f) return;
        direction.Normalize();
        if (useSeparation) direction = ApplySeparation(u, direction);
        // Do not let separation push a unit sideways off the bridge while it is crossing.
        if (u.routeStage == 1)
        {
            float bridgeX = u.lane * BridgeX;
            direction = new Vector3((bridgeX - current.x) * 1.8f, 0f, direction.z).normalized;
        }
        u.previousPosition = current;
        u.root.transform.position = current + direction * u.speed * dt;
        float bob = u.flying ? Mathf.Sin(Time.time * 4f + u.root.GetInstanceID()) * 0.08f : 0f;
        u.root.transform.position = new Vector3(u.root.transform.position.x,
            u.flying ? u.flightHeight + bob : 0f, u.root.transform.position.z);
        Face(u, u.root.transform.position + direction);

    }

    Vector3 ApplySeparation(Unit u, Vector3 desired)
    {
        Vector3 push = Vector3.zero;
        Vector3 p = u.root.transform.position;
        for (int i = 0; i < units.Count; i++)
        {
            Unit other = units[i];
            if (other == u || other.root == null || other.player != u.player) continue;
            if (u.flying || other.flying) continue;
            Vector3 delta = p - other.root.transform.position;
            delta.y = 0f;
            float minDistance = u.radius + other.radius + 0.12f;
            float d = delta.magnitude;
            if (d > 0.001f && d < minDistance)
                push += delta.normalized * ((minDistance - d) / minDistance);
        }
        Vector3 result = desired + push * SeparationStrength;
        if (result.sqrMagnitude < 0.001f) return desired;
        return result.normalized;
    }

    void Face(Unit u, Vector3 target)
    {
        Vector3 dir = target - u.root.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) u.root.transform.rotation = Quaternion.Slerp(u.root.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);
    }

    void UpdateUnitAnimation(Unit u, float dt)
    {
        if (u.flying)
        {
            float flap = Mathf.Sin(Time.time * 11f) * 28f;
            if (u.leftWing != null) u.leftWing.transform.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (u.rightWing != null) u.rightWing.transform.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }
        if (u.weapon == null) return;
        if (u.attackAnimTimer > 0f)
        {
            u.attackAnimTimer -= dt;
            float progress = 1f - Mathf.Clamp01(u.attackAnimTimer / 0.22f);
            float swing = Mathf.Sin(progress * Mathf.PI) * 70f;
            u.weapon.transform.localRotation = Quaternion.Euler(0f, 0f, swing);
        }
        else
        {
            u.weapon.transform.localRotation = Quaternion.identity;
        }
    }

    void AttackUnit(Unit attacker, Unit target)
    {
        // A unit may have been destroyed earlier in the same frame by another attack.
        if (attacker == null || target == null || attacker.root == null || target.root == null) return;
        attacker.attackTimer = attacker.attackRate;
        if (attacker.ranged)
            CreateProjectile(attacker.root.transform.position + Vector3.up * 1.35f, target, null, attacker.damage, ProjectileSpeed, attacker.player, attacker.type == UnitType.Mage);
        else
        {
            DamageUnit(target, attacker.damage);
            if (manager != null) manager.PlayHitSound();
        }
        PlayAttackMotion(attacker);
    }

    void AttackTower(Unit attacker, Tower target)
    {
        if (attacker == null || attacker.root == null || target == null || target.destroyed || target.root == null) return;
        attacker.attackTimer = attacker.attackRate;
        attacker.targetTowerLocked = true;
        if (attacker.ranged)
            CreateProjectile(attacker.root.transform.position + Vector3.up * 1.35f, null, target, attacker.damage, ProjectileSpeed, attacker.player, attacker.type == UnitType.Mage);
        else
        {
            DamageTower(target, attacker.damage);
            if (manager != null) manager.PlayHitSound();
        }
        PlayAttackMotion(attacker);
    }

    void PlayAttackMotion(Unit u)
    {
        u.attackAnimTimer = 0.22f;
    }

    void UpdateTowers(float dt)
    {
        for (int i = 0; i < towers.Count; i++)
        {
            Tower tower = towers[i];
            if (tower.destroyed || tower.root == null) continue;
            if (tower.king && LivingPrincessTowerCount(tower.player) == 2) continue;
            tower.timer -= dt;
            if (tower.timer > 0f) continue;
            Unit target = null;
            float best = tower.range;
            for (int j = 0; j < units.Count; j++)
            {
                Unit u = units[j];
                if (u.root == null || u.player == tower.player) continue;
                float d = Vector3.Distance(Flat(tower.position), Flat(u.root.transform.position));
                if (d < best) { best = d; target = u; }
            }
            if (target != null)
            {
                tower.timer = tower.attackRate;
                CreateProjectile(tower.position + Vector3.up * (tower.king ? 2.8f : 2.25f), target, null, tower.damage, TowerProjectileSpeed, tower.player);
            }
        }
    }

    void CreateProjectile(Vector3 position, Unit targetUnit, Tower targetTower, float damage, float speed, bool sourcePlayer, bool magical = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BattleProjectile";
        go.transform.position = position;
        go.transform.localScale = targetUnit != null && targetUnit.ranged ? Vector3.one * 0.18f : Vector3.one * 0.24f;
        Material mat = magical ? magicProjectile : sourcePlayer ? blueProjectile : redProjectile;
        SetMaterial(go, mat);
        Collider c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
        Projectile p = new Projectile();
        p.root = go; p.targetUnit = targetUnit; p.targetTower = targetTower; p.damage = damage; p.speed = speed; p.life = 6f; p.sourcePlayer = sourcePlayer; p.magical = magical; p.material = mat;
        projectiles.Add(p);
    }

    public bool CanCastFireballAt(Vector3 position)
    {
        return position.x >= -9.5f && position.x <= 9.5f &&
               position.z >= -17.5f && position.z <= 17.5f;
    }

    public bool CastFireball(Vector3 targetPosition, bool sourcePlayer)
    {
        if (!CanCastFireballAt(targetPosition)) return false;
        Vector3 launchPosition = new Vector3(0f, 1.4f,
            sourcePlayer ? -KingTowerZ + 1.2f : KingTowerZ - 1.2f);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Fireball";
        go.transform.position = launchPosition;
        go.transform.localScale = Vector3.one * 0.72f;
        SetMaterial(go, fireballMaterial);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        projectiles.Add(new Projectile
        {
            root = go,
            destination = targetPosition + Vector3.up * 0.35f,
            damage = 220f,
            speed = 15f,
            life = 10f,
            sourcePlayer = sourcePlayer,
            fireball = true,
            material = fireballMaterial
        });
        return true;
    }

    void UpdateProjectiles(float dt)
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            Projectile p = projectiles[i];
            if (p.root == null) { projectiles.RemoveAt(i); continue; }
            p.life -= dt;
            Vector3 targetPos;
            if (p.fireball) targetPos = p.destination;
            else if (p.targetUnit != null && p.targetUnit.root != null) targetPos = p.targetUnit.root.transform.position + Vector3.up * 0.8f;
            else if (p.targetTower != null && !p.targetTower.destroyed && p.targetTower.root != null) targetPos = p.targetTower.position + Vector3.up * 1.2f;
            else { Destroy(p.root); projectiles.RemoveAt(i); continue; }

            Vector3 delta = targetPos - p.root.transform.position;
            float distance = delta.magnitude;
            if (distance <= p.speed * dt + 0.15f)
            {
                if (p.fireball)
                {
                    ResolveFireball(p.destination, p.sourcePlayer, p.damage);
                    CreateImpact(p.destination);
                }
                else if (p.magical)
                    ResolveMageSplash(targetPos, p.sourcePlayer, p.damage, p.targetTower);
                else if (p.targetUnit != null && p.targetUnit.root != null) DamageUnit(p.targetUnit, p.damage);
                else if (p.targetTower != null && !p.targetTower.destroyed) DamageTower(p.targetTower, p.damage);
                if (manager != null) manager.PlayHitSound();
                Destroy(p.root);
                projectiles.RemoveAt(i);
            }
            else
            {
                p.root.transform.position += delta.normalized * p.speed * dt;
                if (delta.sqrMagnitude > 0.001f) p.root.transform.rotation = Quaternion.LookRotation(delta);
            }
            if (p.life <= 0f)
            {
                if (p.root != null) Destroy(p.root);
                projectiles.RemoveAt(i);
            }
        }
    }

    void ResolveMageSplash(Vector3 position, bool sourcePlayer, float damage, Tower targetTower)
    {
        const float radius = 1.5f;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit.root == null || unit.player == sourcePlayer) continue;
            if (Vector3.Distance(Flat(position), Flat(unit.root.transform.position)) <= radius)
                DamageUnit(unit, damage);
        }

        if (targetTower != null && !targetTower.destroyed)
            DamageTower(targetTower, damage);
    }

    void ResolveFireball(Vector3 position, bool sourcePlayer, float damage)
    {
        const float radius = 2.6f;
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit.root == null || unit.player == sourcePlayer) continue;
            if (Vector3.Distance(Flat(position), Flat(unit.root.transform.position)) <= radius)
                DamageUnit(unit, damage);
        }

        for (int i = 0; i < towers.Count; i++)
        {
            Tower tower = towers[i];
            if (tower.destroyed || tower.player == sourcePlayer) continue;
            if (Vector3.Distance(Flat(position), Flat(tower.position)) > radius) continue;

            float towerDamage = damage * 0.5f;
            if (tower.king && LivingPrincessTowerCount(tower.player) == 2)
                towerDamage *= 0.1f;
            DamageTower(tower, towerDamage);
        }
    }

    int LivingPrincessTowerCount(bool playerSide)
    {
        int count = 0;
        for (int i = 0; i < towers.Count; i++)
        {
            Tower tower = towers[i];
            if (!tower.destroyed && !tower.king && tower.player == playerSide)
                count++;
        }
        return count;
    }

    void DamageUnit(Unit u, float amount)
    {
        if (u == null || u.root == null) return;
        CreateDamagePopup(u.root.transform.position + Vector3.up * (u.healthBarHeight + 0.4f), amount);
        u.hp = Mathf.Max(0f, u.hp - amount);
        UpdateUnitHealth(u);
        if (u.hp <= 0f)
        {
            CreateImpact(u.root.transform.position + Vector3.up * 0.6f);
            Destroy(u.root);
            u.root = null;
        }
    }

    void DamageTower(Tower t, float amount)
    {
        if (t == null || t.destroyed) return;
        CreateDamagePopup(t.position + Vector3.up * (t.king ? 5.45f : 4.8f), amount);
        t.hp = Mathf.Max(0f, t.hp - amount);
        UpdateTowerHealth(t);
        if (t.hp <= 0f)
        {
            t.destroyed = true;
            CreateImpact(t.position + Vector3.up * 1.2f);
            if (t.root != null) Destroy(t.root);
            if (manager != null) manager.OnTowerDestroyed(t.player, t.king);
        }
    }

    void UpdateUnitHealth(Unit u)
    {
        if (u.healthFill == null || u.root == null) return;
        float fraction = Mathf.Clamp01(u.hp / u.maxHp);
        Vector3 scale = u.healthFill.transform.localScale;
        float width = (u.radius > 0.65f ? 1.35f : 1.15f) - 0.12f;
        scale.y = 0.14f;
        scale.x = width * fraction;
        u.healthFill.transform.localScale = scale;
        u.healthFill.transform.localPosition = new Vector3(-width * 0.5f + scale.x * 0.5f, 0, -0.055f);
        SetMaterial(u.healthFill, HealthColor(fraction));
    }

    void UpdateTowerHealth(Tower t)
    {
        if (t.healthFill == null || t.root == null) return;
        float fraction = Mathf.Clamp01(t.hp / t.maxHp);
        float fullWidth = (t.king ? 1.7f : 1.4f) - 0.15f;
        float width = fullWidth * fraction;
        t.healthFill.transform.localScale = new Vector3(width, 0.11f, 0.04f);
        t.healthFill.transform.localPosition = new Vector3(-fullWidth * 0.5f + width * 0.5f, 0, -0.065f);
        SetMaterial(t.healthFill, HealthColor(fraction));
    }

    void CreateDamagePopup(Vector3 position, float amount)
    {
        GameObject root = new GameObject("Damage Number");
        TextMesh text = root.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 36;
        text.characterSize = 0.06f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.color = amount >= 100f ? new Color(1f, 0.72f, 0.24f) : Color.white;
        text.text = "-" + Mathf.RoundToInt(amount);
        root.transform.position = position;
        TextMesh shadow = new GameObject("Damage Number Shadow").AddComponent<TextMesh>();
        shadow.transform.SetParent(root.transform, false);
        shadow.transform.localPosition = new Vector3(0.025f, -0.025f, -0.006f);
        shadow.font = text.font;
        shadow.fontSize = text.fontSize;
        shadow.characterSize = text.characterSize * 1.08f;
        shadow.anchor = text.anchor;
        shadow.alignment = text.alignment;
        shadow.fontStyle = FontStyle.Bold;
        shadow.color = new Color(0f, 0f, 0f, 0.8f);
        shadow.text = text.text;
        damagePopups.Add(new DamagePopup
        {
            root = root,
            text = text,
            shadow = shadow,
            startPosition = position,
            remaining = 0.85f,
            duration = 0.85f
        });
    }

    void UpdateDamagePopups(float dt)
    {
        Camera cam = Camera.main;
        for (int i = damagePopups.Count - 1; i >= 0; i--)
        {
            DamagePopup popup = damagePopups[i];
            popup.remaining -= dt;
            if (popup.root != null)
            {
                float progress = 1f - Mathf.Clamp01(popup.remaining / popup.duration);
                popup.root.transform.position = popup.startPosition + Vector3.up * (0.32f * progress);
                if (cam != null)
                    popup.root.transform.rotation = cam.transform.rotation;
                Color color = popup.text.color;
                color.a = 1f - progress;
                popup.text.color = color;
                Color shadowColor = popup.shadow.color;
                shadowColor.a = 0.8f * (1f - progress);
                popup.shadow.color = shadowColor;
            }
            if (popup.remaining <= 0f)
            {
                if (popup.root != null) Destroy(popup.root);
                damagePopups.RemoveAt(i);
            }
        }
    }

    Material HealthColor(float fraction)
    {
        if (fraction <= 0.25f) return redHp;
        if (fraction <= 0.6f) return yellowHp;
        return greenHp;
    }

    // World position of each king tower, used by UI emote bubbles.
    public Vector3 GetKingTowerPosition(bool playerSide)
    {
        for (int i = 0; i < towers.Count; i++)
        {
            Tower tower = towers[i];
            if (tower != null && !tower.destroyed && tower.king && tower.player == playerSide && tower.root != null)
                return tower.root.transform.position;
        }
        return new Vector3(0f, 0f, playerSide ? -KingTowerZ : KingTowerZ);
    }

    Tower GetTargetTowerForUnit(bool targetPlayer, Vector3 unitPosition, int lane)
    {
        Tower lanePrincess = null;
        Tower otherPrincess = null;
        Tower king = null;
        for (int i = 0; i < towers.Count; i++)
        {
            Tower t = towers[i];
            if (t.destroyed || t.root == null || t.player != targetPlayer) continue;
            if (t.king) { king = t; continue; }
            if ((t.position.x >= 0f ? 1 : -1) == lane)
                lanePrincess = t;
            else
                otherPrincess = t;
        }
        return lanePrincess != null ? lanePrincess : king != null ? king : otherPrincess;
    }

    void CreateImpact(Vector3 position)
    {
        GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        effect.name = "ImpactEffect";
        effect.transform.position = position;
        effect.transform.localScale = Vector3.one * 0.25f;
        SetMaterial(effect, impactMaterial);
        Collider c = effect.GetComponent<Collider>(); if (c != null) Destroy(c);
        effects.Add(new TemporaryEffect { root = effect, life = 0.22f });
    }

    void UpdateEffects(float dt)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            TemporaryEffect e = effects[i];
            e.life -= dt;
            if (e.root != null)
            {
                float scale = Mathf.Lerp(0.55f, 2.2f, 1f - Mathf.Clamp01(e.life / 0.22f));
                e.root.transform.localScale = Vector3.one * scale;
            }
            if (e.life <= 0f)
            {
                if (e.root != null) Destroy(e.root);
                effects.RemoveAt(i);
            }
        }
    }

    Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
