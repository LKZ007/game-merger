-- Frankenstein's sandbox menu for Cyberpunk 2077 (a Cyber Engine Tweaks mod), built in so it needs no
-- downloads: every item, every car into your garage, cheats (god mode, eddies, heal, wanted level, time,
-- weather) and a fly/teleport tool. Items and cars that mods add (TweakXL / ArchiveXL) are listed too.
-- Open the CET overlay (~) to see it, or bind "Frankenstein item menu" in CET's Bindings.
-- Frankenstein writes settings.json next to this file before each launch.

local menu = { open = false, items = {}, shown = {}, cats = { "All" }, cat = "All", search = "", last = nil,
               overlay = false, allowed = true, note = "", cars = {}, carSearch = "", god = false, saved = nil,
               power = 1.0, brakes = 1.0, tuned = {}, race = nil, best = nil, hud = true, clock = 0, seq = 0 }

-- the Forza garage: real cars already in Cyberpunk plus the fastest ones, matched loosely so game updates don't break it
local GARAGE = { { "porsche", "Porsche 911 Turbo (a real car, in the base game)" }, { "caliburn", "Rayfield Caliburn" },
                 { "aerondight", "Rayfield Aerondight" }, { "turbo_r", "Quadra Turbo-R" }, { "type66", "Quadra Type-66" },
                 { "shion", "Mizutani Shion" }, { "outlaw", "Herrera Outlaw" }, { "hozuki", "Makigai Hozuki" },
                 { "arch", "Arch Nazare (bike)" } }

local function readSettings()
    local f = io.open("settings.json", "r")
    if not f then return end
    local ok, s = pcall(json.decode, f:read("*a"))
    f:close()
    if ok and s and s.item_menu == false then menu.allowed = false end
    if ok and s then menu.s = s end
end

-- This merge: the rundown, Creative (its own saves) and Play as, inside the game.
-- Creative needs the game to start again with other saves: Frankenstein reads switch.json when the game closes.
local function switchTo(to)
    local f = io.open("switch.json", "w")
    if not f then menu.note = "Couldn't ask Frankenstein to switch"; return end
    f:write(json.encode({ to = to })); f:close()
    menu.note = "Save first. The game closes and Frankenstein starts it again in " .. (to == "creative" and "Creative" or "your normal game") .. "."
    menu.quitAt = menu.now + 4
end

local function playAs(on)
    local pa = menu.s and menu.s.play_as
    if not pa or (pa.style or "") == "" then return end
    menu.playing = on
    -- what Cyberpunk can give V: the other game's toughness and speed (V's body stays V's)
    local tough = pa.style == "superhero" or pa.style == "knight"
    pcall(function()
        local gm, id = Game.GetGodModeSystem(), Game.GetPlayer():GetEntityID()
        if on and tough then gm:AddGodMode(id, gameGodModeType.Invulnerable, CName.new("FrankensteinPlayAs"))
        else gm:RemoveGodMode(id, gameGodModeType.Invulnerable, CName.new("FrankensteinPlayAs")) end
    end)
    menu.note = on and ("Playing as " .. (pa.who or "the other game's hero") .. ": " .. (pa.moves or "")) or "Back to V"
end

-- ---------- Carry over my save: the Extension's character (powers stay on; money comes once per card) ----------
local carry = { done = false, slow = false, tough = false }

local function carryCard() return menu.s and menu.s.carry and menu.s.carry.id and menu.s.carry or nil end
local function hasPower(p)
    local c = carryCard()
    if not c then return false end
    for _, x in ipairs(c.powers or {}) do if x == p then return true end end
    return false
end

local function carryTick()
    local c = carryCard()
    if not c or carry.done or not Game.GetPlayer() then return end
    carry.done = true
    local f = io.open("carry-done.json", "r")
    local had = f and f:read("*a") or ""
    if f then f:close() end
    if had ~= c.id then
        if hasPower("money") and (c.money or 0) > 0 then pcall(function() Game.AddToInventory("Items.money", c.money) end) end
        local w = io.open("carry-done.json", "w")
        if w then w:write(c.id); w:close() end
        menu.note = (c.name ~= "" and c.name or "Your character") .. " from " .. (c.from or "") .. " is here" ..
            ((hasPower("money") and (c.money or 0) > 0) and (" with " .. c.money .. " eddies") or "")
    end
    if hasPower("tough") then
        carry.tough = true
        pcall(function() Game.GetGodModeSystem():AddGodMode(Game.GetPlayer():GetEntityID(), gameGodModeType.Invulnerable, CName.new("FrankensteinCarry")) end)
    end
end

