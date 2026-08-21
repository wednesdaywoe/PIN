-- InvProbe -- reads what the engine actually hands Lua for the inventory, both halves of it.
--
-- Player.GetInventory() returns two tables, not one: items and resources. The Inventory panel keeps
-- them as g_Inventory and g_Resources (Inventory.lua:644), and only merges them in SORTED bag mode,
-- where each resource entry is unwrapped into a .raw and a .refined side (Inventory.lua:3062). Refined
-- materials draw today and raw ones don't, so the question is which step drops them: the engine never
-- filling the raw side, or the panel filtering it out after.
--
-- Read-only. Nothing here writes, sends, or touches the cart.
--
--   /invprobe          summary plus every resource entry, fields and all
--   /invprobe items    the items half too, one line per stack
--   /invprobe id N...  ask the client what it knows about ids the server says you hold
--   /invprobe cat N... walk the resource-category tree above a subtype
--   /invprobe enum     walk the category tree DOWN from Crafting Components and list what's held

require "table"
require "math"
require "string"
require "lib/lib_Slash"
require "lib/lib_Items"
require "lib/lib_SubTypeIds"

local function Sorted(t)
	local keys = {}
	for key in pairs(t) do
		table.insert(keys, tostring(key))
	end
	table.sort(keys)
	return keys
end

-- One level deep. Nested tables print their own fields inline so flags and stat blocks stay readable
-- without a recursive dumper that can hit a cycle.
local function Describe(value)
	local kind = type(value)
	if kind ~= "table" then
		return tostring(value)
	end

	local parts = {}
	for key, inner in pairs(value) do
		local shown
		if type(inner) == "table" then
			local sub = {}
			for k, v in pairs(inner) do
				table.insert(sub, tostring(k).."="..(type(v) == "table" and "{...}" or tostring(v)))
			end
			shown = "{"..table.concat(sub, ", ").."}"
		else
			shown = tostring(inner)
		end
		table.insert(parts, tostring(key).."="..shown)
	end

	if #parts == 0 then
		return "empty table"
	end
	table.sort(parts)
	return "{ "..table.concat(parts, ", ").." }"
end

local function Count(t)
	if type(t) ~= "table" then
		return -1
	end
	local n = 0
	for _ in pairs(t) do
		n = n + 1
	end
	return n
end

-- The client's own name lookup. A row with no itemInfo can't be drawn by lib_ItemCard at all, which is
-- a different failure from a row that's merely filtered, so it's worth separating here.
local function NameOf(sdb_id)
	if sdb_id == nil or type(Game.GetItemInfoByType) ~= "function" then
		return "?"
	end
	local ok, info = pcall(Game.GetItemInfoByType, sdb_id)
	if not ok or type(info) ~= "table" then
		return "NO ITEM DATA"
	end
	return tostring(info.name).." [subtype "..tostring(info.subTypeId).."]"
end

-- resource_type is the packed string lib_Items.GetResourceStats splits into the five stats. PIN sends
-- it empty today (see Docs/streams/client-ui.md UI6), so an empty one here is expected, not a bug.
local function DescribeStats(resource_type)
	if type(resource_type) ~= "string" or resource_type == "" then
		return "resource_type empty -- no stats to draw"
	end
	local ok, stats = pcall(LIB_ITEMS.GetResourceStats, resource_type)
	if not ok then
		return "resource_type '"..resource_type.."' did not parse: "..tostring(stats)
	end
	return "resource_type '"..resource_type.."' -> "..Describe(stats)
end

local function DumpSide(label, entry)
	if entry == nil then
		log("InvProbe:     "..label..": absent")
		return
	end

	log("InvProbe:     "..label..": "..NameOf(entry.item_sdb_id))
	log("InvProbe:       fields "..Describe(entry))
	log("InvProbe:       "..DescribeStats(entry.resource_type))
end

local function DumpResources(resources)
	if type(resources) ~= "table" then
		log("InvProbe: second return is "..type(resources)..", not a table. No client-side surface can")
		log("InvProbe: show the raw tier from here; the route has to be server-side.")
		return
	end

	log("InvProbe: resources: "..Count(resources).." entries")
	for key, RESOURCE in pairs(resources) do
		if type(RESOURCE) ~= "table" then
			log("InvProbe:   ["..tostring(key).."] is a "..type(RESOURCE)..": "..tostring(RESOURCE))
		else
			-- The entry above raw/refined is the grouping the engine chose, and it's the only place a
			-- clue to the grouping key can be. Print its own fields, not just its key names.
			local head = {}
			for k, v in pairs(RESOURCE) do
				if k ~= "raw" and k ~= "refined" then
					head[k] = v
				end
			end
			log("InvProbe:   ["..tostring(key).."] keys: "..table.concat(Sorted(RESOURCE), ", "))
			log("InvProbe:     entry itself "..Describe(head))
			DumpSide("raw", RESOURCE.raw)
			DumpSide("refined", RESOURCE.refined)
		end
	end
end

local function DumpItems(items)
	if type(items) ~= "table" then
		log("InvProbe: first return is "..type(items)..", not a table")
		return
	end

	log("InvProbe: items: "..Count(items).." entries")
	for _, ITEM in pairs(items) do
		log("InvProbe:   "..tostring(ITEM.item_sdb_id).." x"..tostring(ITEM.quantity).." "..NameOf(ITEM.item_sdb_id))
	end
end

