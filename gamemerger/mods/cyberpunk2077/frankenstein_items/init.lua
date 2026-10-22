-- Frankenstein's item menu for Cyberpunk 2077 (a Cyber Engine Tweaks mod).
-- Every item in TweakDB, so items that mods add (through TweakXL or ArchiveXL) are listed too.
-- Open the CET overlay (~) to see it, or bind "Frankenstein item menu" in CET's Bindings.
-- Frankenstein writes settings.json next to this file before each launch.

local menu = { open = false, items = {}, shown = {}, cats = { "All" }, cat = "All", search = "", last = nil,
               overlay = false, allowed = true, note = "" }

local function readSettings()
    local f = io.open("settings.json", "r")
    if not f then return end
    local ok, s = pcall(json.decode, f:read("*a"))
    f:close()
    if ok and s and s.item_menu == false then menu.allowed = false end
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

local function give(it, qty)
    local ok = pcall(function() Game.AddToInventory(it.id, qty) end)
    menu.note = ok and ("Got " .. qty .. " x " .. it.name) or ("Couldn't give " .. it.name)
end

registerForEvent("onInit", function()
    readSettings()
    load()
end)
registerForEvent("onOverlayOpen", function() menu.overlay = true end)
registerForEvent("onOverlayClose", function() menu.overlay = false end)
registerHotkey("frankenstein_items", "Frankenstein item menu", function()
    if menu.allowed then menu.open = not menu.open end
end)

registerForEvent("onDraw", function()
    if not (menu.overlay or menu.open) then return end
    if not menu.allowed then
        if menu.overlay then
            ImGui.Begin("Frankenstein item menu")
            ImGui.Text("The item menu is off for normal play.")
            ImGui.Text("Play this merge in Creative, or turn it on in Frankenstein > Settings.")
            ImGui.End()
        end
        return
    end
    ImGui.SetNextWindowSize(640, 560, ImGuiCond.FirstUseEver)
    if ImGui.Begin("Frankenstein · every item in this merge (" .. #menu.items .. ")") then
        if #menu.items == 0 and ImGui.Button("Load items") then load() end
        menu.search = ImGui.InputText("Search", menu.search, 100)
        if ImGui.BeginCombo("Category", menu.cat) then
            for _, c in ipairs(menu.cats) do
                if ImGui.Selectable(c, c == menu.cat) then menu.cat = c end
            end
            ImGui.EndCombo()
        end
        if menu.last ~= menu.search .. "|" .. menu.cat then filter() end
        ImGui.Text(#menu.shown .. " shown. " .. menu.note)
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
    end
    ImGui.End()
end)

return menu