local function drawCarry()
    local c = carryCard()
    if not c then return end
    ImGui.Separator()
    ImGui.Text("Your character: " .. (c.name ~= "" and c.name or "from " .. (c.from or "")) .. ((c.level or 0) > 0 and ("  ·  level " .. c.level) or ""))
    for _, line in ipairs(c.card or {}) do ImGui.TextWrapped("· " .. line) end
    if hasPower("slowmo") and ImGui.Button(carry.slow and "Stop slowing time" or "Slow time") then
        carry.slow = not carry.slow
        pcall(function()
            if carry.slow then Game.GetTimeSystem():SetTimeDilation(CName.new("Frankenstein"), 0.3)
            else Game.GetTimeSystem():UnsetTimeDilation(CName.new("Frankenstein"), "None") end
        end)
    end
    if hasPower("tough") then
        ImGui.SameLine()
        if ImGui.Button(carry.tough and "Tough: on" or "Tough: off") then
            carry.tough = not carry.tough
            pcall(function()
                local gm, id = Game.GetGodModeSystem(), Game.GetPlayer():GetEntityID()
                if carry.tough then gm:AddGodMode(id, gameGodModeType.Invulnerable, CName.new("FrankensteinCarry"))
                else gm:RemoveGodMode(id, gameGodModeType.Invulnerable, CName.new("FrankensteinCarry")) end
            end)
        end
    end
    if hasPower("ghost") then
        ImGui.SameLine()
        if ImGui.Button("Lose the police") then
            pcall(function() Game.GetScriptableSystemsContainer():Get(CName.new("PreventionSystem")):ChangeHeatStage(EPreventionHeatStage.Heat_0, "Frankenstein") end)
        end
    end
end

local drawWorld  -- the World and events section, defined further down

local function drawMerge()
    local s = menu.s or {}
    ImGui.TextWrapped((s.merge or "This merge") .. (s.creative and "  ·  Creative (its own saves)" or ""))
    ImGui.Separator()
    local to = s.creative and "normal" or "creative"
    if menu.confirm == to then
        ImGui.TextWrapped("Save your game first (pause menu > Save). Then the game closes and starts again in " ..
            (to == "creative" and "Creative, with its own saves." or "your normal game, with your normal saves."))
        if ImGui.Button(to == "creative" and "Yes, switch to Creative now" or "Yes, back to my normal game now") then menu.confirm = nil; switchTo(to) end
        ImGui.SameLine()
        if ImGui.Button("Cancel") then menu.confirm = nil end
    elseif ImGui.Button(s.creative and "Back to my normal game" or "Switch to Creative (every item, own saves)") then
        menu.confirm = to
    end
    ImGui.TextWrapped("Creative keeps its own saves, so your normal game is never touched.")
    local pa = s.play_as
    if pa and (pa.style or "") ~= "" then
        ImGui.Separator()
        ImGui.Text("Play as " .. (pa.who or ""))
        ImGui.TextWrapped((pa.moves or "") .. ". How close: " .. (pa.close or ""))
        if ImGui.Button(menu.playing and "Back to V" or "Play as them") then playAs(not menu.playing) end
    end
    pcall(drawCarry)
    pcall(drawWorld)
    ImGui.Separator()
    ImGui.Text("Rundown")
    for _, line in ipairs(s.rundown or {}) do ImGui.TextWrapped(line) end
end

local function nameOf(rec)
    local ok, n = pcall(function() return Game.GetLocalizedTextByKey(rec:DisplayName()) end)
    if ok and n and n ~= "" then return n end
    return nil
end

local function load()
    menu.items, menu.cats = {}, { "All" }
    local seen = {}
    local ok, recs = pcall(function() return TweakDB:GetRecords("gamedataItem_Record") end)
    if not ok or not recs then return end
    for _, rec in ipairs(recs) do
        local id = TDBID.ToStringDEBUG(rec:GetID())
        local name = nameOf(rec)
        if id and name and not id:find("Dummy") then
            local cat = "Other"
            pcall(function() cat = rec:ItemCategory():Name().value end)
            pcall(function() if cat == "Other" or cat == "" then cat = rec:ItemType():Type().value end end)
            cat = tostring(cat):gsub("^Prt_", ""):gsub("^Wea_", "Weapon: "):gsub("^Clo_", "Clothing: ")
            table.insert(menu.items, { id = id, name = name, cat = cat, find = (name .. " " .. id .. " " .. cat):lower() })
            if not seen[cat] then seen[cat] = true; table.insert(menu.cats, cat) end
        end
    end
    table.sort(menu.items, function(a, b) return a.name < b.name end)
    table.sort(menu.cats, function(a, b) return a == "All" or (b ~= "All" and a < b) end)
    menu.last = nil
end

local function filter()
    menu.shown = {}
    local words = {}
    for w in menu.search:lower():gmatch("%S+") do table.insert(words, w) end
    for _, it in ipairs(menu.items) do
        if menu.cat == "All" or it.cat == menu.cat then
            local hit = true
            for _, w in ipairs(words) do if not it.find:find(w, 1, true) then hit = false; break end end
            if hit then table.insert(menu.shown, it) end
        end
    end
    menu.last = menu.search .. "|" .. menu.cat
end

local function loadCars()
    menu.cars = {}
    local ok, recs = pcall(function() return TweakDB:GetRecords("gamedataVehicle_Record") end)
    if not ok or not recs then return end
    for _, rec in ipairs(recs) do
        local id = TDBID.ToStringDEBUG(rec:GetID())
        if id and id:find("_player") then
            local name = nameOf(rec) or id:gsub("^Vehicle%.", "")
            table.insert(menu.cars, { id = id, name = name, find = (name .. " " .. id):lower() })
        end
    end
    table.sort(menu.cars, function(a, b) return a.name < b.name end)
