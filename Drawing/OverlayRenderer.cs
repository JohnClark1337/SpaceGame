using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceGame.Models;
using SpaceGame.Systems;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace SpaceGame.Drawing;

public class OverlayRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly SpriteFont _titleFont;

    public OverlayRenderer(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, SpriteFont titleFont)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
    }

    public void DrawSystemMapOverlay(
        GameTime gameTime, Player player, Galaxy galaxy,
        SystemScene systemScene,
        int screenW, int screenH)
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 200));

        var sys = galaxy.CurrentSystem;
        if (sys == null)
        {
            var msg = "No system selected";
            var sz = _titleFont.MeasureString(msg);
            DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, msg, (screenW - sz.X) / 2f, screenH / 2f - sz.Y / 2f, Color.Gray);
            return;
        }

        string title = $"System: {sys.Name}";
        var titleSz = _titleFont.MeasureString(title);
        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, title, (screenW - titleSz.X) / 2f, 20, Color.Cyan);

        float cx = screenW / 2f;
        float cy = screenH / 2f + 20;
        float margin = MathF.Min(cx, cy) - 30;
        float systemRadius = 0f;
        foreach (var p in sys.Planets)
            if (p.OrbitRadius > systemRadius) systemRadius = p.OrbitRadius;
        if (sys.Station != null && sys.Station.OrbitRadius > systemRadius)
            systemRadius = sys.Station.OrbitRadius;
        systemRadius = MathF.Max(systemRadius, 1f) * 1.1f;
        float scale = margin / systemRadius;

        // Orbit rings
        foreach (var p in sys.Planets)
            DrawUtils.DrawCircle(_spriteBatch, _pixel, cx, cy, p.OrbitRadius * scale, new Color(60, 60, 80, 100));
        if (sys.Station != null)
            DrawUtils.DrawCircle(_spriteBatch, _pixel, cx, cy, sys.Station.OrbitRadius * scale, new Color(60, 80, 100, 100));

        // Star
        float starR = MathF.Max(sys.StarRadius * scale, 4f);
        DrawUtils.FillCircle(_spriteBatch, _pixel, cx, cy, starR, DrawUtils.ParseColor(sys.Color) * 0.5f);
        DrawUtils.DrawCircle(_spriteBatch, _pixel, cx, cy, starR, DrawUtils.ParseColor(sys.Color));

        // Planets
        float angle = 0;
        foreach (var p in sys.Planets)
        {
            float px = cx + MathF.Cos(angle) * p.OrbitRadius * scale;
            float py = cy + MathF.Sin(angle) * p.OrbitRadius * scale;
            float pr = MathF.Max(p.Radius * scale, 2f);
            DrawUtils.FillCircle(_spriteBatch, _pixel, px, py, pr, DrawUtils.ParseColor(p.Color));

            var lblSz = _font.MeasureString(p.Name);
            DrawUtils.DrawSpacedText(_spriteBatch, _font, p.Name, px - lblSz.X / 2f, py + pr + 4f, Color.White * 0.8f);
            angle += 1.5f;
        }

        // Station
        if (sys.Station != null)
        {
            float stAngle = sys.Station.Angle;
            float stx = cx + MathF.Cos(stAngle) * sys.Station.OrbitRadius * scale;
            float sty = cy + MathF.Sin(stAngle) * sys.Station.OrbitRadius * scale;
            float stR = MathF.Max(sys.Station.Radius * scale, 2f);
            Color stCol = sys.Hostility >= 3 ? new Color(200, 60, 60) : Color.LightBlue;
            DrawUtils.FillCircle(_spriteBatch, _pixel, stx, sty, stR, stCol);

            var stLbl = sys.Station.Name;
            var stSz = _font.MeasureString(stLbl);
            DrawUtils.DrawSpacedText(_spriteBatch, _font, stLbl, stx - stSz.X / 2f, sty + stR + 4f, Color.Cyan);
        }

        // Player position
        float plx = cx + player.Position.X * scale;
        float ply = cy + player.Position.Y * scale;
        DrawUtils.FillCircle(_spriteBatch, _pixel, plx, ply, 4f, Color.White);
        DrawUtils.DrawCircle(_spriteBatch, _pixel, plx, ply, 6f, Color.White);

        // Asteroids
        if (systemScene != null)
        {
            float aSize = 2f;
            foreach (var ast in systemScene.Asteroids)
            {
                float ax = cx + ast.Position.X * scale;
                float ay = cy + ast.Position.Y * scale;
                _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)(ax - aSize), (int)(ay - aSize), (int)(aSize * 2), (int)(aSize * 2)), Color.White * 0.5f);
            }
        }

        // Quest targets
        float qy = screenH - 60;
        foreach (var quest in galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && quest.TargetSystem == sys.Id)
            {
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Quest: {quest.Name}", 30, qy, Color.Gold);
                qy += 20;
            }
        }

        string hint = "[T] or [ESC] Close";
        var hintSz = _font.MeasureString(hint);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, hint, screenW - hintSz.X - 20, screenH - 30, Color.Gray * 0.7f);
    }

    public void DrawGalaxyMapOverlay(
        GameTime gameTime, Player player, Galaxy galaxy, RouteManager routeManager,
        Dictionary<string, List<AttackState>> activeAttacks,
        int screenW, int screenH)
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 200));

        string title = "Galaxy Map";
        var titleSz = _titleFont.MeasureString(title);
        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, title, (screenW - titleSz.X) / 2f, 20, Color.Cyan);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var sys in galaxy.Systems)
        {
            if (sys.X < minX) minX = sys.X;
            if (sys.X > maxX) maxX = sys.X;
            if (sys.Y < minY) minY = sys.Y;
            if (sys.Y > maxY) maxY = sys.Y;
        }

        float rangeX = maxX - minX + 400;
        float rangeY = maxY - minY + 400;
        float mapAreaW = screenW - 80;
        float mapAreaH = screenH - 120;
        float mapScale = MathF.Min(mapAreaW / rangeX, mapAreaH / rangeY) * 0.9f;

        float cx = (minX + maxX) / 2f;
        float cy2 = (minY + maxY) / 2f;
        float originX = screenW / 2f;
        float originY = screenH / 2f + 10;

        // Connection lines
        var drawn = new HashSet<(string, string)>();
        foreach (var sys in galaxy.Systems)
        {
            foreach (var conn in sys.Connections)
            {
                var key = string.Compare(sys.Id, conn, StringComparison.Ordinal) < 0
                    ? (sys.Id, conn) : (conn, sys.Id);
                if (drawn.Contains(key)) continue;
                drawn.Add(key);

                var other = galaxy.FindSystemById(conn);
                if (other == null) continue;

                float x1 = originX + (sys.X - cx) * mapScale;
                float y1 = originY + (sys.Y - cy2) * mapScale;
                float x2 = originX + (other.X - cx) * mapScale;
                float y2 = originY + (other.Y - cy2) * mapScale;

                bool blocked = routeManager.IsBlocked(sys.Id, conn);
                if (blocked)
                {
                    float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                    DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, new Color(200, 30, 30) * pulse);
                }
                else
                {
                    DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, new Color(60, 80, 120, 80));
                }
            }
        }

        // Systems
        foreach (var sys in galaxy.Systems)
        {
            float sx = originX + (sys.X - cx) * mapScale;
            float sy = originY + (sys.Y - cy2) * mapScale;

            Color color = DrawUtils.GetFactionColor(sys.Faction);
            DrawUtils.FillCircle(_spriteBatch, _pixel, sx, sy, MathF.Max(sys.Radius * mapScale * 0.6f, 3f), color * 0.8f);
            DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, MathF.Max(sys.Radius * mapScale * 0.6f, 3f), color);

            bool isCurrent = player.CurrentSystemId == sys.Id;
            bool isQuest = galaxy.ActiveQuests.Any(q => q.TargetSystem == sys.Id);

            if (isCurrent)
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, 8f, Color.Yellow);

            if (isQuest)
            {
                float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, 12f, Color.Gold * pulse);
            }

            if (activeAttacks.ContainsKey(sys.Id))
            {
                float pulse3 = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 0.25f + 0.75f;
                Color atkColor = new Color(255, 120, 0) * pulse3;
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, MathF.Max(sys.Radius * mapScale * 0.6f, 3f) + 6f, atkColor);
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, MathF.Max(sys.Radius * mapScale * 0.6f, 3f) + 10f, atkColor * 0.3f);
            }

            var lblSz = _font.MeasureString(sys.Name);
            float lx = sx - lblSz.X / 2f;
            float ly = sy + MathF.Max(sys.Radius * mapScale * 0.6f, 3f) + 4f;
            _spriteBatch.Draw(_pixel,
                new Microsoft.Xna.Framework.Rectangle((int)lx - 3, (int)ly - 1,
                    (int)lblSz.X + 6, (int)lblSz.Y + 3),
                new Color(0, 0, 0, 140));
            DrawUtils.DrawSpacedText(_spriteBatch, _font, sys.Name, lx, ly, isCurrent ? Color.White : Color.White * 0.8f);
        }

        // Player position
        float plx = originX + (player.Position.X - cx) * mapScale;
        float ply2 = originY + (player.Position.Y - cy2) * mapScale;
        DrawUtils.FillCircle(_spriteBatch, _pixel, plx, ply2, 4f, Color.White);
        DrawUtils.DrawCircle(_spriteBatch, _pixel, plx, ply2, 7f, Color.White);

        // Legend
        float legX = 20;
        float legY = screenH - 80;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Blockaded route  ---", legX, legY, new Color(200, 30, 30));
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Quest target  O", legX, legY + 18, Color.Gold);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Current system  O", legX, legY + 36, Color.Yellow);

        // AI status
        string aiInfo = $"AI [{routeManager.Difficulty}]  Blockades: {routeManager.CountBlocked}/{routeManager.MaxBlocked}";
        var aiSz = _font.MeasureString(aiInfo);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, aiInfo, screenW - aiSz.X - 20, 50, new Color(255, 150, 100));

        string hint = "[G] or [ESC] Close";
        var hintSz = _font.MeasureString(hint);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, hint, screenW - hintSz.X - 20, screenH - 30, Color.Gray * 0.7f);
    }

    public void DrawQuestLog(Galaxy galaxy, int questLogSelection, int screenW, int screenH)
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 180));

        int panelW = 640;
        int panelH = screenH - 80;
        int px = (screenW - panelW) / 2;
        int py = 40;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(10, 10, 30, 230));
        int textX = px + 20;
        int textY = py + 20;

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, "Quest Log", textX, textY, Color.Cyan);
        textY += 40;

        var active = galaxy.ActiveQuests;
        if (active.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No active quests.", textX, textY, Color.Gray);
        }
        else
        {
            for (int i = 0; i < active.Count; i++)
            {
                var q = active[i];
                bool selected = i == questLogSelection;
                string prefix = selected ? "> " : "  ";
                Color c = selected ? Color.Yellow : Color.White;

                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"{prefix}{q.Name}", textX, textY, c);
                textY += 22;

                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {q.Description}", textX, textY, c * 0.6f);
                textY += 20;

                string location = q.ObjectiveType == "travel"
                    ? $"Target: {q.TargetSystem}"
                    : $"Search {q.TargetSystem} for {q.TargetItem}";
                bool objectiveMet = galaxy.IsQuestObjectiveMet(q, null!);
                if (objectiveMet)
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {location} - Objective Complete!", textX, textY, Color.Lime);
                else
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {location}", textX, textY, c * 0.4f);
                textY += 26;
            }
        }

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Up/Dn] Scroll  [Q/ESC] Close", textX, panelH + py - 30, Color.Gray * 0.6f);
    }

    public void DrawInventoryOverlay(
        Player player, Galaxy galaxy, GameTime gameTime,
        int inventoryTab, int invScroll, int invSelection,
        bool equipSelectMode, int equipSelectSlotIdx, int equipSelectCursor,
        string invMsgText, float invMsgTimer,
        int screenW, int screenH)
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 180));

        int panelW = 800;
        int panelH = 600;
        int px = (screenW - panelW) / 2;
        int py = (screenH - panelH) / 2;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(15, 15, 35, 235));
        DrawUtils.DrawRect(_spriteBatch, _pixel, px, py, panelW, panelH, new Color(60, 60, 100));

        int textX = px + 20;
        int textY = py + 20;

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, "Inventory", textX, textY, Color.Cyan);
        textY += 50;

        int cap = player.CargoCapacity;
        if (player.OwnedUpgrades.Contains("cargo_v1")) cap = (int)(cap * 2.0f);
        int used = player.UsedCargo;
        Color capColor = used > cap ? Color.Red : Color.Gray;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Cargo: {used}/{cap}", px + panelW - 200, py + 25, capColor);
        if (used > cap)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "OVERWEIGHT!", px + panelW - 200, py + 45, Color.Red);
        }

        // Tabs
        string[] tabs = { "Quest Items", "Resources", "Equipment", "Upgrades", "Consumables" };
        float tabX = textX;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tc = i == inventoryTab ? Color.Yellow : Color.Gray;
            string tPrefix = i == inventoryTab ? "[ " : "  ";
            string tSuffix = i == inventoryTab ? " ]" : "  ";
            var sz = _font.MeasureString(tabs[i]);
            DrawUtils.DrawSpacedText(_spriteBatch, _font, tPrefix + tabs[i] + tSuffix, tabX, textY, tc);
            tabX += sz.X + 30;
        }
        textY += 40;

        var pos = new Vector2(textX, textY);
        float rightX = px + panelW - 20;

        switch (inventoryTab)
        {
            case 0: DrawInventoryQuestItems(pos, rightX, player); break;
            case 1: DrawInventoryResources(pos, rightX, player, galaxy); break;
            case 2: DrawInventoryEquipment(pos, rightX, player, galaxy, equipSelectMode, equipSelectSlotIdx, equipSelectCursor, invSelection); break;
            case 3: DrawInventoryUpgrades(pos, rightX, player, galaxy); break;
            case 4: DrawInventoryConsumables(pos, rightX, player, galaxy, invSelection); break;
        }

        // Message overlay
        if (invMsgTimer > 0f && !string.IsNullOrEmpty(invMsgText))
        {
            float msgW = 300f;
            float msgH = 40f;
            float msgX = (screenW - msgW) / 2f;
            float msgY = py + panelH + 30f;
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)msgX, (int)msgY, (int)msgW, (int)msgH),
                new Color(0, 0, 0, 200));
            var msgLabelSz = _font.MeasureString(invMsgText);
            DrawUtils.DrawSpacedText(_spriteBatch, _font, invMsgText, msgX + (msgW - msgLabelSz.X) / 2f, msgY + (msgH - msgLabelSz.Y) / 2f, Color.Lime);
        }
    }

    private void DrawInventoryQuestItems(Vector2 pos, float rightX, Player player)
    {
        float textY = pos.Y;
        if (player.QuestItems.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No quest items.", pos.X, textY, Color.Gray);
            return;
        }

        for (int i = 0; i < player.QuestItems.Count; i++)
        {
            var entry = player.QuestItems[i];
            string label = $"{entry.Id.Replace('_', ' ')} x{entry.Quantity}";
            DrawUtils.DrawSpacedText(_spriteBatch, _font, label, pos.X, textY, Color.Wheat);
            textY += 24;
        }
    }

    private void DrawInventoryResources(Vector2 pos, float rightX, Player player, Galaxy galaxy)
    {
        float textY = pos.Y;
        if (player.Resources.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No resources in cargo.", pos.X, textY, Color.Gray);
            return;
        }

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Item                          Qty    Value", pos.X, textY, Color.Gray * 0.6f);
        textY += 24;

        for (int i = 0; i < player.Resources.Count; i++)
        {
            var entry = player.Resources[i];
            var def = galaxy.FindResource(entry.Id);
            string name = def?.Name ?? entry.Id;
            int totalValue = (def?.BasePrice ?? 0) * entry.Quantity;
            string line = $"{name,-30} {entry.Quantity,-5} {totalValue}cr";
            DrawUtils.DrawSpacedText(_spriteBatch, _font, line, pos.X, textY, Color.White * 0.9f);
            textY += 22;
        }
    }

    private void DrawInventoryEquipment(
        Vector2 pos, float rightX, Player player, Galaxy galaxy,
        bool equipSelectMode, int equipSelectSlotIdx, int equipSelectCursor, int invSelection)
    {
        float textY = pos.Y;

        string[] slotLabels = { "Weapon 1", "Weapon 2", "Shield", "Engine", "Utility 1", "Utility 2" };
        string[] slotKeys = { "weapon1", "weapon2", "shield", "engine", "utility1", "utility2" };
        string[] slotFilters = { "weapon", "weapon", "shield", "engine", "utility", "utility" };

        if (equipSelectMode && equipSelectSlotIdx >= 0 && equipSelectSlotIdx < slotKeys.Length)
        {
            string slotLabel = slotLabels[equipSelectSlotIdx];
            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Select equipment for {slotLabel}:", pos.X, textY, Color.Cyan);
            textY += 28;

            string filter = slotFilters[equipSelectSlotIdx];
            string key = slotKeys[equipSelectSlotIdx];
            string? currentId = player.Equipment.ContainsKey(key) ? player.Equipment[key] : null;

            var optLabels = new List<string> { "None" };
            var optIds = new List<string> { "" };
            foreach (var entry in player.UnequippedEquipment)
            {
                var def = galaxy.FindEquipment(entry.Id);
                if (def != null && def.Slot == filter)
                {
                    optLabels.Add($"{def.Name} x{entry.Quantity}");
                    optIds.Add(entry.Id);
                }
            }

            float cx = pos.X + 30;
            for (int i = 0; i < optLabels.Count; i++)
            {
                bool isSelected = i == equipSelectCursor;
                bool isCurrent = (!string.IsNullOrEmpty(currentId) && optIds[i] == currentId);
                string prefix = isSelected ? "> " : "  ";
                Color c = isSelected ? Color.Yellow : (isCurrent ? Color.Lime : Color.White);
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"{prefix}{optLabels[i]}", cx, textY, c);
                if (isCurrent && !isSelected)
                {
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, "(equipped)",
                        cx + 180, textY, Color.Gray * 0.5f);
                }
                textY += 24;
            }

            textY += 8;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Enter] Select  [ESC] Cancel", pos.X, textY, Color.Gray * 0.5f);
            return;
        }

        for (int i = 0; i < slotKeys.Length; i++)
        {
            string slotLabel = slotLabels[i];
            bool filled = player.Equipment.ContainsKey(slotKeys[i]);
            string equipName = "";
            if (filled)
            {
                var def = galaxy.FindEquipment(player.Equipment[slotKeys[i]]);
                equipName = def?.Name ?? player.Equipment[slotKeys[i]];
            }

            bool selected = i == invSelection;
            string prefix = selected ? "> " : "  ";
            Color slotColor = selected ? Color.Yellow : Color.Gray;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"{prefix}{slotLabel}:", pos.X, textY, slotColor);
            string itemLabel = filled ? equipName : "--- empty ---";
            Color itemColor = filled ? Color.Lime : Color.Gray * 0.5f;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, itemLabel, pos.X + 130, textY, itemColor);

            if (selected)
            {
                DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Enter] Select", rightX - 130, textY, Color.Orange * 0.7f);
            }

            textY += 26;
        }

        textY += 8;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Unequipped:", pos.X, textY, Color.Gray * 0.7f);
        textY += 18;
        if (player.UnequippedEquipment.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "  None", pos.X, textY, Color.Gray * 0.4f);
            textY += 20;
        }
        else
        {
            int maxEq = Math.Min(player.UnequippedEquipment.Count, 5);
            for (int i = 0; i < maxEq; i++)
            {
                var entry = player.UnequippedEquipment[i];
                var def = galaxy.FindEquipment(entry.Id);
                string name = def?.Name ?? entry.Id;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {name} x{entry.Quantity}", pos.X, textY, Color.Gray * 0.6f);
                textY += 18;
            }
        }

        textY += 4;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Q/E] Switch tab  |  [I] or [ESC] Close", pos.X, textY, Color.Gray * 0.5f);
    }

    private void DrawInventoryUpgrades(Vector2 pos, float rightX, Player player, Galaxy galaxy)
    {
        float textY = pos.Y;

        if (player.OwnedUpgrades.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No upgrades purchased.", pos.X, textY, Color.Gray);
            return;
        }

        foreach (var upId in player.OwnedUpgrades)
        {
            var up = galaxy.AllUpgrades.FirstOrDefault(u => u.Id == upId);
            if (up == null) continue;

            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {up.Name}", pos.X, textY, Color.Lime);
            textY += 20;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"     {up.Description}", pos.X, textY, Color.Gray * 0.7f);
            textY += 24;
        }

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Q/E] Switch tab  |  [I] or [ESC] Close", pos.X, textY, Color.Gray * 0.5f);
    }

    private void DrawInventoryConsumables(Vector2 pos, float rightX, Player player, Galaxy galaxy, int invSelection)
    {
        float textY = pos.Y;
        if (player.Consumables.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No consumables.", pos.X, textY, Color.Gray);
            return;
        }

        for (int i = 0; i < player.Consumables.Count; i++)
        {
            var entry = player.Consumables[i];
            var def = galaxy.FindConsumable(entry.Id);
            string name = def?.Name ?? entry.Id;
            bool selected = i == invSelection;
            string prefix = selected ? "> " : "  ";
            Color itemColor = selected ? Color.Yellow : Color.White;
            string label = $"{prefix}{name} x{entry.Quantity}";
            DrawUtils.DrawSpacedText(_spriteBatch, _font, label, pos.X, textY, itemColor);

            if (selected && def != null)
            {
                string hint = "[Enter] Use";
                var hintSz = _font.MeasureString(hint);
                DrawUtils.DrawSpacedText(_spriteBatch, _font, hint, rightX - hintSz.X, textY, Color.Lime * 0.7f);
                DrawUtils.DrawSpacedText(_spriteBatch, _font, def.Description, pos.X + 20, textY + 22, Color.Gray * 0.7f);
            }

            textY += selected ? 40 : 24;
        }
    }

    public void DrawBroadcastDialog(
        List<GalacticBroadcast> pendingBroadcasts, GameTime gameTime,
        int broadcastScroll, int broadcastTab, int screenW, int screenH)
    {
        float dialogW = 600f;
        float dialogH = 400f;
        float dx = (screenW - dialogW) / 2f;
        float dy = (screenH - dialogH) / 2f;
        float contentTop = dy + 60f;
        float contentBottom = dy + dialogH - 40f;
        float contentH = contentBottom - contentTop;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 180));

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)dx, (int)dy, (int)dialogW, (int)dialogH),
            new Color(10, 10, 30, 240));
        DrawUtils.DrawRect(_spriteBatch, _pixel, dx, dy, dialogW, dialogH, new Color(80, 80, 140));

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, "GALACTIC BROADCAST", dx + 20, dy + 15, Color.Gold);

        // Tabs
        string[] tabs = { "All", "Empire", "Federation" };
        float tabW = dialogW / 3f;
        float tabY = dy + 45f;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tabColor = i == broadcastTab ? Color.White : Color.Gray * 0.5f;
            float tabX = dx + tabW * i;
            float tw = _font.MeasureString(tabs[i]).X;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, tabs[i], 
                tabX + (tabW - tw) / 2f, tabY, tabColor);
            if (i < tabs.Length - 1)
            {
                float sepX = tabX + tabW;
                DrawUtils.DrawLine(_spriteBatch, _pixel, sepX, tabY - 2f, sepX, tabY + 18f, Color.Gray * 0.3f);
            }
        }
        DrawUtils.DrawLine(_spriteBatch, _pixel, dx, tabY + 20f, dx + dialogW, tabY + 20f, Color.Gray * 0.3f);

        if (pendingBroadcasts.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No broadcasts received.", dx + 30f, contentTop, Color.Gray);
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "[ESC or B] Close", dx + 20, dy + dialogH - 30f, Color.Gray * 0.7f);
            return;
        }

        // Build lines
        var lines = new List<(string text, Color color, bool isHeader)>();
        foreach (var bc in pendingBroadcasts)
        {
            if (broadcastTab == 1 && bc.Faction != "Trigor Empire") continue;
            if (broadcastTab == 2 && bc.Faction != "Atlas Federation") continue;

            Color factionColor = bc.Faction == "Trigor Empire" ? new Color(220, 60, 60) : new Color(60, 140, 220);
            lines.Add((bc.Faction, factionColor, true));

            if (!string.IsNullOrEmpty(bc.CommanderName))
                lines.Add(($"{bc.CommanderName}, {bc.CommanderTitle}", factionColor * 0.8f, false));

            string msg = DrawUtils.SanitizeText(bc.Message);
            float wrapW = dialogW - 60f;
            float spaceW = 8f;
            var words = msg.Split(' ');
            string curLine = "";
            float lineW = 0f;
            foreach (var word in words)
            {
                float wordW = _font.MeasureString(word).X;
                bool wordTooWide = wordW > wrapW;

                if (curLine.Length == 0)
                {
                    if (wordTooWide)
                    {
                        string chunk = "";
                        float chunkW = 0f;
                        foreach (char c in word)
                        {
                            float cw = _font.MeasureString(c.ToString()).X;
                            if (chunkW + cw > wrapW && chunk.Length > 0)
                            {
                                lines.Add((chunk, Color.White * 0.9f, false));
                                chunk = c.ToString();
                                chunkW = cw;
                            }
                            else
                            {
                                chunk += c;
                                chunkW += cw;
                            }
                        }
                        curLine = chunk;
                        lineW = chunkW;
                    }
                    else
                    {
                        curLine = word;
                        lineW = wordW;
                    }
                }
                else if (lineW + spaceW + wordW > wrapW)
                {
                    lines.Add((curLine, Color.White * 0.9f, false));
                    curLine = word;
                    lineW = wordW;
                }
                else
                {
                    curLine += " " + word;
                    lineW += spaceW + wordW;
                }
            }
            if (curLine.Length > 0)
                lines.Add((curLine, Color.White * 0.9f, false));

            lines.Add(("", Color.Transparent, false));
        }

        if (lines.Count > 0 && lines[^1].text.Length == 0)
            lines.RemoveAt(lines.Count - 1);

        float totalH = 0f;
        foreach (var l in lines)
            totalH += l.isHeader ? 36f : (l.text.Length == 0 ? 12f : 24f);

        float maxScrollF = Math.Max(0f, totalH - contentH);
        int clampedScroll = (int)Math.Clamp(broadcastScroll, 0, (int)maxScrollF);

        // Draw with scissor
        _spriteBatch.End();
        var prevScissor = _spriteBatch.GraphicsDevice.ScissorRectangle;
        var rs = new RasterizerState { ScissorTestEnable = true };
        _spriteBatch.GraphicsDevice.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(
            (int)dx, (int)contentTop, (int)dialogW, (int)(contentBottom - contentTop));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, rs);

        float sy = contentTop - clampedScroll;
        float textX = dx + 30f;

        foreach (var l in lines)
        {
            float lineH = l.isHeader ? 36f : (l.text.Length == 0 ? 12f : 24f);
            if (sy + lineH >= contentTop && sy <= contentBottom && l.text.Length > 0)
            {
                if (l.isHeader)
                    DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, l.text, textX, sy, l.color);
                else
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, l.text, textX, sy, l.color);
            }
            sy += lineH;
        }

        _spriteBatch.End();
        _spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        if (clampedScroll > 0)
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "^", dx + dialogW - 25f, contentTop + 5f, Color.Gray * 0.6f);
        if (clampedScroll < maxScrollF)
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "v", dx + dialogW - 25f, contentBottom - 20f, Color.Gray * 0.6f);

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[ESC or B] Close     [Tab] Filter", dx + 20, dy + dialogH - 30f, Color.Gray * 0.7f);
    }

    public void DrawQuestDialog(
        QuestDialog? currentQuestDialog, ref List<string> wrappedLines,
        ref int questDialogScroll, int screenW, int screenH)
    {
        if (currentQuestDialog == null) return;
        float dialogW = 600f;
        float dialogH = 300f;
        float dx = (screenW - dialogW) / 2f;
        float dy = (screenH - dialogH) / 2f;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 200));
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)dx, (int)dy, (int)dialogW, (int)dialogH),
            new Color(10, 10, 30, 240));
        DrawUtils.DrawRect(_spriteBatch, _pixel, dx, dy, dialogW, dialogH, new Color(80, 80, 140));

        float textX = dx + 30f;
        float textY = dy + 20f;
        float maxTextW = dialogW - 60f;
        float lineH = 22f;

        if (!string.IsNullOrEmpty(currentQuestDialog.Speaker))
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, currentQuestDialog.Speaker, textX, textY, Color.Gold);
            textY += 30f;
        }

        float topArea = textY;
        float bottomArea = dy + dialogH - 35f;
        int maxVisible = (int)((bottomArea - topArea) / lineH);

        wrappedLines = DrawUtils.WordWrap(_font, currentQuestDialog.Text, maxTextW);
        questDialogScroll = Math.Clamp(questDialogScroll, 0,
            Math.Max(0, wrappedLines.Count - maxVisible));

        int visibleLines = Math.Min(maxVisible, wrappedLines.Count - questDialogScroll);
        float drawY = topArea;
        for (int i = questDialogScroll; i < questDialogScroll + visibleLines; i++)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, wrappedLines[i], textX, drawY, Color.White * 0.9f);
            drawY += lineH;
        }

        if (wrappedLines.Count > maxVisible)
        {
            float scrollBarH = (float)maxVisible / wrappedLines.Count * (bottomArea - topArea);
            float scrollY = topArea + (float)questDialogScroll / wrappedLines.Count * (bottomArea - topArea);
            float scrollX = dx + dialogW - 8f;
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                (int)scrollX, (int)topArea, 4, (int)(bottomArea - topArea)),
                new Color(40, 40, 60));
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                (int)scrollX, (int)scrollY, 4, (int)scrollBarH),
                new Color(140, 140, 180));
        }

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Enter/ESC/Space] Continue" +
            (wrappedLines.Count > maxVisible ? " | Up/Down scroll" : ""),
            dx + 20, dy + dialogH - 30f, Color.Gray * 0.7f);
    }
}
