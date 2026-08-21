-- MatList -- every material the character holds, in one readable list.
--
-- UI4 of the client-ui stream (Docs/streams/client-ui.md). The stream's own exit condition: open it
-- and read what you have, raw and refined together.
--
-- The reason this is an addon and not a patch to the shipped Inventory panel is the gather below.
-- There is no single call that returns every material -- UI1 measured it -- and the shipped panel is
-- built on the one that returns least:
--
--   Player.GetInventory()              -> Iron Ore, Copper Wiring
--   Player.GetInventoryItemsOfType(15) -> Melded Chitin Fragment, Melded Blood Sample
--
-- Disjoint. So all three sources are read and merged by item id, and the items half is swept as well
-- in case something lands there that neither of the other two reports. Over-reading is cheap; the
-- whole point of the panel is that it does not quietly omit a stack.
--
-- 15 is SubTypeIds.Resource, "Crafting Components". Only that node answers GetInventoryItemsOfType --
-- its ~50 child categories all return nothing (/invprobe enum) -- so it is one call, not a tree walk.
--
--   /mats    toggle the list

require "table"
require "math"
require "lib/lib_Slash"
require "lib/lib_MultiArt"
require "lib/lib_RowScroller"
require "lib/lib_SubTypeIds"
require "lib/lib_Items"
require "lib/lib_Tooltip"
require "lib/lib_math"

local FRAME = Component.GetFrame("Main")
local TITLE = Component.GetWidget("title")
local SUBTITLE = Component.GetWidget("subtitle")
local LIST = Component.GetWidget("list")
local FOOTER = Component.GetWidget("footer")

local c_CraftingComponents = SubTypeIds.Resource
local c_FallbackIcon = 231706

local BP_ROW = [[<Group dimensions="left:0; right:100%; height:38">
		<Border class="ButtonSolid" dimensions="dock:fill" style="tint:#1b1e1f; exposure:0; alpha:0.55"/>
		<Group name="icon" dimensions="left:5; top:5; width:28; height:28"/>
		<Text name="name" dimensions="left:42; right:100%-78; top:0; height:38" style="font:UbuntuRegular_11; valign:center; padding:0"/>
		<Text name="qty" dimensions="right:100%-10; width:64; top:0; height:38" style="font:UbuntuBold_12; halign:right; valign:center; padding:0; color:#c8d2d6"/>
		<FocusBox name="focus" dimensions="dock:fill"/>
	</Group>]]

-- Padded at the top rather than the bottom so a heading sits closer to the rows it introduces than to
-- the group above it. Without that the list reads as evenly spaced bands with no grouping at all.
local BP_HEADER = [[<Group dimensions="left:0; right:100%; height:34">
		<Text name="label" dimensions="left:4; right:100%; top:12; height:20" style="font:Demi_10; valign:center; padding:0; color:#7fb0c0"/>
		<Border dimensions="left:4; right:100%; bottom:100%; height:1" style="tint:#2b3235; exposure:0"/>
	</Group>]]

local c_RarityColors = {
	salvage = "salvage", common = "common", uncommon = "uncommon",
	rare = "rare", epic = "epic", legendary = "legendary",
}

local SCROLLER = nil
local w_ROWS = {}
local w_TOOLTIP = nil
local g_IsOpen = false

-- ------------------------------------------
-- CATEGORIES
-- ------------------------------------------

-- Materials are filed in a tree under Crafting Components, and the immediate node is the one worth
-- grouping by: "Raw Metals" and "Raw Biomaterials" tell a player something, while their shared parent
-- "Raw Resource" would fold both into one heading and say less.
--
-- The walk upward only exists as a fallback. A subtype with no resource-type info of its own still
-- gets a heading from the first ancestor that has one, so a material family PIN has never seen lands
-- somewhere sensible instead of under "Other". Ids are compared through tonumber because the client's
-- own lib_Items does the same before comparing parentResourceTypeId.
-- 1962's item names carry trailing content markers -- "Cryogenic Recharger I^Q", the ^Q meaning
-- pre-1.6 -- which the shipped panels never strip because they never draw these items. It is editor
-- bookkeeping, not part of the name, so it comes off before a player sees it. Only a caret followed
-- by one or two letters at the very end is touched; a caret anywhere else stays.
local function CleanName(name)
	if type(name) ~= "string" then
		return name
	end
	local stripped = name:match("^(.-)%^%a%a?$")
	if stripped and stripped ~= "" then
		return stripped
	end
	return name
end