end

-- every cheat is wrapped: if a game update renames something, the button says so instead of breaking
local function try(label, fn)
    local ok, err = pcall(fn)
    menu.note = ok and label or (label .. " didn't work in this game version (" .. tostring(err):sub(1, 80) .. ")")
end

local function player() return Game.GetPlayer() end

local function teleport(dx, dy, dz)
    local p = player()
    local pos = p:GetWorldPosition()
    Game.GetTeleportationFacility():Teleport(p, Vector4.new(pos.x + dx, pos.y + dy, pos.z + dz, 1), p:GetWorldOrientation():ToEulerAngles())
end

-- ---------- Forza tuning: changes the game's car data (TweakDB); a car called after tuning drives with it ----------
local function eachEngine(fn)
    for _, c in ipairs(menu.cars) do
        pcall(function()
            local eng = TweakDB:GetFlat(c.id .. ".vehEngineData")
            if eng then fn(c, TDBID.ToStringDEBUG(eng)) end
        end)
    end
end

local function tune()
    local n = 0
    eachEngine(function(c, eng)
        local key = eng .. ".engineMaxTorque"
        if menu.tuned[key] == nil then menu.tuned[key] = TweakDB:GetFlat(key) end
        local base = menu.tuned[key]
        if base and TweakDB:SetFlat(key, base * menu.power) then n = n + 1 end
    end)
    for _, c in ipairs(menu.cars) do
        pcall(function()
            local dm = TweakDB:GetFlat(c.id .. ".driveModelData")
            if not dm then return end
            local key = TDBID.ToStringDEBUG(dm) .. ".brakingFrictionFactor"
            if menu.tuned[key] == nil then menu.tuned[key] = TweakDB:GetFlat(key) end
            if menu.tuned[key] then TweakDB:SetFlat(key, menu.tuned[key] * menu.brakes) end
        end)
    end
    if n == 0 then error("this game version names its car data differently") end
    return n
end

-- ---------- street races: a timed sprint to a point across the city ----------
local atan2 = math.atan2 or math.atan  -- CET's Lua has atan2; newer Lua spells it atan(y, x)
menu.now = 0
local function startRace(dist)
    local p = Game.GetPlayer()
    local pos = p:GetWorldPosition()
    local a = math.random() * math.pi * 2
    menu.race = { x = pos.x + math.cos(a) * dist, y = pos.y + math.sin(a) * dist, start = menu.now, dist = dist,
                  prize = math.floor(dist * 12) }
end

local function raceTick()
    local r = menu.race
    if not r then return end
    local p = Game.GetPlayer()
    if not p then return end
    local pos = p:GetWorldPosition()
    local dx, dy = r.x - pos.x, r.y - pos.y
    r.left = math.sqrt(dx * dx + dy * dy)
    local f = p:GetWorldForward()
    local ang = math.deg(atan2(dy, dx) - atan2(f.y, f.x)) % 360
    local arrows = { "↑", "↖", "←", "↙", "↓", "↘", "→", "↗" }
    r.arrow = arrows[math.floor((ang + 22.5) / 45) % 8 + 1]
    r.time = menu.now - r.start
    if r.left < 30 then
        local par = r.dist / 40  -- seconds a quick drive takes
        local prize = r.time <= par and r.prize * 2 or r.prize
        pcall(function() Game.AddToInventory("Items.money", prize) end)
        menu.note = string.format("Race won in %.1f s! +%d eddies%s", r.time, prize, r.time <= par and " (beat the par time: double)" or "")
        if not menu.best or r.time < menu.best then menu.best = r.time end
        menu.race = nil
    end
end

