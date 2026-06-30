using SpaceGame.Models;

namespace SpaceGame.Systems;

public class Player
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Angle { get; set; }
    public float RotationVelocity { get; set; }

    public int Credits { get; set; } = 50;
    public int Health { get; set; } = 50;
    public int MaxHealth { get; set; } = 50;
    public float ShieldHP { get; set; }
    public float MaxShieldHP { get; set; }
    public float Fuel { get; set; } = 100f;
    public float MaxFuel { get; set; } = 100f;
    public List<string> OwnedUpgrades { get; set; } = new();
    public List<string> CompletedQuests { get; set; } = new();
    public string? CurrentSystemId { get; set; }

    public List<InventoryEntry> Resources { get; set; } = new();
    public List<InventoryEntry> QuestItems { get; set; } = new();
    public List<InventoryEntry> Consumables { get; set; } = new();
    public List<InventoryEntry> UnequippedEquipment { get; set; } = new();
    public Dictionary<string, string> Equipment { get; set; } = new(); // slot -> equipmentId
    public int CargoCapacity { get; set; } = 100;

    public float BaseMaxSpeed { get; set; } = 300f;
    public float BaseThrust { get; set; } = 500f;
    public float BaseRotationSpeed { get; set; } = 5f;

    private const float Drag = 0.98f;

    public float MaxSpeed => BaseMaxSpeed * _speedMult;
    public float Thrust => BaseThrust;
    public float RotationSpeed => BaseRotationSpeed * (OwnedUpgrades.Contains("handling_v1") ? 1.3f : 1f);

    private float _speedMult = 1f;

    public bool HasShield => MaxShieldHP > 0;

    public int UsedCargo
    {
        get
        {
            int total = 0;
            foreach (var entry in Resources)
                total += entry.Quantity;
            return total;
        }
    }

    public bool HasEnergyCanister => Consumables.Any(c => c.Id == "energy_canister" && c.Quantity > 0);

    public void RecalculateStats(List<EquipmentDef> allEquipment)
    {
        float shieldBonus = 0;
        float armorHpBonus = 0;
        float speedMult = 1f;
        float fuelEff = 1f;
        int cargoBonus = 0;

        foreach (var kv in Equipment)
        {
            var def = allEquipment.FirstOrDefault(e => e.Id == kv.Value);
            if (def == null) continue;

            switch (def.EffectType)
            {
                case "shield_strength":
                    shieldBonus += def.EffectValue;
                    break;
                case "armor_hp":
                    armorHpBonus += (int)def.EffectValue;
                    break;
                case "engine_boost":
                    speedMult *= def.EffectValue;
                    break;
                case "engine_efficiency":
                    fuelEff *= def.EffectValue;
                    break;
                case "cargo_capacity":
                    cargoBonus += (int)def.EffectValue;
                    break;
            }
        }

        // Apply ownable upgrades too
        if (OwnedUpgrades.Contains("speed_boost")) speedMult *= 1.25f;

        _speedMult = speedMult;
        FuelEfficiency = fuelEff;
        MaxShieldHP = shieldBonus;
        ShieldHP = MathF.Min(ShieldHP, MaxShieldHP);
        int baseHp = 50;
        MaxHealth = baseHp + (int)armorHpBonus;
        Health = Math.Min(Health, MaxHealth);
        CargoCapacity = 100 + cargoBonus;
    }

    public float FuelEfficiency { get; set; } = 1f;

    public bool UseConsumable(string id, float effectValue, string effectType)
    {
        var entry = Consumables.FirstOrDefault(c => c.Id == id);
        if (entry == null || entry.Quantity <= 0)
            return false;
        entry.Quantity--;
        if (entry.Quantity <= 0)
            Consumables.RemoveAll(c => c.Id == id);

        switch (effectType)
        {
            case "fuel_refill":
                Fuel = MathF.Min(MaxFuel, Fuel + MaxFuel * effectValue);
                break;
            case "fuel_add":
                Fuel = MathF.Min(MaxFuel, Fuel + effectValue);
                break;
            case "repair_hp":
                Health = Math.Min(MaxHealth, Health + (int)effectValue);
                break;
        }
        return true;
    }

    public bool UseEnergyCanister()
    {
        return UseConsumable("energy_canister", 0.2f, "fuel_refill");
    }

    public bool UseFuelCell()
    {
        return UseConsumable("fuel_cell", 20f, "fuel_add");
    }

    public string? GetEquippedWeapon(int slotIndex)
    {
        string key = slotIndex == 0 ? "weapon1" : "weapon2";
        return Equipment.TryGetValue(key, out var id) ? id : null;
    }

    public float GetWeaponDamage(List<EquipmentDef> allEquipment, int slotIndex)
    {
        string key = slotIndex == 0 ? "weapon1" : "weapon2";
        if (!Equipment.TryGetValue(key, out var id))
            return 1f;
        var def = allEquipment.FirstOrDefault(e => e.Id == id);
        if (def == null || (def.EffectType != "weapon_bullet" && def.EffectType != "weapon_damage" && def.EffectType != "weapon_missile"))
            return 1f;
        return def.EffectValue;
    }

    public void TakeDamage(float amount)
    {
        if (ShieldHP > 0f)
        {
            float absorbed = MathF.Min(ShieldHP, amount);
            ShieldHP -= absorbed;
            amount -= absorbed;
        }
        if (amount > 0f)
            Health = Math.Max(0, Health - (int)amount);
    }

    public int MissileAmmoCount
    {
        get
        {
            var entry = Resources.FirstOrDefault(r => r.Id == "missile_ammo");
            return entry?.Quantity ?? 0;
        }
    }

    public bool ConsumeMissileAmmo()
    {
        var entry = Resources.FirstOrDefault(r => r.Id == "missile_ammo");
        if (entry == null || entry.Quantity <= 0)
            return false;
        entry.Quantity--;
        if (entry.Quantity <= 0)
            Resources.RemoveAll(r => r.Id == "missile_ammo");
        return true;
    }

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
    }

    public void Update(float dt, bool thrustUp, bool thrustDown, bool turnLeft, bool turnRight)
    {
        if (thrustDown) { (turnLeft, turnRight) = (turnRight, turnLeft); }
        if (turnLeft)  RotationVelocity -= RotationSpeed * dt;
        if (turnRight) RotationVelocity += RotationSpeed * dt;
        RotationVelocity *= 0.9f;
        Angle += RotationVelocity * dt;

        if (thrustUp && Fuel > 0)
            Velocity += Vector2.FromAngle(Angle) * Thrust * dt;
        if (thrustDown && Fuel > 0)
            Velocity -= Vector2.FromAngle(Angle) * Thrust * 0.5f * dt;

        float speed = Velocity.Length();
        if (speed > MaxSpeed)
            Velocity = Velocity.Normalized() * MaxSpeed;

        Position += Velocity * dt;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Velocity *= Drag;
        if (Velocity.Length() < 1f)
            Velocity = Vector2.Zero;
    }
}