local function CategoryOf(subTypeId)
	local id, guard = tonumber(subTypeId), 0

	while id and guard < 12 do
		guard = guard + 1
		local ok, info = pcall(Game.GetResourceTypeInfo, id)
		if not ok or type(info) ~= "table" then
			break
		end

		if info.name and info.name ~= "" and id ~= c_CraftingComponents then
			return info.name
		end
		id = tonumber(info.parentResourceTypeId)
	end

	return "Other"
end

-- A heading has to say something the row does not. Subtype 2259 (Cryogenic Recharger I) resolves to a
-- node named after the item itself, which would put a one-row group under a heading repeating its own
-- name. Anything degenerate like that falls back to what the client does know: that the item is a
-- crafted component.
local function Heading(subTypeId, name, info)
	local category = CategoryOf(subTypeId)

	if not category or category == "Other" or category == name then
		if info and info.type == "crafting_component" then
			return "Crafted"
		end
		return "Other"
	end
	return category
end

-- ------------------------------------------
-- GATHER
-- ------------------------------------------

local function Gather()
	local byId = {}

	local function Take(entry)
		if type(entry) ~= "table" then
			return
		end
		local id = entry.item_sdb_id or entry.itemTypeId
		if not id or byId[tostring(id)] then
			return
		end

		local info
		local ok, result = pcall(Game.GetItemInfoByType, id)
		if ok and type(result) == "table" then
			info = result
		end

		local subTypeId = entry.subTypeId or (info and info.subTypeId)

		byId[tostring(id)] = {
			item_sdb_id = id,
			name = CleanName(entry.name or (info and info.name)) or ("Item "..tostring(id)),
			raw_name = entry.name or (info and info.name),
			icon_id = entry.icon_id or entry.web_icon_id or (info and info.web_icon_id),
			-- GetItemCount is the one number that agreed with the server on every id (UI1), so it wins
			-- over whatever the entry carries.
			quantity = Player.GetItemCount(id) or entry.total or entry.quantity or 0,
			category = Heading(subTypeId, entry.name or (info and info.name), info),
		}
	end

	local ok, items, resources = pcall(Player.GetInventory)
	if ok and type(resources) == "table" then
		for _, RESOURCE in pairs(resources) do
			if type(RESOURCE) == "table" then
				Take(RESOURCE.raw)
				Take(RESOURCE.refined)
			end
		end
	end

	local ok2, byType = pcall(Player.GetInventoryItemsOfType, c_CraftingComponents)
	if ok2 and type(byType) == "table" then
		for _, ITEM in pairs(byType) do
			Take(ITEM)
		end
	end

	-- Third sweep: anything in the items half that the client itself calls a crafting component. Neither
	-- call above is documented to be complete, and this one costs a loop over a list already in hand.
	--
	-- Two tests, because an item can pass one and fail the other. Cryogenic Recharger I (81626) is
	-- subtype 2259, which is nowhere under Crafting Components, so IsItemOfType misses it -- but its
	-- own itemInfo.type reads "crafting_component" and the server files it as one. A crafted output
	-- that the materials list cannot see is exactly the omission this panel exists to prevent.
	if ok and type(items) == "table" then
		for _, ITEM in pairs(items) do
			local id = ITEM.item_sdb_id

			local ok3, by_subtype = pcall(Game.IsItemOfType, id, c_CraftingComponents)
			local by_type = false
			local ok4, info = pcall(Game.GetItemInfoByType, id)
			if ok4 and type(info) == "table" then
				by_type = (info.type == "crafting_component")
			end

			if (ok3 and by_subtype) or by_type then
				Take(ITEM)
			end
		end
	end

	local list = {}
	for _, MAT in pairs(byId) do
		if MAT.quantity and MAT.quantity > 0 then
			table.insert(list, MAT)
		end
	end

	table.sort(list, function(a, b)
		if a.category ~= b.category then
			return tostring(a.category) < tostring(b.category)
		end
		return tostring(a.name) < tostring(b.name)
	end)
	return list
end

-- ------------------------------------------
-- TOOLTIP
-- ------------------------------------------

-- The client's own item tooltip, the same widget the Inventory panel puts under the cursor. It is
-- built per hover and destroyed on leave because that is what lib_ItemCard does; a tooltip kept alive
-- between rows shows the previous item for a frame.
--
-- Materials carry no stat block today (the packed resource_type string arrives empty, see UI6), so
-- what this draws is name, rarity-tinted frame, category path and description. That is the whole of
-- what 1962 has for a material, not a subset of it.
local function HideTooltip()
	if w_TOOLTIP then
		w_TOOLTIP:Destroy()
		w_TOOLTIP = nil
	end
	Tooltip.Show(false)