-- A resource the server sent that never reached Lua is either dropped in transit or discarded by the
-- client for having no item data behind it. This tells the two apart: no itemInfo means the client
-- could not have drawn it whatever we do, and the fix is data, not interface.
local function LookUpIds(ids)
	for _, token in ipairs(ids) do
		local id = tonumber(token)
		if id == nil then
			log("InvProbe: '"..tostring(token).."' is not a number")
		else
			log("InvProbe: id "..tostring(id).." -> "..NameOf(id).."  count "..tostring(Player.GetItemCount(id)))
			local ok, info = pcall(Game.GetItemInfoByType, id)
			if ok and type(info) == "table" then
				log("InvProbe:   fields "..Describe(info))
			end
		end
	end
end

-- Resources are filed under a category tree, each node naming its parent (lib_Items.lua:1029). Iron
-- Ore reaches Lua and Melded Chitin Fragment doesn't, and their categories are the one thing that
-- differs, so walk both trees to the top and see where they stop matching.
local function WalkCategories(ids)
	for _, token in ipairs(ids) do
		local id = tonumber(token)
		if id == nil then
			log("InvProbe: '"..tostring(token).."' is not a number")
		else
			log("InvProbe: category "..tostring(id))
			local guard = 0
			while id and guard < 12 do
				guard = guard + 1
				local ok, info = pcall(Game.GetResourceTypeInfo, id)
				if not ok or type(info) ~= "table" then
					log("InvProbe:   "..tostring(id)..": NO RESOURCE TYPE INFO")
					break
				end
				log("InvProbe:   "..tostring(id).." "..Describe(info))
				id = info.parentResourceTypeId
			end
		end
	end
end

-- GetInventory() returns two of the six resources the client demonstrably holds, so a panel built on
-- it would be wrong by construction. This tests the alternative: descend the category tree from 15
-- ("Crafting Components") and ask GetInventoryItemsOfType at every node. If that reaches the melded
-- resources, UI4 has a route that doesn't depend on the accessor that drops them.
local c_CraftingComponents = 15

local function EnumerateByCategory(id, depth, seen, found)
	if seen[id] or depth > 6 then
		return
	end
	seen[id] = true

	local ok, info = pcall(Game.GetResourceTypeInfo, id)
	if not ok or type(info) ~= "table" then
		return
	end

	local pad = string.rep("  ", depth)
	local ok2, items = pcall(Player.GetInventoryItemsOfType, id)
	local held = (ok2 and type(items) == "table") and Count(items) or -1

	log("InvProbe: "..pad..tostring(id).." "..tostring(info.name).."  held "..tostring(held))
	if held > 0 then
		for _, ITEM in pairs(items) do
			local sdb = ITEM.item_sdb_id or ITEM.itemTypeId
			log("InvProbe: "..pad.."  -> "..tostring(sdb).." x"..tostring(ITEM.quantity or ITEM.total).." "..NameOf(sdb))
			found[tostring(sdb)] = true
		end
	end

	for _, child in pairs(info.childResourceTypeIds or {}) do
		EnumerateByCategory(child, depth + 1, seen, found)
	end
end

function RunProbe(args)
	if not Player.IsReady() then
		log("InvProbe: player isn't ready yet; try again once you're in the world")
		return
	end

	if args and args[1] == "enum" then
		log("InvProbe: ---- enumerate under Crafting Components")
		local found = {}
		EnumerateByCategory(c_CraftingComponents, 0, {}, found)
		local names = Sorted(found)
		log("InvProbe: reached "..tostring(#names).." distinct id(s): "..table.concat(names, ", "))
		log("InvProbe: ---- end")
		return
	end

	if args and args[1] == "cat" then
		local ids = {}
		for i = 2, #args do
			table.insert(ids, args[i])
		end
		if #ids == 0 then
			log("InvProbe: usage /invprobe cat 3288 3291")
			return
		end
		log("InvProbe: ---- category walk")
		WalkCategories(ids)
		log("InvProbe: ---- end")
		return
	end

	if args and args[1] == "id" then
		local ids = {}
		for i = 2, #args do
			table.insert(ids, args[i])
		end
		if #ids == 0 then
			log("InvProbe: usage /invprobe id 10 30404 77343")
			return
		end
		log("InvProbe: ---- id lookup")
		LookUpIds(ids)
		log("InvProbe: ---- end")
		return
	end

	local want_items = args and args[1] == "items"

	log("InvProbe: ---- begin")

	local ok, items, resources = pcall(Player.GetInventory)
	if not ok then
		log("InvProbe: GetInventory raised: "..tostring(items))
		return
	end

	log("InvProbe: returns are "..type(items).." and "..type(resources))
	DumpResources(resources)

	if want_items then
		DumpItems(items)
	else
		log("InvProbe: items: "..Count(items).." entries (run /invprobe items to list them)")
	end

	-- The six the server says are held (dbg_inventory, 2026-08-21). GetItemCount reads the client's own
	-- inventory state, so a nonzero count on an id the resources table omits proves the drop is in
	-- GetInventory and not in transit.
	log("InvProbe: counts for the ids the server reports holding:")
	for _, id in ipairs({10, 30404, 77343, 77344, 86668, 86703}) do
		log("InvProbe:   "..tostring(id).." count "..tostring(Player.GetItemCount(id)))
	end

	local current, limit = Player.GetInventoryWeight()
	log("InvProbe: weight "..tostring(current).."/"..tostring(limit))
	log("InvProbe: ---- end. Compare the counts against the server's dbg_inventory.")
end

function OnComponentLoad()
	LIB_SLASH.BindCallback({
		slash_list = "invprobe",
		description = "Dump both halves of Player.GetInventory() to the log",
		func = RunProbe,
	})
	log("InvProbe: loaded -- /invprobe, /invprobe items")
end
