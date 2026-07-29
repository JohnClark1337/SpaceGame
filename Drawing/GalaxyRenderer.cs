using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceGame.Models;
using SpaceGame.Systems;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;

namespace SpaceGame.Drawing;

public class GalaxyRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly SpriteFont _titleFont;

    public GalaxyRenderer(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, SpriteFont titleFont)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
    }

    public void DrawStarfield(Starfield starfield, GameTime gameTime, Vector2 offset, int screenW, int screenH)
    {
        foreach (var star in starfield.Stars)
        {
            float sx = star.X + offset.X;
            float sy = star.Y + offset.Y;
            if (sx >= 0 && sx < screenW && sy >= 0 && sy < screenH)
            {
                float brightness = star.Brightness;
                byte b = (byte)(brightness * 255);
                _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                    (int)sx, (int)sy, star.Size, star.Size),
                    new Color(b, b, b, b));
            }
        }
    }

    public void DrawConnectionLines(
        Galaxy galaxy, RouteManager routeManager, Player player, GameTime gameTime,
        Dictionary<string, List<AttackState>> activeAttacks,
        Vector2 offset, int screenW, int screenH,
        bool isTraveling, int selectedConnectionIndex)
    {
        var drawn = new HashSet<(string, string)>();
        foreach (var sys in galaxy.Systems)
        {
            foreach (var connId in sys.Connections)
            {
                var key = string.Compare(sys.Id, connId, StringComparison.Ordinal) < 0
                    ? (sys.Id, connId) : (connId, sys.Id);
                if (drawn.Contains(key)) continue;
                drawn.Add(key);

                var other = galaxy.FindSystemById(connId);
                if (other == null) continue;

                float x1 = sys.X + offset.X;
                float y1 = sys.Y + offset.Y;
                float x2 = other.X + offset.X;
                float y2 = other.Y + offset.Y;

                bool blocked = routeManager.IsBlocked(sys.Id, connId);
                if (blocked)
                {
                    float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                    DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, new Color(200, 30, 30) * pulse);
                }
                else
                {
                    bool isCurrentRoute = (player.CurrentSystemId == sys.Id || player.CurrentSystemId == connId);
                    float lineAlpha = isCurrentRoute ? 0.4f : 0.15f;
                    DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, new Color(60, 80, 120) * lineAlpha);
                }
            }
        }

        // Highlight selected route on map
        if (!isTraveling && player.CurrentSystemId != null)
        {
            var currentSys = galaxy.FindSystemById(player.CurrentSystemId);
            if (currentSys != null)
            {
                var openConns = currentSys.Connections
                    .Where(id => !routeManager.IsBlocked(player.CurrentSystemId, id))
                    .ToList();
                if (selectedConnectionIndex < openConns.Count)
                {
                    var target = galaxy.FindSystemById(openConns[selectedConnectionIndex]);
                    if (target != null)
                    {
                        float x1 = currentSys.X + offset.X;
                        float y1 = currentSys.Y + offset.Y;
                        float x2 = target.X + offset.X;
                        float y2 = target.Y + offset.Y;
                        float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 0.2f + 0.8f;
                        DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, Color.Cyan * pulse);
                        DrawUtils.DrawLine(_spriteBatch, _pixel, x1, y1, x2, y2, Color.White * (pulse * 0.3f));
                    }
                }
            }
        }
    }

    public void DrawSystems(
        Galaxy galaxy, Player player, GameTime gameTime,
        Dictionary<string, List<AttackState>> activeAttacks,
        Vector2 offset, int screenW, int screenH)
    {
        foreach (var sys in galaxy.Systems)
        {
            float sx = sys.X + offset.X;
            float sy = sys.Y + offset.Y;

            if (sx < -150 || sx > screenW + 150 || sy < -150 || sy > screenH + 150)
                continue;

            Color color = DrawUtils.GetFactionColor(sys.Faction);
            float t = (float)gameTime.TotalGameTime.TotalSeconds;
            float pulse = MathF.Sin(t * 1.5f + sys.X * 0.01f) * 0.12f + 1f;
            float drawRadius = sys.Radius * pulse;

            var label = sys.Name;
            var labelSize = _font.MeasureString(label);

            // Outer glow
            for (int i = 5; i >= 0; i--)
            {
                float r = drawRadius + i * 10f;
                float alpha = 0.02f + i * 0.06f;
                DrawUtils.FillCircle(_spriteBatch, _pixel, sx, sy, r, color * MathF.Min(alpha, 0.5f));
            }

            // Core
            DrawUtils.FillCircle(_spriteBatch, _pixel, sx, sy, drawRadius * 0.7f, color * 0.9f);
            DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius * 0.7f, color);

            // Player's current system highlight
            if (player.CurrentSystemId == sys.Id)
            {
                float ringAlpha = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 8f, Color.Cyan * ringAlpha);
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 14f, Color.Cyan * ringAlpha * 0.3f);
            }

            // Quest target indicator
            bool isQuestTarget = galaxy.ActiveQuests.Any(q => q.ObjectiveType == "travel" && q.TargetSystem == sys.Id);
            if (isQuestTarget && player.CurrentSystemId != sys.Id)
            {
                float pulse2 = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 18f, Color.Gold * pulse2);
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 24f, Color.Gold * pulse2 * 0.3f);
            }

            // Under-attack indicator
            if (activeAttacks.ContainsKey(sys.Id) && player.CurrentSystemId != sys.Id)
            {
                float pulse3 = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 0.25f + 0.75f;
                Color atkColor = new Color(255, 120, 0) * pulse3;
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 14f, atkColor);
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, drawRadius + 22f, atkColor * 0.3f);
                var atkSz = _font.MeasureString("UNDER ATTACK");
                DrawUtils.DrawSpacedText(_spriteBatch, _font, "UNDER ATTACK",
                    sx - atkSz.X / 2f, sy - drawRadius - atkSz.Y - 10, atkColor);
            }

            // Distance hint
            float dist = Vector2.Distance(player.Position, new Vector2(sys.X, sys.Y));
            if (dist < 400f && player.CurrentSystemId != sys.Id)
            {
                string hint = $"{sys.Name}  -  {(int)dist}u";
                var hintSize = _font.MeasureString(hint);
                float hx = sx - hintSize.X / 2f;
                float hy = sy + drawRadius + labelSize.Y + 20f;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, hint,
                    hx, hy, Color.Gray * MathF.Max(0.2f, 1f - dist / 400f));
            }

            // Label
            float labelX = sx - labelSize.X / 2f;
            float labelY = sy + drawRadius + 6f;
            bool isCurrent = player.CurrentSystemId == sys.Id;
            byte bg = (byte)(isCurrent ? 60 : 20);
            _spriteBatch.Draw(_pixel,
                new Microsoft.Xna.Framework.Rectangle((int)labelX - 4, (int)labelY - 2,
                    (int)labelSize.X + 8, (int)labelSize.Y + 4),
                new Color(0, 0, 0, (int)bg));
            DrawUtils.DrawSpacedText(_spriteBatch, _font, label,
                labelX, labelY, isCurrent ? Color.Cyan : Color.White * 0.7f);
        }

        // Quest target markers
        foreach (var quest in galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && quest.TargetSystem != null)
            {
                var target = galaxy.FindSystemById(quest.TargetSystem);
                if (target != null)
                {
                    float tx = target.X + offset.X;
                    float ty = target.Y + offset.Y;
                    float pulse2 = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                    DrawUtils.DrawCircle(_spriteBatch, _pixel, tx, ty, target.Radius + 16f, Color.Gold * pulse2);
                    DrawUtils.DrawCircle(_spriteBatch, _pixel, tx, ty, target.Radius + 22f, Color.Gold * pulse2 * 0.3f);

                    float arrowY = ty - target.Radius - 26f;
                    DrawUtils.DrawLine(_spriteBatch, _pixel, tx - 6, arrowY + 8, tx, arrowY, Color.Gold * pulse2);
                    DrawUtils.DrawLine(_spriteBatch, _pixel, tx + 6, arrowY + 8, tx, arrowY, Color.Gold * pulse2);
                }
            }
        }
    }

    public void DrawShip(Vector2 center, Player player, Galaxy galaxy, RouteManager routeManager,
        bool isTraveling, Vector2 travelStartPos, Vector2 travelEndPos, int selectedConnectionIndex)
    {
        float angle;
        if (isTraveling)
        {
            float dx = travelEndPos.X - travelStartPos.X;
            float dy = travelEndPos.Y - travelStartPos.Y;
            angle = MathF.Atan2(dy, dx);
        }
        else if (player.CurrentSystemId != null)
        {
            var currentSys = galaxy.FindSystemById(player.CurrentSystemId);
            if (currentSys != null)
            {
                var connections = currentSys.Connections
                    .Where(id => !routeManager.IsBlocked(player.CurrentSystemId, id))
                    .ToList();
                if (selectedConnectionIndex < connections.Count)
                {
                    var target = galaxy.FindSystemById(connections[selectedConnectionIndex]);
                    if (target != null)
                    {
                        float dx = target.X - currentSys.X;
                        float dy = target.Y - currentSys.Y;
                        angle = MathF.Atan2(dy, dx);
                        player.Angle = angle;
                    }
                    else { angle = player.Angle; }
                }
                else { angle = player.Angle; }
            }
            else { angle = player.Angle; }
        }
        else
        {
            angle = player.Angle;
        }
        float len = 18f;

        var tip = center + Vector2.FromAngle(angle) * len;
        var left = center + Vector2.FromAngle(angle + 2.4f) * len * 0.65f;
        var right = center + Vector2.FromAngle(angle - 2.4f) * len * 0.65f;

        // Thrust flames
        float speed = player.Velocity.Length();
        if (speed > 20f)
        {
            float flameBase = MathF.Min(speed / 5f, 12f);
            Vector2[] origins = {
                left * 0.67f + right * 0.33f,
                (left + right) * 0.5f,
                left * 0.33f + right * 0.67f
            };
            for (int i = 0; i < 3; i++)
            {
                float fLen = flameBase * (i == 1 ? 1f : 0.5f);
                var ftip = origins[i] + Vector2.FromAngle(angle + MathF.PI) * fLen;
                var fside1 = origins[i] + Vector2.FromAngle(angle + MathF.PI + 0.3f) * 3f;
                var fside2 = origins[i] + Vector2.FromAngle(angle + MathF.PI - 0.3f) * 3f;
                DrawUtils.DrawLine(_spriteBatch, _pixel, ftip.X, ftip.Y, fside1.X, fside1.Y, Color.Orange);
                DrawUtils.DrawLine(_spriteBatch, _pixel, ftip.X, ftip.Y, fside2.X, fside2.Y, Color.Orange);
            }
        }

        // Main triangle hull
        DrawUtils.DrawLine(_spriteBatch, _pixel, tip.X, tip.Y, left.X, left.Y, Color.White);
        DrawUtils.DrawLine(_spriteBatch, _pixel, tip.X, tip.Y, right.X, right.Y, Color.White);
        DrawUtils.DrawLine(_spriteBatch, _pixel, left.X, left.Y, right.X, right.Y, Color.White);

        // Cockpit window
        float cp = 0.7f;
        var cockpit = center + Vector2.FromAngle(angle) * len * cp;
        float cs = 2f;
        var cf = cockpit + Vector2.FromAngle(angle) * cs;
        var cl = cockpit + Vector2.FromAngle(angle + 1.5f) * cs * 0.4f;
        var cr = cockpit + Vector2.FromAngle(angle - 1.5f) * cs * 0.4f;
        DrawUtils.DrawLine(_spriteBatch, _pixel, cf.X, cf.Y, cl.X, cl.Y, Color.Cyan * 0.6f);
        DrawUtils.DrawLine(_spriteBatch, _pixel, cf.X, cf.Y, cr.X, cr.Y, Color.Cyan * 0.6f);

        // Hull panel lines
        var rearMid = (left + right) * 0.5f;
        DrawUtils.DrawLine(_spriteBatch, _pixel, cockpit.X, cockpit.Y, rearMid.X, rearMid.Y, Color.White * 0.5f);

        float sideInset = len * 0.025f;
        var siL = cockpit + Vector2.FromAngle(angle + 1.5f) * sideInset;
        var siR = cockpit + Vector2.FromAngle(angle - 1.5f) * sideInset;
        float hullLen = (rearMid - cockpit).Length();
        var lEdge = (left - tip).Normalized();
        var rEdge = (right - tip).Normalized();
        var lEnd = siL + lEdge * hullLen * 0.8f;
        var rEnd = siR + rEdge * hullLen * 0.8f;
        DrawUtils.DrawLine(_spriteBatch, _pixel, siL.X, siL.Y, lEnd.X, lEnd.Y, Color.White * 0.35f);
        DrawUtils.DrawLine(_spriteBatch, _pixel, siR.X, siR.Y, rEnd.X, rEnd.Y, Color.White * 0.35f);
        DrawUtils.DrawLine(_spriteBatch, _pixel, lEnd.X, lEnd.Y, rEnd.X, rEnd.Y, Color.White * 0.35f);
    }

    public void DrawHUD(
        Player player, RouteManager routeManager, Galaxy galaxy, GameTime gameTime,
        Dictionary<string, List<AttackState>> activeAttacks,
        Vector2 galaxyPlayerPos,
        bool useLlm, bool isTraveling, string? travelDestId, float travelLerp,
        int selectedConnectionIndex, int screenW, int screenH)
    {
        // Top-left info
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Credits: {player.Credits}", 10, 10, Color.Yellow);

        // Right-side status
        float rightX = screenW - 300;
        float meterY = 10;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Fuel: {player.Fuel:F0}/{player.MaxFuel}",
            rightX, meterY, Color.Gray * 0.6f);
        meterY += 20;
        if (player.HasShield)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Shield: {player.ShieldHP:F0}/{player.MaxShieldHP:F0}",
                rightX, meterY, Color.CornflowerBlue);
            meterY += 20;
        }
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"HP: {player.Health}/{player.MaxHealth}",
            rightX, meterY, Color.Gray * 0.6f);
        meterY += 25;

        string diffStr = routeManager.Difficulty.ToString();
        int blockedCount = routeManager.CountBlocked;
        int maxBlocked = routeManager.MaxBlocked;
        Color aiColor = blockedCount > 0 ? new Color(255, 150, 100) : Color.Gray * 0.6f;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"AI [{diffStr}]  Blockades: {blockedCount}/{maxBlocked}",
            rightX, meterY, aiColor);

        // LLM commander notification
        if (useLlm)
        {
            string llmLabel = "LLM Commander Active";
            var llmSz = _font.MeasureString(llmLabel);
            float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2f) * 0.15f + 0.85f;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, llmLabel,
                (screenW - llmSz.X) / 2f, 6,
                new Color(100, 200, 255) * pulse);
        }

        if (isTraveling)
        {
            var destSys = galaxy.FindSystemById(travelDestId ?? "");
            string destName = destSys?.Name ?? "unknown";
            float pct = travelLerp * 100f;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Traveling to {destName}... {pct:F0}%",
                10, 30, Color.Cyan);
        }
        else if (player.CurrentSystemId != null)
        {
            var currentSys = galaxy.FindSystemById(player.CurrentSystemId);
            if (currentSys != null)
            {
                string info = $"Docked at: {currentSys.Name} [{currentSys.Faction}]";
                DrawUtils.DrawSpacedText(_spriteBatch, _font, info, 10, 30, Color.Cyan);
                DrawUtils.DrawSpacedText(_spriteBatch, _font, "[E] Enter System",
                    10, 50, Color.Gray * 0.7f);

                var connections = currentSys.Connections
                    .Select(id => galaxy.FindSystemById(id))
                    .Where(s => s != null)
                    .Cast<StarSystemData>()
                    .ToList();

                float routeY = 170;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, "--- Connections ---",
                    rightX, routeY, Color.Gold);
                routeY += 22;

                int selectableIdx = 0;
                foreach (var conn in connections)
                {
                    bool blocked = routeManager.IsBlocked(currentSys.Id, conn.Id);
                    float dist = Vector2.Distance(
                        new Vector2(currentSys.X, currentSys.Y),
                        new Vector2(conn.X, conn.Y));
                    float fuelCost = MathF.Max(25f, dist * 0.015f);
                    bool inRange = player.Fuel >= fuelCost && (player.Fuel - fuelCost) > player.MaxFuel / 3;
                    bool selected = !blocked && inRange && selectableIdx == selectedConnectionIndex;

                    Color c;
                    string prefix;
                    string suffix = "";
                    bool connUnderAttack = activeAttacks.ContainsKey(conn.Id);
                    if (blocked)
                    {
                        c = Color.Red * 0.5f;
                        prefix = "  ";
                        suffix = "  BLOCKED";
                    }
                    else if (connUnderAttack)
                    {
                        c = new Color(255, 150, 50) * 0.9f;
                        prefix = "  ";
                        suffix = "  UNDER ATTACK";
                    }
                    else if (!inRange)
                    {
                        c = Color.DimGray * 0.7f;
                        prefix = "  ";
                        suffix = "  OUT OF RANGE";
                    }
                    else if (selected)
                    {
                        c = Color.Yellow;
                        prefix = "> ";
                    }
                    else
                    {
                        c = Color.White * 0.8f;
                        prefix = "  ";
                    }
                    string label = $"{prefix}{conn.Name}  [{(int)dist}u]{suffix}";
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, label,
                        rightX, routeY, c);
                    routeY += 18;

                    if (!blocked && inRange) selectableIdx++;
                }
            }
        }

        // Active quests
        float y = isTraveling ? 55 : 80;
        if (galaxy.ActiveQuests.Count > 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "--- Active Quests ---", 10, y, Color.Gold);
            y += 20;
            foreach (var q in galaxy.ActiveQuests)
            {
                string status;
                if (q.ObjectiveType == "travel" && q.TargetSystem != null)
                {
                    var target = galaxy.FindSystemById(q.TargetSystem);
                    float dist = target != null
                        ? Vector2.Distance(player.Position, new Vector2(target.X, target.Y))
                        : 0;
                    status = $"Travel to {target?.Name ?? q.TargetSystem} [{dist:F0}u]";
                }
                else
                    status = q.Description;

                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {q.Name}: {status}", 10, y, Color.White * 0.9f);
                y += 18;
            }
        }

        // Owned upgrades
        if (player.OwnedUpgrades.Count > 0)
        {
            y += 10;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "--- Upgrades ---", 10, y, Color.Lime);
            y += 20;
            foreach (var upId in player.OwnedUpgrades)
            {
                var up = galaxy.AllUpgrades.FirstOrDefault(u => u.Id == upId);
                if (up != null)
                    DrawUtils.DrawSpacedText(_spriteBatch, _font, $"  {up.Name}", 10, y, Color.Lime * 0.8f);
                y += 18;
            }
        }

        // Controls hint
        string controls;
        if (isTraveling)
            controls = "Traveling...";
        else if (player.CurrentSystemId != null)
            controls = "Up/Down: Select Route | Enter: Travel | E: Enter System | Q: Quest Log | ESC: Pause";
        else
            controls = "Q: Quest Log | ESC: Pause";
        var controlsSize = _font.MeasureString(controls);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, controls,
            screenW / 2f - controlsSize.X / 2f, screenH - 25,
            Color.Gray * 0.5f);

        // Minimap
        DrawMinimap(galaxy, player, gameTime, screenW, screenH);
    }

    private void DrawMinimap(Galaxy galaxy, Player player, GameTime gameTime, int screenW, int screenH)
    {
        int mapX = screenW - 210;
        int mapY = 10;
        int mapW = 200;
        int mapH = 150;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(mapX, mapY, mapW, mapH),
            new Color(10, 10, 20, 180));
        DrawUtils.DrawRect(_spriteBatch, _pixel, mapX, mapY, mapW, mapH, new Color(60, 60, 80));

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var sys in galaxy.Systems)
        {
            if (sys.X < minX) minX = sys.X;
            if (sys.X > maxX) maxX = sys.X;
            if (sys.Y < minY) minY = sys.Y;
            if (sys.Y > maxY) maxY = sys.Y;
        }

        float rangeX = maxX - minX + 200;
        float rangeY = maxY - minY + 200;
        float scale = MathF.Min(mapW / rangeX, mapH / rangeY) * 0.9f;

        float cx = (minX + maxX) / 2f;
        float cy = (minY + maxY) / 2f;

        foreach (var sys in galaxy.Systems)
        {
            float sx = mapX + mapW / 2f + (sys.X - cx) * scale;
            float sy = mapY + mapH / 2f + (sys.Y - cy) * scale;
            Color c = DrawUtils.GetFactionColor(sys.Faction);
            DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, 3f, c);

            bool isCurrent = player.CurrentSystemId == sys.Id;
            bool isQuest = galaxy.ActiveQuests.Any(q => q.TargetSystem == sys.Id);
            if (isCurrent)
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, 5f, Color.Yellow);
            if (isQuest)
                DrawUtils.DrawCircle(_spriteBatch, _pixel, sx, sy, 5f, Color.Gold * 0.6f);
        }

        float px = mapX + mapW / 2f + (player.Position.X - cx) * scale;
        float py = mapY + mapH / 2f + (player.Position.Y - cy) * scale;
        DrawUtils.DrawCircle(_spriteBatch, _pixel, px, py, 3f, Color.White);
    }

    public void DrawStatusMessage(string message, float timer, int screenW, int screenH)
    {
        if (timer <= 0) return;
        string msg = message;
        float alpha = MathF.Min(1f, timer * 2f);
        var size = _titleFont.MeasureString(msg);
        float x = (screenW - size.X) / 2f;
        float y = screenH / 3f;

        _spriteBatch.Draw(_pixel,
            new Microsoft.Xna.Framework.Rectangle((int)x - 10, (int)y - 6,
                (int)size.X + 20, (int)size.Y + 12),
            new Color(0, 0, 0, (int)(160 * alpha)));

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, msg,
            x, y, Color.White * alpha);
    }
}
