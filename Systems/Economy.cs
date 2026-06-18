using SpaceGame.Models;

namespace SpaceGame.Systems;

public class Economy
{
    private Galaxy _galaxy;
    private Dictionary<string, SystemMarketState> _markets = new();

    public Economy(Galaxy galaxy)
    {
        _galaxy = galaxy;
    }

    public void Initialize()
    {
        _markets.Clear();
        foreach (var sys in _galaxy.Systems)
        {
            var market = new SystemMarketState();
            if (sys.Economy != null)
            {
                foreach (var kv in sys.Economy.Production)
                {
                    float dailyProd = kv.Value;
                    float demand = sys.Economy.Demand.GetValueOrDefault(kv.Key, 1f);
                    float startStock = dailyProd * 60f;
                    market.Stocks[kv.Key] = startStock;
                    market.Demands[kv.Key] = demand;
                    market.ProductionRates[kv.Key] = dailyProd;
                }
                foreach (var kv in sys.Economy.Demand)
                {
                    if (!market.Demands.ContainsKey(kv.Key))
                        market.Demands[kv.Key] = kv.Value;
                }
            }
            // Add universal ammo/consumable to any system with market service
            if (sys.Services != null && sys.Services.Contains("market"))
            {
                if (!market.ProductionRates.ContainsKey("missile_ammo"))
                {
                    market.ProductionRates["missile_ammo"] = 5f;
                    market.Demands["missile_ammo"] = 1f;
                    market.Stocks["missile_ammo"] = 300f;
                }
            }

            _markets[sys.Id] = market;
        }
    }

    public int GetBuyPrice(string systemId, string resourceId)
    {
        var market = GetMarket(systemId);
        float basePrice = GetBasePrice(resourceId);
        float demand = market.Demands.GetValueOrDefault(resourceId, 1f);
        float stock = market.Stocks.GetValueOrDefault(resourceId, 0f);
        float prod = market.ProductionRates.GetValueOrDefault(resourceId, 1f);
        float stockRatio = prod > 0f ? stock / (prod * 60f) : stock / (stock + 100f);
        float mult = demand / MathF.Max(stockRatio, 0.05f) * 0.5f;
        mult = Math.Clamp(mult, 0.3f, 5f);
        return Math.Max(1, (int)(basePrice * mult));
    }

    public int GetSellPrice(string systemId, string resourceId)
    {
        int buyPrice = GetBuyPrice(systemId, resourceId);
        return Math.Max(1, (int)(buyPrice * 0.6f));
    }

    public bool Buy(Player player, string systemId, string resourceId, int quantity)
    {
        var market = GetMarket(systemId);
        int price = GetBuyPrice(systemId, resourceId);
        int totalCost = price * quantity;
        if (player.Credits < totalCost) return false;
        if (player.UsedCargo + quantity > player.CargoCapacity) return false;

        player.Credits -= totalCost;
        AddToInventory(player.Resources, resourceId, quantity);

        if (market.Stocks.ContainsKey(resourceId))
            market.Stocks[resourceId] -= quantity;
        return true;
    }

    public bool Sell(Player player, string systemId, string resourceId, int quantity)
    {
        var market = GetMarket(systemId);
        var entry = player.Resources.FirstOrDefault(r => r.Id == resourceId);
        if (entry == null || entry.Quantity < quantity) return false;

        int price = GetSellPrice(systemId, resourceId);
        player.Credits += price * quantity;
        RemoveFromInventory(player.Resources, resourceId, quantity);

        int volume = GetVolume(resourceId);
        if (market.Stocks.ContainsKey(resourceId))
            market.Stocks[resourceId] += quantity;
        else
            market.Stocks[resourceId] = quantity;
        return true;
    }

    public int GetStock(string systemId, string resourceId)
    {
        var market = GetMarket(systemId);
        return (int)market.Stocks.GetValueOrDefault(resourceId, 0f);
    }

    public bool HasResource(string systemId, string resourceId)
    {
        var market = GetMarket(systemId);
        return market.ProductionRates.ContainsKey(resourceId) || market.Demands.ContainsKey(resourceId);
    }

    public void Tick(float dt)
    {
        foreach (var kv in _markets)
        {
            var market = kv.Value;
            foreach (var pkv in market.ProductionRates)
            {
                string resId = pkv.Key;
                float rate = pkv.Value;
                float cap = rate * 120f;
                float current = market.Stocks.GetValueOrDefault(resId, 0f);
                if (current < cap)
                {
                    current += rate * dt;
                    if (current > cap) current = cap;
                    market.Stocks[resId] = current;
                }
            }
        }
    }

    private SystemMarketState GetMarket(string systemId)
    {
        if (_markets.TryGetValue(systemId, out var market))
            return market;
        var newMarket = new SystemMarketState();
        _markets[systemId] = newMarket;
        return newMarket;
    }

    private float GetBasePrice(string resourceId)
    {
        var res = _galaxy.FindResource(resourceId);
        return res?.BasePrice ?? 10;
    }

    private int GetVolume(string resourceId)
    {
        var res = _galaxy.FindResource(resourceId);
        return res?.Volume ?? 1;
    }

    private static void AddToInventory(List<InventoryEntry> items, string id, int qty)
    {
        var existing = items.FirstOrDefault(i => i.Id == id);
        if (existing != null)
            existing.Quantity += qty;
        else
            items.Add(new InventoryEntry { Id = id, Quantity = qty });
    }

    private static void RemoveFromInventory(List<InventoryEntry> items, string id, int qty)
    {
        var entry = items.FirstOrDefault(i => i.Id == id);
        if (entry == null) return;
        entry.Quantity -= qty;
        if (entry.Quantity <= 0)
            items.Remove(entry);
    }
}

public class SystemMarketState
{
    public Dictionary<string, float> Stocks { get; set; } = new();
    public Dictionary<string, float> Demands { get; set; } = new();
    public Dictionary<string, float> ProductionRates { get; set; } = new();
}
