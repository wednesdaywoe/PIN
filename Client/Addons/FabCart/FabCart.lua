-- FabCart — finds out whether a filled cart ever reaches the server.
--
-- The one unknown left in crafting: how a chosen recipe becomes a "start building this" request.
-- Reading the client settles half of it and rules a lot out:
--
--   * The client binary can still send all eight Fabrication commands (240-247) and still decodes all
--     eight responses, because it still fires ON_FAB_START_RESPONSE and its five siblings into Lua.
--   * Nothing shipped binds those events. Not one file under system/gui mentions them.
--   * The Game table exports eight recipe functions and no sender. AddRecipeToCart and
--     RemoveRecipeFromCart are the only two that write anything; the other six read.
--   * Mainframe.lua's whole "CRAFTING FUNCTIONS" section is one function, and it reads.
--
-- ANSWERED 2026-08-20, first run. The cart does talk to the server, and it is not fabrication.
-- AddRecipeToCart sends UpdateShoppingList (command 194) on the mission-and-marker controller, and no
-- Fabrication command was sent at all. So the cart is the tracked-recipe list -- the panel that tells
-- you which ingredients you still need -- and it is a wishlist, not a build order. Fabrication_Start
-- has no caller anywhere in the script layer, so PIN cannot wait for the client to ask for a build.
--
-- The evidence, kept because it is easy to lose: IsTrackingRecipe(75097) went false to true on the add
-- and back to false on the remove; not one ON_FAB_* event fired; command 194 had never appeared in any
-- server log before this probe existed and then appeared once per run, matching the run times to the
-- second.
--
-- The probe watches both ends at once, which is what made that readable:
--   client end   -- every ON_FAB_* event is bound below, so a response of any kind is logged
--   server end   -- PIN already logs any command it has no handler for, with the raw bytes:
--                   "Unrecognized MsgID for GSS Packet; Controller = 2 ... MsgID = 243"
--                 Nothing extra is needed server-side. No fabrication command has ever appeared in
--                 the logs, so anything in the 240-247 range is new and is the answer.
--
-- Not read-only, unlike FabProbe. It adds one recipe to the cart and takes it back out, so run it on
-- a character you do not mind touching.

require "table"
require "lib/lib_Slash"

local WATCHED = {
	"GetRecipe", "GetRecipeIds", "GetRecipeInfo", "GetRecipeList",
	"AddRecipeToCart", "RemoveRecipeFromCart", "CanTrackRecipe", "IsTrackingRecipe",
}

local function Describe(value)
	local kind = type(value)
	if kind ~= "table" then
		return kind.." "..tostring(value)
	end

	local parts = {}
	for key, inner in pairs(value) do
		local shown = type(inner) == "table" and "{...}" or tostring(inner)
		table.insert(parts, tostring(key).."="..shown)
	end
	if #parts == 0 then
		return "empty table"
	end
	return "{ "..table.concat(parts, ", ").." }"
end

local function Try(name, fn, ...)
	if type(fn) ~= "function" then
		log("FabCart: "..name.." is not exported by this client")
		return nil
	end

	local ok, result = pcall(fn, ...)
	if not ok then
		log("FabCart: "..name.." raised: "..tostring(result))
		return nil
	end

	log("FabCart: "..name.." returned "..Describe(result))
	return result
end

-- Every fabrication response the client still knows how to decode. If any of these fires, the server
-- answered, which means something sent -- and the server log names which command it was.
local function Report(label)
	return function(args)
		log("FabCart: EVENT "..label.." "..Describe(args))
	end
end

OnFabStartResponse       = Report("ON_FAB_START_RESPONSE")
OnFabFetchRecipes        = Report("ON_FAB_FETCH_RECIPES")
OnFabFetchInstance       = Report("ON_FAB_FETCH_INSTANCE")
OnFabApplyActionResponse = Report("ON_FAB_APPLY_ACTION_RESPONSE")
OnFabFinalized           = Report("ON_FAB_FINALIZED")
OnFabCanceled            = Report("ON_FAB_CANCELED")
OnRecipeListUpdate       = Report("ON_RECIPE_LIST_UPDATE")

-- The cart is invisible: nothing reads it back. GetRecipe is the closest thing to a window on it, so
-- it is sampled either side of every write. A field that changes is the cart; no change means the
-- cart state is not reachable from Lua and only the server log can tell us anything.
local function Sample(when, id)
	log("FabCart: --- "..when)
	Try("GetRecipe("..tostring(id)..")", Game.GetRecipe, id)
	Try("IsTrackingRecipe("..tostring(id)..")", Game.IsTrackingRecipe, id)
	Try("CanTrackRecipe("..tostring(id)..")", Game.CanTrackRecipe, id)
end

local function FirstResearchRecipe()
	if type(Game.GetRecipeIds) ~= "function" then
		return nil
	end

	local ok, ids = pcall(Game.GetRecipeIds, "Research")
	if not ok or type(ids) ~= "table" then
		return nil
	end

	for _, id in pairs(ids) do
		return id
	end
	return nil
end

-- The cart does reach the server, and not as a Fabrication command: adding a recipe sends
-- UpdateShoppingList (command 194) on the mission-and-marker controller, which is the tracked-recipe
-- list -- the ingredients-still-needed panel, not a build order. The first run of this probe added and
-- removed inside one frame, and the send that followed carried two zero counts, so the message is
-- gathered at end of frame and carries the whole list rather than the one change. To see an id on the
-- wire the list has to still be occupied when the frame ends, so the add and the remove are separate
-- commands now.
--
--   /fabcart hold [id]   add only, leave it in the list
--   /fabcart drop [id]   remove only
--   /fabcart [id]        add and remove in one frame, the original round trip

local function ResolveId(token)
	if token == nil or token == "" then
		local id = FirstResearchRecipe()
		if id == nil then
			log("FabCart: no Research recipe ids to test with; pass one, e.g. /fabcart hold 75097")
		else
			log("FabCart: no id given, using the first Research recipe: "..tostring(id))
		end
		return id
	end

	local id = tonumber(token)
	if id == nil then
		log("FabCart: '"..tostring(token).."' is not a number; usage /fabcart [hold|drop] [recipe_id]")
	end
	return id
end

function RunCart(args)
	local mode, token = "cycle", nil
	local first = args and args[1]
	if first == "hold" or first == "drop" then
		mode, token = first, args[2]
	else
		token = first
	end

	local id = ResolveId(token)
	if id == nil then
		return
	end

	log("FabCart: ---- begin "..mode..", recipe "..tostring(id))
	for _, name in pairs(WATCHED) do
		log("FabCart: Game."..name.." is "..type(Game[name]))
	end

	Sample("before the cart is touched", id)

	if mode ~= "drop" then
		Try("AddRecipeToCart("..tostring(id)..")", Game.AddRecipeToCart, id)
		Sample("after AddRecipeToCart", id)
	end

	if mode ~= "hold" then
		Try("RemoveRecipeFromCart("..tostring(id)..")", Game.RemoveRecipeFromCart, id)
		Sample("after RemoveRecipeFromCart", id)
	end

	log("FabCart: ---- end. Now read the server log for MsgID 194.")
end

function OnComponentLoad()
	LIB_SLASH.BindCallback({
		slash_list = "fabcart",
		description = "Add or remove a recipe from the cart, logging both ends",
		func = RunCart,
	})
	log("FabCart: loaded -- /fabcart [id], /fabcart hold [id], /fabcart drop [id]")
end
