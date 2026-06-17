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
    public List<string> OwnedUpgrades { get; set; } = new();
    public List<string> CompletedQuests { get; set; } = new();
    public string? CurrentSystemId { get; set; }

    public List<InventoryEntry> Resources { get; set; } = new();
    public List<InventoryEntry> QuestItems { get; set; } = new();
    public Dictionary<string, string> Equipment { get; set; } = new(); // slot -> equipmentId
    public int CargoCapacity { get; set; } = 100;

    public float BaseMaxSpeed { get; set; } = 300f;
    public float BaseThrust { get; set; } = 500f;
    public float BaseRotationSpeed { get; set; } = 5f;

    private const float Drag = 0.98f;

    public float MaxSpeed
    {
        get
        {
            float mult = 1f;
            if (OwnedUpgrades.Contains("speed_boost")) mult *= 1.25f;
            return BaseMaxSpeed * mult;
        }
    }

    public float Thrust => BaseThrust;

    public float RotationSpeed
    {
        get
        {
            float mult = 1f;
            if (OwnedUpgrades.Contains("handling_v1")) mult *= 1.3f;
            return BaseRotationSpeed * mult;
        }
    }

    public bool HasShield => OwnedUpgrades.Contains("shield_v1") || Equipment.ContainsKey("shield");

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

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
    }

    public void Update(float dt, bool thrustUp, bool thrustDown, bool turnLeft, bool turnRight)
    {
        if (turnLeft)  RotationVelocity -= RotationSpeed * dt;
        if (turnRight) RotationVelocity += RotationSpeed * dt;
        RotationVelocity *= 0.9f;
        Angle += RotationVelocity * dt;

        if (thrustUp)
            Velocity += Vector2.FromAngle(Angle) * Thrust * dt;
        if (thrustDown)
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