-- ---------- World and events: the Extension game built into Night City from Cyberpunk's own cars ----------
-- The festival (or any merge's zone) is placed where you stand and kept in festival.json, so it comes back every
-- session. Events start from it: speed trap, drift zone and circuit, on top of the street races above.
local world = { spot = nil, spawned = {}, ev = nil, last = nil }

local function worldStyle() return (menu.s and menu.s.world and menu.s.world.style) or "" end

local function saveSpot()
    local f = io.open("festival.json", "w")
    if f then f:write(json.encode(world.spot or {})); f:close() end
end

local function loadSpot()
    local f = io.open("festival.json", "r")
    if not f then return end
    local ok, v = pcall(json.decode, f:read("*a")); f:close()
    if ok and v and v.x then world.spot = v end
end

local function zoneCars()
    local out = {}
    for _, g in ipairs(GARAGE) do
        for _, c in ipairs(menu.cars) do
            if #out < 8 and c.id:lower():find(g[1], 1, true) then table.insert(out, c.id); break end
        end
    end
    for _, c in ipairs(menu.cars) do if #out < 8 then table.insert(out, c.id) end end
    return out
end

local function spawnZone()
    local sp = world.spot
    if not sp then return 0 end
    local n = 0
    for i, rec in ipairs(zoneCars()) do
        local a = (i - 1) / 8 * math.pi * 2
        local ok, id = pcall(function()
            local spec = DynamicEntitySpec.new()
            spec.recordID = rec
            spec.position = Vector4.new(sp.x + math.cos(a) * 14, sp.y + math.sin(a) * 14, sp.z + 0.5, 1)
            spec.orientation = EulerAngles.new(0, 0, math.deg(a) + 90):ToQuat()
            spec.alwaysSpawned = true
            return Game.GetDynamicEntitySystem():CreateEntity(spec)
        end)
        if ok and id then table.insert(world.spawned, id); n = n + 1 end
    end
    return n
end

local function clearZone()
    for _, id in ipairs(world.spawned) do pcall(function() Game.GetDynamicEntitySystem():DeleteEntity(id) end) end
    world.spawned = {}
end

local function buildZoneHere()
    local pos = Game.GetPlayer():GetWorldPosition()
    clearZone()
    world.spot = { x = pos.x, y = pos.y, z = pos.z }
    saveSpot()
    local n = spawnZone()
    menu.note = n > 0 and ("Built the " .. ((menu.s and menu.s.world and menu.s.world.zone) or "zone"):match("^[^:]+") .. " here (" .. n .. " cars)")
        or "This game version wouldn't place cars here; the spot is saved for events."
end

local function lighting()
    try("Festival light", function()
        Game.GetWeatherSystem():SetWeather("24h_weather_sunny", 10, 0)
        Game.GetTimeSystem():SetGameTimeByHMS(18, 30, 0)
    end)
end

local function startEvent(kind)
    local p = Game.GetPlayer()
    local pos = p:GetWorldPosition()
    local c = world.spot or { x = pos.x, y = pos.y, z = pos.z }
    local f = p:GetWorldForward()
    if kind == "trap" then
        world.ev = { kind = kind, name = "Speed trap", x = c.x + f.x * 400, y = c.y + f.y * 400, start = menu.now }
    elseif kind == "drift" then
        world.ev = { kind = kind, name = "Drift zone", start = menu.now, score = 0, len = 30 }
    else
        local pts = {}
        for i = 0, 3 do
            local a = i / 4 * math.pi * 2
            table.insert(pts, { x = c.x + math.cos(a) * 300, y = c.y + math.sin(a) * 300 })
        end
        world.ev = { kind = "circuit", name = "Circuit", pts = pts, at = 1, lap = 1, laps = 2, start = menu.now }
    end
    menu.note = world.ev.name .. " started"
end

local function arrowTo(x, y)
    local p = Game.GetPlayer()
    local pos, f = p:GetWorldPosition(), p:GetWorldForward()
    local dx, dy = x - pos.x, y - pos.y
    local ang = math.deg(atan2(dy, dx) - atan2(f.y, f.x)) % 360
    local arrows = { "↑", "↖", "←", "↙", "↓", "↘", "→", "↗" }
    return arrows[math.floor((ang + 22.5) / 45) % 8 + 1], math.sqrt(dx * dx + dy * dy)
end

local function pay(n, msg)
    pcall(function() Game.AddToInventory("Items.money", n) end)
    menu.note = msg .. "  +" .. n .. " eddies"
    world.ev = nil
end

local function worldTick(dt)
    if world.spot and #world.spawned == 0 and not world.tried then
        local pos = Game.GetPlayer():GetWorldPosition()
        local dx, dy = pos.x - world.spot.x, pos.y - world.spot.y
        if dx * dx + dy * dy < 300 * 300 then world.tried = true; spawnZone() end
    end
    local e = world.ev
    if not e then return end
    local p = Game.GetPlayer()
    local v = Game.GetMountedVehicle(p)
    local pos = p:GetWorldPosition()
    local kmh = 0
    if v then pcall(function() kmh = math.abs(v:GetCurrentSpeed()) * 3.6 end) end
    e.time = menu.now - e.start
    if e.kind == "trap" then
        e.arrow, e.left = arrowTo(e.x, e.y)
        if e.left < 20 then
            local stars = kmh >= 250 and 3 or kmh >= 200 and 2 or kmh >= 150 and 1 or 0
            pay(500 + stars * 1000, string.format("Speed trap: %d km/h  %s", kmh, string.rep("★", stars) .. string.rep("☆", 3 - stars)))
        end
    elseif e.kind == "drift" then
        if world.last and v then
            local mx, my = pos.x - world.last.x, pos.y - world.last.y
            local d = math.sqrt(mx * mx + my * my)
            local f = v:GetWorldForward()
            if d > 1 then
                local cos = (mx * f.x + my * f.y) / d
                local slide = math.deg(math.acos(math.max(-1, math.min(1, cos))))
                if slide > 12 and slide < 100 and kmh > 30 then e.score = e.score + math.floor(kmh * slide / 10) end
            end
        end
        if e.time >= e.len then pay(math.floor(e.score / 10), "Drift zone: " .. e.score .. " points") end
    else
        local pt = e.pts[e.at]
        e.arrow, e.left = arrowTo(pt.x, pt.y)
        if e.left < 30 then
            e.at = e.at + 1
            if e.at > #e.pts then e.at = 1; e.lap = e.lap + 1 end
            if e.lap > e.laps then pay(3000, string.format("Circuit done in %.1f s", e.time)) end
        end
    end
    world.last = { x = pos.x, y = pos.y }
end

drawWorld = function()
    local w = menu.s and menu.s.world
    if not w then return end
    ImGui.Separator()
    ImGui.Text("World")
    ImGui.TextWrapped(w.zone or "")
    if ImGui.Button(world.spot and "Move it here" or "Build it here") then try("Built", buildZoneHere) end
    if world.spot then
        ImGui.SameLine()
        if ImGui.Button("Go there") then
            try("At the zone", function()
                local p, s = Game.GetPlayer(), world.spot
                Game.GetTeleportationFacility():Teleport(p, Vector4.new(s.x + 20, s.y, s.z + 1, 1), p:GetWorldOrientation():ToEulerAngles())
            end)
        end
    end
    if (w.lighting or "") ~= "" then
        if ImGui.Button("Light: " .. w.lighting) then lighting() end
    end
    ImGui.Separator()
    ImGui.Text("Events")
    for _, ev in ipairs({ { "Speed trap", "trap" }, { "Drift zone (30 s)", "drift" }, { "Circuit (2 laps)", "circuit" } }) do
        if ImGui.Button(ev[1]) then try(ev[1], function() startEvent(ev[2]) end) end
        ImGui.SameLine()
    end
    ImGui.NewLine()
    for _, m in ipairs(w.modes or {}) do ImGui.TextWrapped("· " .. m) end
end

-- ---------- the merge's story: quests from its characters (Frankenstein writes story.json) ----------
local story = { pack = nil, q = 1, s = 1, base = 0, day = 0, had = false, msg = nil, msgUntil = 0, done = false }

local function readJson(name)
    local f = io.open(name, "r")
    if not f then return nil end
    local ok, v = pcall(json.decode, f:read("*a"))
    f:close()
    return ok and v or nil
end

local function saveStory()
    local f = io.open("progress.json", "w")
    if f then f:write(json.encode({ title = story.pack.title, q = story.q, s = story.s, done = story.done })); f:close() end
end

local function money()
    local n = 0
    pcall(function() n = Game.GetTransactionSystem():GetItemQuantity(Game.GetPlayer(), MarketSystem.Money()) end)
    return n
end

local function gameDay()
    local d = 0
    pcall(function() d = Game.GetTimeSystem():GetGameTime():Days() end)
    return d
end

local function heat()
    local h = 0
    pcall(function() h = Game.GetScriptableSystemsContainer():Get(CName.new("PreventionSystem")):GetHeatStageAsInt() end)
    return h
end

local function who(q)
    for _, c in ipairs(story.pack.characters or {}) do
        if c.id == q.giver then return ((c.first or "") .. " " .. (c.last or "")):gsub("%s+$", "") end
    end
    return "Unknown"
end

local function say(from, text)
    story.msg = from .. ": " .. (text or "")
    story.msgUntil = menu.now + 12
end

local function startStep()
    story.base, story.day, story.had = money(), gameDay(), false
    saveStory()
end

local function storyTick()
    local pk = story.pack
    if not pk or story.done or not Game.GetPlayer() then return end
    local q = pk.quests[story.q]
    if not q then story.done = true; saveStory(); return end
    if story.s == 0 then say(who(q), q.start_text); story.s = 1; startStep(); return end
    local st = q.steps[story.s]
    if not st then
        local r = q.reward or {}
        if (r.cash or 0) > 0 then pcall(function() Game.AddToInventory("Items.money", r.cash) end) end
        say(who(q), (q.done_text or "Nice work.") .. ((r.cash or 0) > 0 and (" (+" .. r.cash .. " eddies)") or ""))
        story.q, story.s = story.q + 1, 0
        if not pk.quests[story.q] then story.done = true; say("Frankenstein", "Story complete: " .. (pk.title or "")) end
        saveStory()
        return
    end
    local amount = math.max(1, st.amount or 1)
    local ok = false
    if st.type == "text" then say(who(q), st.text); ok = true
    elseif st.type == "earn" then ok = money() - story.base >= amount
    elseif st.type == "cash" then ok = money() >= amount
    elseif st.type == "wanted" then ok = heat() >= math.min(5, amount)
    elseif st.type == "escape" then if heat() > 0 then story.had = true end; ok = story.had and heat() == 0
    elseif st.type == "days" then ok = gameDay() - story.day >= amount
    end  -- takedown and buy (or if the game can't tell): press "I did it" in the Story tab
    if ok then story.s = story.s + 1; startStep() end
end

local function loadStory()
    local pk = readJson("story.json")
    if not pk or not pk.quests or #pk.quests == 0 then return end
    story.pack = pk
    local pr = readJson("progress.json")
    if pr and pr.title == pk.title then story.q, story.s, story.done = pr.q or 1, pr.s or 0, pr.done or false
    else story.q, story.s = 1, 0; say("Frankenstein", (pk.title or "") .. ": " .. (pk.intro or "")) end
end

-- ---------- Frankenstein's Spawner (the app) sends requests through a file next to this one ----------
local function catalog()
    local f = io.open("catalog.json", "w")
    if not f then return end
    local items, cars = {}, {}
    for _, it in ipairs(menu.items) do table.insert(items, { id = it.id, name = it.name, cat = it.cat }) end
    for _, c in ipairs(menu.cars) do table.insert(cars, { id = c.id, name = c.name }) end
    f:write(json.encode({ items = items, cars = cars }))
    f:close()
end

local function bridge()
    local f = io.open("commands.json", "r")
    if not f then return end
    local ok, cmds = pcall(json.decode, f:read("*a"))
    f:close()
    if not ok or type(cmds) ~= "table" then return end
    for _, c in ipairs(cmds) do
        if (c.n or 0) > menu.seq then
            menu.seq = c.n
            if c.kind == "car" then
                pcall(function() Game.GetVehicleSystem():EnablePlayerVehicle(c.id, true, false) end)
                menu.note = "From Frankenstein: " .. (c.name or c.id) .. " is in your garage. Press V."
            else
                pcall(function() Game.AddToInventory(c.id, c.qty or 1) end)
                menu.note = "From Frankenstein: " .. (c.qty or 1) .. " x " .. (c.name or c.id)
            end
        end
    end
end

local function give(it, qty)
    local ok = pcall(function() Game.AddToInventory(it.id, qty) end)
    menu.note = ok and ("Got " .. qty .. " x " .. it.name) or ("Couldn't give " .. it.name)
end

registerForEvent("onInit", function()
    readSettings()
    load()
    loadCars()
    pcall(catalog)
    pcall(loadStory)
    pcall(loadSpot)
    -- requests sent before this session started don't count
    pcall(function()
        local f = io.open("commands.json", "r")
        if f then
            local ok, cmds = pcall(json.decode, f:read("*a")); f:close()
            if ok and type(cmds) == "table" then for _, c in ipairs(cmds) do menu.seq = math.max(menu.seq, c.n or 0) end end
        end
    end)
end)
registerForEvent("onUpdate", function(dt)
    menu.now = (menu.now or 0) + dt
    menu.clock = menu.clock + dt
    if menu.clock < 0.5 then return end
    menu.clock = 0
    if menu.quitAt and menu.now >= menu.quitAt then
        menu.quitAt = nil
        if not pcall(function() Game.GetSystemRequestsHandler():ExitGame() end) then
            menu.note = "Quit the game now: Frankenstein starts it again in the other mode."
        end
    end
    pcall(raceTick)
    pcall(worldTick)
    pcall(carryTick)
    pcall(bridge)
    pcall(storyTick)
end)
registerForEvent("onOverlayOpen", function() menu.overlay = true end)
registerForEvent("onOverlayClose", function() menu.overlay = false end)
registerHotkey("frankenstein_items", "Frankenstein sandbox menu", function()
    if menu.allowed then menu.open = not menu.open end
end)

local function drawHud()
    local kmh = nil
    pcall(function()
        local v = Game.GetMountedVehicle(Game.GetPlayer())
        if v then kmh = math.floor(math.abs(v:GetCurrentSpeed()) * 3.6 + 0.5) end
    end)
    local q = story.pack and not story.done and story.pack.quests[story.q]
    local line = story.msg and menu.now < story.msgUntil and story.msg or nil
    local goal = q and story.s > 0 and q.steps[story.s] and (q.title .. ": " .. q.steps[story.s].text) or nil
    local ev = world.ev
    local hint = menu.now < 25 and menu.s and (menu.s.creative and "Creative · press ~ > This merge to go back to your normal game"
        or "Press ~ > This merge to switch to Creative") or nil
    if not kmh and not menu.race and not line and not goal and not ev and not hint then return end
    ImGui.SetNextWindowPos(30, 30, ImGuiCond.FirstUseEver)
    ImGui.SetNextWindowBgAlpha(0.55)
    if ImGui.Begin("Frankenstein HUD", ImGuiWindowFlags.NoTitleBar + ImGuiWindowFlags.AlwaysAutoResize + ImGuiWindowFlags.NoScrollbar) then
        if kmh and worldStyle() == "forza" then
            -- Forza-style: big speed, a speed bar, the event
            ImGui.SetWindowFontScale(2.2)
            ImGui.Text(string.format("%3d", kmh))
            ImGui.SetWindowFontScale(1)
            ImGui.SameLine()
            ImGui.Text("KM/H")
            ImGui.ProgressBar(math.min(kmh / 300, 1), 220, 8, "")
        elseif kmh then ImGui.Text(string.format("%3d km/h   %3d mph", kmh, math.floor(kmh / 1.609 + 0.5))) end
        if ev then
            if ev.kind == "drift" then ImGui.Text(string.format("%s  %d pts  %2d s left", ev.name, ev.score or 0, math.max(0, ev.len - math.floor(ev.time or 0))))
            elseif ev.kind == "circuit" then ImGui.Text(string.format("%s  %s %4d m  lap %d/%d  %5.1f s", ev.name, ev.arrow or "", math.floor(ev.left or 0), ev.lap, ev.laps, ev.time or 0))
            else ImGui.Text(string.format("%s  %s %4d m", ev.name, ev.arrow or "", math.floor(ev.left or 0))) end
        end
        if hint then ImGui.TextWrapped(hint) end
        if line then ImGui.TextWrapped(line) end
        if goal then ImGui.TextWrapped(goal) end
        if menu.race and menu.race.left then
            ImGui.Text(string.format("RACE  %s %4d m   %5.1f s", menu.race.arrow or "", math.floor(menu.race.left), menu.race.time or 0))
        end
    end
    ImGui.End()
end

registerForEvent("onDraw", function()
    if menu.hud then pcall(drawHud) end
    if not (menu.overlay or menu.open) then return end
    if not menu.allowed then
        if menu.overlay then
            ImGui.SetNextWindowSize(520, 520, ImGuiCond.FirstUseEver)
            ImGui.Begin("Frankenstein · this merge")
            ImGui.TextWrapped(menu.note)
            pcall(drawMerge)
            ImGui.Separator()
            ImGui.TextWrapped("The item menu and spawner are off for normal play. Creative has them all.")
            ImGui.End()
        end
        return
    end
    ImGui.SetNextWindowSize(680, 600, ImGuiCond.FirstUseEver)
    if ImGui.Begin("Frankenstein sandbox · every item and car in this merge") then
        ImGui.Text(menu.note)
        if ImGui.BeginTabBar("frank_tabs") then
            if ImGui.BeginTabItem("This merge") then
                pcall(drawMerge)
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Items (" .. #menu.items .. ")") then
                if #menu.items == 0 and ImGui.Button("Load items") then load() end
                menu.search = ImGui.InputText("Search", menu.search, 100)
                if ImGui.BeginCombo("Category", menu.cat) then
                    for _, c in ipairs(menu.cats) do
                        if ImGui.Selectable(c, c == menu.cat) then menu.cat = c end
                    end
                    ImGui.EndCombo()
                end
                if menu.last ~= menu.search .. "|" .. menu.cat then filter() end
                ImGui.Text(#menu.shown .. " shown")
                ImGui.BeginChild("items", 0, 0, true)
                for i, it in ipairs(menu.shown) do
                    if i > 400 then ImGui.Text("… search to see more"); break end
                    if ImGui.Button("x1##" .. i) then give(it, 1) end
                    ImGui.SameLine()
                    if ImGui.Button("x10##" .. i) then give(it, 10) end
                    ImGui.SameLine()
                    ImGui.Text(it.name .. "  (" .. it.cat .. ")")
                end
                ImGui.EndChild()
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Cars (" .. #menu.cars .. ")") then
                ImGui.TextWrapped("Add a car to your garage, then call it: press V (or your Call vehicle button). Car mods you install show up here too.")
                if #menu.cars == 0 and ImGui.Button("Load cars") then loadCars() end
                menu.carSearch = ImGui.InputText("Search cars", menu.carSearch, 100)
                if ImGui.Button("Add every car to my garage") then
                    try("Every car is in your garage", function()
                        for _, c in ipairs(menu.cars) do Game.GetVehicleSystem():EnablePlayerVehicle(c.id, true, false) end
                    end)
                end
                ImGui.BeginChild("cars", 0, 0, true)
                local q = menu.carSearch:lower()
                for i, c in ipairs(menu.cars) do
                    if q == "" or c.find:find(q, 1, true) then
                        if ImGui.Button("Add##car" .. i) then
                            try(c.name .. " is in your garage. Press V to call it.", function()
                                Game.GetVehicleSystem():EnablePlayerVehicle(c.id, true, false)
                            end)
                        end
                        ImGui.SameLine()
                        ImGui.Text(c.name)
                    end
                end
                ImGui.EndChild()
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Cheats") then
                local god = ImGui.Checkbox("God mode (can't die)", menu.god)
                if god ~= menu.god then
                    menu.god = god
                    try(god and "God mode on" or "God mode off", function()
                        local gm, id = Game.GetGodModeSystem(), Game.GetPlayer():GetEntityID()
                        if god then gm:AddGodMode(id, gameGodModeType.Invulnerable, CName.new("Frankenstein"))
                        else gm:ClearGodMode(id, CName.new("Frankenstein")) end
                    end)
                end
                if ImGui.Button("+100,000 eddies") then try("+100,000 eddies", function() Game.AddToInventory("Items.money", 100000) end) end
                ImGui.SameLine()
                if ImGui.Button("Heal") then
                    try("Healed", function()
                        Game.GetStatPoolsSystem():RequestSettingStatPoolValue(player():GetEntityID(), gamedataStatPoolType.Health, 100, nil)
                    end)
                end
                ImGui.SameLine()
                if ImGui.Button("Lose the police") then
                    try("Police called off", function()
                        Game.GetScriptableSystemsContainer():Get(CName.new("PreventionSystem")):ChangeHeatStage(EPreventionHeatStage.Heat_0, "Frankenstein")
                    end)
                end
                ImGui.Separator()
                ImGui.Text("Time")
                for _, h in ipairs({ 6, 12, 18, 23 }) do
                    if ImGui.Button(string.format("%02d:00", h)) then
                        try("Time set", function() Game.GetTimeSystem():SetGameTimeByHMS(h, 0, 0) end)
                    end
                    ImGui.SameLine()
                end
                ImGui.NewLine()
                ImGui.Text("Weather")
                for _, w in ipairs({ { "Sunny", "24h_weather_sunny" }, { "Rain", "24h_weather_rain" }, { "Fog", "24h_weather_fog" }, { "Storm", "24h_weather_storm" } }) do
                    if ImGui.Button(w[1]) then
                        try("Weather: " .. w[1], function() Game.GetWeatherSystem():SetWeather(w[2], 5, 0) end)
                    end
                    ImGui.SameLine()
                end
                ImGui.NewLine()
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Garage") then
                ImGui.TextWrapped("Forza garage: the real cars already in Cyberpunk and the quickest ones, one click into your garage. Real-world car mods you add show up in the Cars tab.")
                for i, g in ipairs(GARAGE) do
                    local hit = nil
                    for _, c in ipairs(menu.cars) do if c.id:lower():find(g[1], 1, true) then hit = c; break end end
                    if hit then
                        if ImGui.Button("Add##g" .. i) then
                            try(g[2] .. " is in your garage. Press V to call it.", function()
                                Game.GetVehicleSystem():EnablePlayerVehicle(hit.id, true, false)
                            end)
                        end
                        ImGui.SameLine()
                        ImGui.Text(g[2])
                    end
                end
                if ImGui.Button("Add the whole Forza garage") then
                    try("The Forza garage is yours. Press V to call a car.", function()
                        for _, g in ipairs(GARAGE) do
                            for _, c in ipairs(menu.cars) do
                                if c.id:lower():find(g[1], 1, true) then Game.GetVehicleSystem():EnablePlayerVehicle(c.id, true, false); break end
                            end
                        end
                    end)
                end
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Tuning") then
                ImGui.TextWrapped("Forza-style tuning for every car. Call your car again (V) after changing it.")
                menu.power = ImGui.SliderFloat("Power", menu.power, 0.8, 2.5, "%.2fx")
                menu.brakes = ImGui.SliderFloat("Brakes", menu.brakes, 0.8, 2.0, "%.2fx")
                if ImGui.Button("Apply to every car") then try("Tuned. Call your car again to feel it.", function() tune() end) end
                ImGui.SameLine()
                if ImGui.Button("Back to normal") then
                    menu.power, menu.brakes = 1.0, 1.0
                    try("Cars are back to normal", function() tune() end)
                end
                menu.hud = ImGui.Checkbox("Speedometer while driving", menu.hud)
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Races") then
                ImGui.TextWrapped("Street races: get in a car and race to a point across Night City. The arrow on the speedometer points the way. Beat the par time for double the prize.")
                for _, r in ipairs({ { "Sprint (800 m)", 800 }, { "Cross-town (1.6 km)", 1600 }, { "Marathon (3 km)", 3000 } }) do
                    if ImGui.Button(r[1]) then try("Race on! Follow the arrow.", function() startRace(r[2]) end) end
                    ImGui.SameLine()
                end
                ImGui.NewLine()
                if menu.race and ImGui.Button("Give up this race") then menu.race = nil; menu.note = "Race cancelled" end
                if menu.best then ImGui.Text(string.format("Best time this session: %.1f s", menu.best)) end
                ImGui.EndTabItem()
            end
            if story.pack and ImGui.BeginTabItem("Story") then
                ImGui.TextWrapped((story.pack.title or "") .. " - " .. (story.pack.intro or ""))
                local q = story.pack.quests[story.q]
                if story.done then ImGui.Text("Story complete.")
                elseif q then
                    ImGui.Separator()
                    ImGui.TextWrapped("Quest " .. story.q .. " of " .. #story.pack.quests .. ": " .. q.title .. " (from " .. who(q) .. ")")
                    ImGui.TextWrapped(q.briefing or "")
                    for i, st in ipairs(q.steps) do
                        ImGui.Text((i < story.s and "[x] " or (i == story.s and "[>] " or "[ ] ")) .. st.text)
                    end
                    local st = q.steps[story.s]
                    if st and st.type ~= "text" and ImGui.Button("I did it") then story.s = story.s + 1; startStep() end
                end
                ImGui.EndTabItem()
            end
            if ImGui.BeginTabItem("Fly") then
                ImGui.TextWrapped("Move yourself around the map. For a free camera, use the game's own Photo mode from the pause menu.")
                if ImGui.Button("Up 20 m") then try("Up", function() teleport(0, 0, 20) end) end
                ImGui.SameLine()
                if ImGui.Button("Down 5 m") then try("Down", function() teleport(0, 0, -5) end) end
                ImGui.SameLine()
                if ImGui.Button("Forward 25 m") then
                    try("Forward", function()
                        local f = player():GetWorldForward()
                        teleport(f.x * 25, f.y * 25, 0)
                    end)
                end
                if ImGui.Button("Save this spot") then
                    try("Spot saved", function() menu.saved = player():GetWorldPosition() end)
                end
                ImGui.SameLine()
                if menu.saved and ImGui.Button("Back to saved spot") then
                    try("Back at your spot", function()
                        local p, s = player(), menu.saved
                        Game.GetTeleportationFacility():Teleport(p, Vector4.new(s.x, s.y, s.z, 1), p:GetWorldOrientation():ToEulerAngles())
                    end)
                end
                ImGui.EndTabItem()
            end
            ImGui.EndTabBar()
        end
    end
    ImGui.End()
end)

return menu