end

local function ShowTooltip(PARENT, MAT)
	HideTooltip()

	local ok, info = pcall(Game.GetItemInfoByType, MAT.item_sdb_id)
	if not ok or type(info) ~= "table" then
		return
	end
	info.quantity = MAT.quantity

	w_TOOLTIP = LIB_ITEMS.CreateToolTip(PARENT)
	w_TOOLTIP:DisplayInfo(info)

	local bounds = w_TOOLTIP:GetBounds()
	Tooltip.Show(w_TOOLTIP:GetWidget(), {
		width = bounds.width,
		height = bounds.height,
		frame_color = Component.LookupColor(c_RarityColors[info.rarity] or "common"),
		alpha = 0.3,
	})
end

-- ------------------------------------------
-- DRAW
-- ------------------------------------------

local function ReleaseRows()
	HideTooltip()

	for _, ROW in pairs(w_ROWS) do
		if ROW.ICON then
			ROW.ICON:Destroy()
		end
		Component.RemoveWidget(ROW.WIDGET)
	end
	w_ROWS = {}

	if SCROLLER then
		SCROLLER:Destroy()
		SCROLLER = nil
	end
end

local function AddHeader(label)
	local WIDGET = Component.CreateWidget(BP_HEADER, LIST)
	WIDGET:GetChild("label"):SetText(tostring(label):upper())

	SCROLLER:AddRow(WIDGET)
	table.insert(w_ROWS, {WIDGET = WIDGET})
end

local function AddMaterial(MAT)
	local WIDGET = Component.CreateWidget(BP_ROW, LIST)

	local ICON = MultiArt.Create(WIDGET:GetChild("icon"))
	local icon_id = MAT.icon_id
	if not icon_id or icon_id == 0 then
		icon_id = c_FallbackIcon
	end
	ICON:SetIcon(icon_id)

	WIDGET:GetChild("name"):SetText(tostring(MAT.name))
	WIDGET:GetChild("qty"):SetText(_math.MakeReadable(MAT.quantity, true))

	local FOCUS = WIDGET:GetChild("focus")
	FOCUS:BindEvent("OnMouseEnter", function()
		ShowTooltip(WIDGET, MAT)
	end)
	FOCUS:BindEvent("OnMouseLeave", function()
		HideTooltip()
	end)

	SCROLLER:AddRow(WIDGET)
	table.insert(w_ROWS, {WIDGET = WIDGET, ICON = ICON})
end

local function Draw()
	ReleaseRows()

	SCROLLER = RowScroller.Create(LIST)
	SCROLLER:SetSlider(RowScroller.SLIDER_DEFAULT)
	SCROLLER:ShowSlider("auto")
	SCROLLER:SetSpacing(4)
	SCROLLER:LockUpdates()

	local list = Gather()
	local category = nil
	local total = 0

	for _, MAT in ipairs(list) do
		if MAT.category ~= category then
			category = MAT.category
			AddHeader(category)
		end
		AddMaterial(MAT)
		total = total + MAT.quantity
	end

	SCROLLER:UnlockUpdates()
	SCROLLER:UpdateSize()

	TITLE:SetText("MATERIALS")
	if #list == 0 then
		SUBTITLE:SetText("nothing held")
		FOOTER:SetText("")
	else
		SUBTITLE:SetText(tostring(#list).." material"..(#list == 1 and "" or "s"))
		FOOTER:SetText(_math.MakeReadable(total, true).." units in total")
	end

	log("MatList: drew "..tostring(#list).." material(s)")
	for _, MAT in ipairs(list) do
		log("MatList:   "..tostring(MAT.item_sdb_id).." x"..tostring(MAT.quantity)
			.." '"..tostring(MAT.raw_name).."' icon "..tostring(MAT.icon_id).." ["..tostring(MAT.category).."]")
	end
end

-- ------------------------------------------
-- COMPONENT
-- ------------------------------------------

local function Toggle(open)
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
		ReleaseRows()
		Component.SetInputMode(nil)
	end
end

function OnInventoryChanged()
	if g_IsOpen then
		Draw()
	end
end

function OnComponentLoad()
	if not (FRAME and TITLE and SUBTITLE and LIST and FOOTER) then
		log("MatList: a widget is missing -- check the id= attributes in MatList.xml")
	end

	LIB_SLASH.BindCallback({
		slash_list = "mats",
		description = "Show every material you are holding",
		func = function()
			Toggle(not g_IsOpen)
		end,
	})
	log("MatList: loaded -- /mats")
end
