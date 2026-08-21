-- MatList -- every material the character holds, in one list, named and counted.
--
-- The spike behind UI3 (Docs/streams/client-ui.md). It exists to settle one question: can a PIN-authored
-- addon draw a materials list that is actually complete? The shipped Inventory panel cannot, and not
-- because it filters anything -- UI1 found there is no single accessor that returns everything:
--
--   Player.GetInventory()              -> Iron Ore, Copper Wiring
--   Player.GetInventoryItemsOfType(15) -> Melded Chitin Fragment, Melded Blood Sample
--
-- Disjoint sets, four materials between them, and Inventory.lua reads only the first. So the merge
-- below is the whole point of the file; everything else is a window to put it in.
--
-- 15 is SubTypeIds.Resource, "Crafting Components". Only that node answers -- all ~50 categories under
-- it return nothing (/invprobe enum), so this is one call, not a tree walk.
--
--   /mats    toggle the list

require "table"
require "lib/lib_Slash"
require "lib/lib_MultiArt"

local FRAME = Component.GetFrame("Main")
local TITLE = Component.GetWidget("title")
local SUBTITLE = Component.GetWidget("subtitle")
local LIST = Component.GetWidget("list")

local function Require(what, WIDGET)
	if not WIDGET then
		log("MatList: MISSING "..what.." -- check the id in MatList.xml")
	end
	return WIDGET
end

local c_CraftingComponents = 15
local c_RowHeight = 34
local c_MaxRows = 13
local c_FallbackIcon = 231706

local w_ROWS = {}
local g_IsOpen = false

local BP_ROW = [[<Group dimensions="left:0; right:100%; height:32">
		<Border class="ButtonSolid" dimensions="dock:fill" style="tint:#1b1e1f; exposure:0; alpha:0.6"/>
		<Group name="icon" dimensions="left:2; top:2; width:28; height:28"/>
		<Text name="name" dimensions="left:38; right:100%-70; top:0; height:32" style="font:UbuntuRegular_11; valign:center; padding:0"/>
		<Text name="qty" dimensions="right:100%-6; width:60; top:0; height:32" style="font:UbuntuBold_11; halign:right; valign:center; padding:0; color:#c8d2d6"/>
	</Group>]]

-- Both accessors, keyed by item id so a material reported by both is counted once. Nothing is assumed
-- about which one holds what: that split is the engine's business and it has already surprised us.
local function Gather()
	local byId = {}

	local function Take(entry, source)
		if type(entry) ~= "table" then
			return
		end
		local id = entry.item_sdb_id or entry.itemTypeId
		if not id then
			return
		end

		local key = tostring(id)
		if not byId[key] then
			byId[key] = {
				item_sdb_id = id,
				name = entry.name,
				icon_id = entry.icon_id or entry.web_icon_id,
				quantity = entry.total or entry.quantity or Player.GetItemCount(id),
				source = source,
			}
		elseif byId[key].source ~= source then
			byId[key].source = "both"
		end
	end

	local ok, _, resources = pcall(Player.GetInventory)
	if ok and type(resources) == "table" then
		for _, RESOURCE in pairs(resources) do
			if type(RESOURCE) == "table" then
				Take(RESOURCE.raw, "inventory")
				Take(RESOURCE.refined, "inventory")
			end
		end
	end

	local ok2, items = pcall(Player.GetInventoryItemsOfType, c_CraftingComponents)
	if ok2 and type(items) == "table" then
		for _, ITEM in pairs(items) do
			Take(ITEM, "by-type")
		end
	end

	-- A row with no name is a row the player cannot read, so fall back to the client's item database
	-- before giving up on it. UI5 is where an unnamed row stops being acceptable.
	local list = {}
	for _, MAT in pairs(byId) do
		if not MAT.name or MAT.name == "" then
			local ok3, info = pcall(Game.GetItemInfoByType, MAT.item_sdb_id)
			if ok3 and type(info) == "table" then
				MAT.name = info.name
				MAT.icon_id = MAT.icon_id or info.web_icon_id
			end
		end
		MAT.name = MAT.name or ("Item "..tostring(MAT.item_sdb_id))
		table.insert(list, MAT)
	end

	table.sort(list, function(a, b)
		return tostring(a.name) < tostring(b.name)
	end)
	return list
end

local function ReleaseRows()
	for _, ROW in pairs(w_ROWS) do
		ROW.ICON:Destroy()
		Component.RemoveWidget(ROW.GROUP)
	end
	w_ROWS = {}
end

local function Draw()
	ReleaseRows()

	local list = Gather()
	local shown = 0

	for i, MAT in ipairs(list) do
		if i > c_MaxRows then
			break
		end

		local GROUP = Component.CreateWidget(BP_ROW, LIST)
		GROUP:SetDims("left:0; right:100%; top:"..((i - 1) * c_RowHeight).."; height:32")

		local ICON = MultiArt.Create(GROUP:GetChild("icon"))
		ICON:SetIcon(MAT.icon_id and MAT.icon_id ~= 0 and MAT.icon_id or c_FallbackIcon)

		GROUP:GetChild("name"):SetText(tostring(MAT.name))
		GROUP:GetChild("qty"):SetText(tostring(MAT.quantity))

		table.insert(w_ROWS, {GROUP = GROUP, ICON = ICON})
		shown = i
	end

	log("MatList: drew "..tostring(shown).." of "..tostring(#list).." material(s)")

	TITLE:SetText("MATERIALS")
	if #list == 0 then
		SUBTITLE:SetText("nothing held")
	elseif #list > shown then
		SUBTITLE:SetText(tostring(shown).." of "..tostring(#list).." shown")
	else
		SUBTITLE:SetText(tostring(#list).." held")
	end
end

local function Toggle(open)
	log("MatList: /mats -> "..(open and "open" or "close"))
	if not FRAME then
		log("MatList: no frame; the panel cannot be shown")
		return
	end
	g_IsOpen = open
	FRAME:Show(g_IsOpen)
	if g_IsOpen then
		Draw()
		Component.SetInputMode("cursor")
	else
		Component.SetInputMode(nil)
	end
end

function OnInventoryChanged()
	if g_IsOpen then
		Draw()
	end
end

function OnComponentLoad()
	LIB_SLASH.BindCallback({
		slash_list = "mats",
		description = "Show every material you are holding",
		func = function()
			Toggle(not g_IsOpen)
		end,
	})
	Require("frame Main", FRAME)
	Require("widget title", TITLE)
	Require("widget subtitle", SUBTITLE)
	Require("widget list", LIST)
	log("MatList: loaded -- /mats")
end
