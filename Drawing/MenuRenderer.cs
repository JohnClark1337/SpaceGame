using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceGame.Models;
using SpaceGame.Systems;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace SpaceGame.Drawing;

public class MenuRenderer
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly SpriteFont _titleFont;
    private readonly GalaxyRenderer _galaxyRenderer;

    public MenuRenderer(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, SpriteFont titleFont)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
        _galaxyRenderer = new GalaxyRenderer(spriteBatch, pixel, font, titleFont);
    }

    public void DrawMenu(
        string currentMenu, object menuSystem,
        int menuSelection, int priceScroll, int systemInfoScroll, ref int systemInfoMaxScroll,
        Player player, Galaxy galaxy, RouteManager routeManager, GameTime gameTime,
        Dictionary<string, List<AttackState>> activeAttacks,
        int screenW, int screenH)
    {
        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 140));

        int panelH = currentMenu == "SystemInfo" ? 640 : 500;
        int panelW = 700;
        int px = (screenW - panelW) / 2;
        int py = (screenH - panelH) / 2;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(20, 20, 40, 230));
        DrawUtils.DrawRect(_spriteBatch, _pixel, px, py, panelW, panelH, new Color(80, 80, 120));

        int textX = px + 20;
        int textY = py + 20;

        switch (currentMenu)
        {
            case "Pause":
                DrawPauseMenu(textX, ref textY, menuSelection);
                break;
            case "SystemInfo":
                DrawSystemInfo(textX, ref textY, px, py, panelW, panelH, menuSystem,
                    ref menuSelection, ref priceScroll, ref systemInfoScroll, ref systemInfoMaxScroll,
                    player, galaxy, routeManager, gameTime, activeAttacks, screenW, screenH);
                break;
            case "UpgradeShop":
                DrawUpgradeShop(textX, ref textY, menuSystem, menuSelection, player, galaxy);
                break;
            case "QuestBoard":
                DrawQuestBoard(textX, ref textY, menuSystem, menuSelection, galaxy);
                break;
            case "Controls":
                // Controls is drawn full-screen, handled separately
                break;
        }
    }

    private void DrawPauseMenu(int textX, ref int textY, int menuSelection)
    {
        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, "Paused",
            textX, textY, Color.Cyan);
        textY += 60;

        string[] options = { "New Game", "Save Game", "Load Game", "Training Mode", "Controls", "Quit" };
        for (int i = 0; i < options.Length; i++)
        {
            bool selected = menuSelection == i;
            Color c = selected ? Color.Yellow : Color.Gray;
            string prefix = selected ? "> " : "  ";
            DrawUtils.DrawSpacedText(_spriteBatch, _font, prefix + options[i],
                textX, textY, c);
            textY += 30;
        }

        textY += 20;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Enter] Select  |  [ESC] Resume",
            textX, textY, Color.Gray * 0.6f);
    }

    private void DrawSystemInfo(
        int textX, ref int textY, int px, int py, int panelW, int panelH,
        object menuSystem,
        ref int menuSelection, ref int priceScroll, ref int systemInfoScroll, ref int systemInfoMaxScroll,
        Player player, Galaxy galaxy, RouteManager routeManager, GameTime gameTime,
        Dictionary<string, List<AttackState>> activeAttacks,
        int screenW, int screenH)
    {
        var sys = menuSystem as StarSystemData;
        if (sys == null) return;
        int scrollOffset = -systemInfoScroll;

        // Scissor region for scrolling content
        _spriteBatch.End();
        var prevRect = _spriteBatch.GraphicsDevice.ScissorRectangle;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            null, new RasterizerState { ScissorTestEnable = true });
        _spriteBatch.GraphicsDevice.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(
            px + 1, py + 1, panelW - 2, panelH - 2);

        // System name
        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, sys.Name, textX, textY + scrollOffset, DrawUtils.ParseColor(sys.Color));
        textY += 48;

        // Description
        float descWidth = panelW - 60;
        var descLines = DrawUtils.WordWrap(_font, sys.Description, descWidth);
        foreach (var line in descLines)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, line, textX, textY + scrollOffset, Color.White * 0.8f);
            textY += 20;
        }
        textY += 8;

        // Faction
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Faction: {sys.Faction ?? "None"}", textX, textY + scrollOffset, Color.Cyan);
        textY += 20;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Hostility Level: {sys.Hostility}/10", textX, textY + scrollOffset,
            sys.Hostility > 3 ? Color.OrangeRed : Color.LimeGreen);
        textY += 20;

        if (activeAttacks.ContainsKey(sys.Id))
        {
            float pulse = MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 3f) * 0.2f + 0.8f;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "*** UNDER ATTACK ***", textX, textY + scrollOffset,
                new Color(255, 120, 0) * pulse);
            textY += 22;
        }

        if (sys.Services.Count > 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "Services: " + string.Join(", ", sys.Services), textX, textY + scrollOffset, Color.Yellow * 0.9f);
            textY += 26;
        }

        // Mini system map
        DrawMiniSystemMap(px, py, panelW, panelH, sys);

        textY += 12;

        // Resource price comparison
        var currentSys = galaxy.CurrentSystem;
        if (currentSys != null && currentSys.Id != sys.Id)
        {
            DrawPriceComparison(textX, ref textY, ref priceScroll, scrollOffset, px, py, panelW, panelH,
                sys, currentSys, galaxy);
        }
        else if (currentSys != null && currentSys.Id == sys.Id)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "-- Current system --",
                textX, textY + scrollOffset, Color.Gray * 0.6f);
            textY += 22;
        }

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        _spriteBatch.GraphicsDevice.ScissorRectangle = prevRect;

        int shortcutAreaY = py + panelH - 50;
        systemInfoMaxScroll = Math.Max(0, textY - shortcutAreaY);
        systemInfoScroll = Math.Min(systemInfoScroll, systemInfoMaxScroll);

        float sx = px + 20;
        float sy = shortcutAreaY;
        bool hasQuests = galaxy.AvailableQuests.Any(q => q.GiverSystem == sys.Id);
        if (hasQuests)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Tab] View Quests",
                sx, sy, Color.Gray);
            sy += 22;
        }
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[ESC] Close",
            sx, sy, Color.Gray);
    }

    private void DrawMiniSystemMap(int px, int py, int panelW, int panelH, StarSystemData sys)
    {
        float mapCx = px + panelW - 105;
        float mapCy = py + panelH - 105;
        float mapR = 85;
        DrawUtils.DrawRect(_spriteBatch, _pixel, mapCx - mapR - 3, mapCy - mapR - 3, mapR * 2 + 6, mapR * 2 + 6, new Color(60, 60, 100));

        float miniSysR = 0f;
        foreach (var p in sys.Planets)
            if (p.OrbitRadius > miniSysR) miniSysR = p.OrbitRadius;
        if (sys.Station != null && sys.Station.OrbitRadius > miniSysR)
            miniSysR = sys.Station.OrbitRadius;
        miniSysR = MathF.Max(MathF.Max(miniSysR, 1f) * 1.1f, sys.StarRadius * 2f);
        float mScale = mapR / miniSysR;

        float mStarR = MathF.Max(sys.StarRadius * mScale, 3f);
        DrawUtils.FillCircle(_spriteBatch, _pixel, mapCx, mapCy, mStarR, DrawUtils.ParseColor(sys.Color) * 0.5f);
        DrawUtils.DrawCircle(_spriteBatch, _pixel, mapCx, mapCy, mStarR, DrawUtils.ParseColor(sys.Color));

        foreach (var p in sys.Planets)
            DrawUtils.DrawCircle(_spriteBatch, _pixel, mapCx, mapCy, p.OrbitRadius * mScale, new Color(60, 60, 80, 100));
        if (sys.Station != null)
            DrawUtils.DrawCircle(_spriteBatch, _pixel, mapCx, mapCy, sys.Station.OrbitRadius * mScale, new Color(60, 80, 100, 100));

        float ang = 0;
        foreach (var p in sys.Planets)
        {
            float ppx = mapCx + MathF.Cos(ang) * p.OrbitRadius * mScale;
            float ppy = mapCy + MathF.Sin(ang) * p.OrbitRadius * mScale;
            DrawUtils.FillCircle(_spriteBatch, _pixel, ppx, ppy, MathF.Max(p.Radius * mScale, 2f), DrawUtils.ParseColor(p.Color));
            ang += 1.5f;
        }

        if (sys.Station != null)
        {
            float stAngle = sys.Station.Angle;
            float stx = mapCx + MathF.Cos(stAngle) * sys.Station.OrbitRadius * mScale;
            float sty = mapCy + MathF.Sin(stAngle) * sys.Station.OrbitRadius * mScale;
            Color stCol = sys.Hostility >= 3 ? new Color(200, 60, 60) : Color.LightBlue;
            DrawUtils.FillCircle(_spriteBatch, _pixel, stx, sty, MathF.Max(sys.Station.Radius * mScale, 2f), stCol);
        }

        string mapLabel = "System";
        var mapSz = _font.MeasureString(mapLabel);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, mapLabel,
            mapCx - mapSz.X / 2f, mapCy + mapR + 8, Color.Gray * 0.7f);
    }

    private void DrawPriceComparison(
        int textX, ref int textY, ref int priceScroll, int scrollOffset,
        int px, int py, int panelW, int panelH,
        StarSystemData sys, StarSystemData currentSys, Galaxy galaxy)
    {
        float scale = 1.3f;
        int pageSize = 5;

        DrawUtils.DrawSpacedText(_spriteBatch, _font, "--- Market Prices vs Current ---",
            textX, textY + scrollOffset, Color.Gold, scale);
        textY += (int)(26 * scale);

        float col1 = textX;
        float col2 = textX + 155;
        float col3 = textX + 365;
        float col4 = textX + 575;
        float tableW = 620;
        int lineH = (int)(20 * scale);
        int headerSize = (int)(18 * scale);

        // Header
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Resource", col1, textY + scrollOffset, Color.Gray * 0.7f, scale);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Selected (B/S)", col2 + 18, textY + scrollOffset, Color.Gray * 0.7f, scale);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Current (B/S)", col3 + 18, textY + scrollOffset, Color.Gray * 0.7f, scale);
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "Action", col4 + 18, textY + scrollOffset, Color.Gray * 0.7f, scale);
        float headerY = textY;
        textY += headerSize;

        float hdrLine = textY + scrollOffset - 4;
        DrawUtils.DrawLine(_spriteBatch, _pixel, col1, hdrLine, col1 + tableW, hdrLine, new Color(80, 80, 120) * 0.5f);

        float sepBottom = textY + pageSize * lineH + 4;
        DrawUtils.DrawLine(_spriteBatch, _pixel, col2 - 2, headerY + scrollOffset - 2, col2 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);
        DrawUtils.DrawLine(_spriteBatch, _pixel, col3 - 2, headerY + scrollOffset - 2, col3 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);
        DrawUtils.DrawLine(_spriteBatch, _pixel, col4 - 2, headerY + scrollOffset - 2, col4 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);

        var allRes = galaxy.AllResources;
        int total = allRes.Count;
        int maxScroll = Math.Max(0, total - pageSize);
        if (priceScroll > maxScroll) priceScroll = maxScroll;

        for (int i = priceScroll; i < priceScroll + pageSize && i < total; i++)
        {
            var res = allRes[i];
            int hereBuy = galaxy.Economy.GetBuyPrice(sys.Id, res.Id);
            int hereSell = galaxy.Economy.GetSellPrice(sys.Id, res.Id);
            int curBuy = galaxy.Economy.GetBuyPrice(currentSys.Id, res.Id);
            int curSell = galaxy.Economy.GetSellPrice(currentSys.Id, res.Id);

            DrawUtils.DrawSpacedText(_spriteBatch, _font, $"[{res.Symbol}] {res.Name}",
                col1, textY + scrollOffset, Color.White * 0.7f, scale);

            string price = $"{hereBuy}/{hereSell}";
            float pw = _font.MeasureString(price).X * scale;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, price,
                col2 + (182 - pw) / 2f, textY + scrollOffset, Color.White * 0.7f, scale);

            string curPrice = $"{curBuy}/{curSell}";
            float cw = _font.MeasureString(curPrice).X * scale;
            DrawUtils.DrawSpacedText(_spriteBatch, _font, curPrice,
                col3 + (182 - cw) / 2f, textY + scrollOffset, Color.White * 0.7f, scale);

            Color c = Color.White * 0.75f;
            string hint = "";
            if (curBuy < hereSell) { hint = "BUY"; c = Color.LightGreen; }
            else if (hereBuy < curSell) { hint = "SELL"; c = Color.Orange; }

            if (hint != "")
            {
                float hw = _font.MeasureString(hint).X * scale;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, hint,
                    col4 + (40 - hw) / 2f, textY + scrollOffset, c, scale);
            }

            float rowLine = textY + lineH;
            DrawUtils.DrawLine(_spriteBatch, _pixel, col1, rowLine + scrollOffset, col1 + tableW, rowLine + scrollOffset, new Color(60, 60, 90) * 0.3f);
            textY += lineH;
        }

        // Empty rows
        int drawn = Math.Min(pageSize, total - priceScroll);
        for (int i = drawn; i < pageSize; i++)
        {
            float rowLine = textY + lineH;
            DrawUtils.DrawLine(_spriteBatch, _pixel, col1, rowLine + scrollOffset, col1 + tableW, rowLine + scrollOffset, new Color(60, 60, 90) * 0.3f);
            textY += lineH;
        }

        // Scroll bar
        if (maxScroll > 0)
        {
            float scrollBarX = col1 + tableW + 6;
            float scrollBarH = pageSize * lineH;
            float thumbH = scrollBarH * pageSize / total;
            float thumbY = textY - scrollBarH + priceScroll * (scrollBarH - thumbH) / maxScroll;

            DrawUtils.DrawLine(_spriteBatch, _pixel, scrollBarX, textY - scrollBarH + scrollOffset, scrollBarX, textY + scrollOffset, new Color(60, 60, 100) * 0.5f);
            DrawUtils.DrawRect(_spriteBatch, _pixel, scrollBarX - 2, thumbY + scrollOffset, 4, thumbH, new Color(120, 120, 180) * 0.7f);
        }

        textY += 8;
    }

    private void DrawUpgradeShop(int textX, ref int textY, object menuSystem, int menuSelection, Player player, Galaxy galaxy)
    {
        var sys = menuSystem as StarSystemData;
        if (sys == null) return;
        var upgrades = galaxy.GetAvailableUpgradesForSystem(sys.Id, player);

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, $"{sys.Name}  -  Upgrade Shop",
            textX, textY, Color.Yellow);
        textY += 50;

        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"Your Credits: {player.Credits}",
            textX, textY, Color.Gold);
        textY += 30;

        if (upgrades.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No upgrades available.", textX, textY, Color.Gray);
        }
        else
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                var up = upgrades[i];
                bool selected = menuSelection == i;
                bool canAfford = player.Credits >= up.Cost;
                Color nameColor = selected ? (canAfford ? Color.Lime : Color.Red) : (canAfford ? Color.White : Color.Gray * 0.5f);
                string prefix = selected ? "> " : "  ";
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"{prefix}{up.Name}  -  {up.Cost}cr",
                    textX, textY, nameColor);
                textY += 20;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"     {up.Description}",
                    textX, textY, Color.White * 0.5f);
                textY += 24;
            }
        }

        textY += 20;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[Enter] Buy  |  [ESC] Back",
            textX, textY, Color.Gray * 0.7f);
    }

    private void DrawQuestBoard(int textX, ref int textY, object menuSystem, int menuSelection, Galaxy galaxy)
    {
        var sys = menuSystem as StarSystemData;
        if (sys == null) return;
        var quests = galaxy.AvailableQuests.Where(q => q.GiverSystem == sys.Id).ToList();

        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, $"{sys.Name}  -  Quest Board",
            textX, textY, Color.Gold);
        textY += 50;

        if (quests.Count == 0)
        {
            DrawUtils.DrawSpacedText(_spriteBatch, _font, "No quests available here.",
                textX, textY, Color.Gray);
        }
        else
        {
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                bool selected = menuSelection == i;
                Color c = selected ? Color.Yellow : Color.White;
                string prefix = selected ? "> " : "  ";
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"{prefix}{q.Name}",
                    textX, textY, c);
                textY += 20;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"     {q.Description}",
                    textX, textY, Color.White * 0.5f);
                textY += 18;
                DrawUtils.DrawSpacedText(_spriteBatch, _font, $"     Reward: {q.RewardCredits}cr" +
                    (q.RewardUpgrade != null ? $" + {q.RewardUpgrade}" : ""),
                    textX, textY, Color.Yellow * 0.7f);
                textY += 24;
            }
        }

        textY += 20;
        DrawUtils.DrawSpacedText(_spriteBatch, _font, "[ESC] Back",
            textX, textY, Color.Gray * 0.7f);
    }

    public void DrawControls(int menuSelection, int controlsScroll, int screenW, int screenH)
    {
        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 200));

        string title = "Controls";
        var titleSz = _titleFont.MeasureString(title);
        DrawUtils.DrawSpacedText(_spriteBatch, _titleFont, title,
            (screenW - titleSz.X) / 2f, 30, Color.Cyan);

        string[] lines = {
            "Galaxy View",
            "  Up / W        Select route up",
            "  Down / S      Select route down",
            "  Enter         Travel along selected route",
            "  E             Enter system",
            "",
            "System View (Flight)",
            "  W / Up        Thrust forward",
            "  S / Down      Thrust backward",
            "  A / Left      Rotate left",
            "  D / Right     Rotate right",
            "  Shift         Boost",
            "  E             Dock / Undock / Interact",
            "  F             Fire primary weapon",
            "  Space         Auto-fire (hold)",
            "  1 Key         Weapon 1 (Cannon)",
            "  2 Key         Weapon 2 (Laser)",
            "  3 Key         Weapon 3 (Missile)",
            "  Tab           Target nearest enemy",
            "  R             Repair",
            "  C             Use Energy Canister",
            "  O             Cycle combat target",
            "  N             Target nearest enemy",
            "",
            "System View (Docked)",
            "  Up / W        Navigate up",
            "  Down / S      Navigate down",
            "  Enter         Buy / Select",
            "  Back          Sell",
            "  U             Upgrade shop",
            "  ESC           Undock",
            "",
            "Training Mode",
            "  F1            Spawn menu",
            "  Up / W        Navigate spawn menu",
            "  Down / S      Navigate spawn menu",
            "  Enter         Spawn selected ship",
            "  Y             Sacrifice health",
            "  ESC           Pause / Resume",
            "",
            "General",
            "  ESC           Pause menu / Back",
            "  T             System map",
            "  G             Galaxy map",
            "  I             Inventory",
            "  Q             Quest log",
            "  F5            Quick save",
            "  F9            Quick load",
        };

        float lx = (screenW - 500) / 2f;
        float ly = 80 - controlsScroll * 20;
        foreach (var line in lines)
        {
            bool isHeader = line.Length > 0 && line[0] != ' ';
            Color c = isHeader ? new Color(255, 200, 100) : Color.Gray * 0.9f;
            if (ly + 20 > 20 && ly < screenH)
                DrawUtils.DrawSpacedText(_spriteBatch, _font, line,
                    lx, ly, c);
            ly += isHeader ? 24 : 20;
        }

        string scrollHint = controlsScroll > 0 ? " [Up] Scroll up" : "";
        DrawUtils.DrawSpacedText(_spriteBatch, _font, $"[ESC] Back{scrollHint}  [Down] Scroll down",
            screenW - 280, screenH - 30,
            Color.Gray * 0.6f);
    }
}
