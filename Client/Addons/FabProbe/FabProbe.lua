-- FabProbe — asks the client what its recipe API still answers, and writes the answer to the log.
--
-- CRAFT1 turns on one question nothing in the repo can settle: when the client is asked for a recipe
-- list, does it answer out of its own clientdb, or does it wait for the server? Those lead to opposite
-- plans. Locally answered means a panel can be built and demonstrated before PIN sends a single
-- fabrication message. Server answered means PIN has to reply to FabricationFetchAllRecipes (command
-- 241, response 177) before any panel can show anything at all, and the panel is second.
--
-- The reason it has to be probed rather than read: v1.6 deleted the crafting panel's Lua, and with it
-- every call site. Eight recipe functions survive in the client binary — GetRecipeIds, GetRecipeInfo,
-- GetRecipe, GetRecipeList, AddRecipeToCart, RemoveRecipeFromCart, CanTrackRecipe, IsTrackingRecipe —
-- but only the first two are called by anything still shipped (Mainframe.lua, feeding the web panel).
-- An export with no caller tells you nothing about what it returns.
--
-- Deliberately read-only. AddRecipeToCart and RemoveRecipeFromCart are named below but never called:
-- a cart is state, and this is a component that runs at login on a live character.
--
-- Output goes to the client log, not the screen. Read it with:
--   ls -t ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/ \
--     drive_c/users/steamuser/AppData/Local/Red\ 5\ Studios/Firefall/*_last_run.log | head -1
-- then grep FabProbe.

require "table"
require "lib/lib_Slash"

-- dbitems::Blueprint_Types, all eight rows. GetRecipeIds takes one of these by name — Mainframe.lua
-- passes "Research" — and nothing says whether the others are live or were switched off before 1962.
local CATEGORIES = {
	"None",
	"Upgrade",
	"Research",
	"Nanotemplate",
	"Master Crafting",
	"Resource Blending",
	"Refining",
	"Item Upgrade",
}

local function Describe(value)
	local kind = type(value)
	if kind == "table" then
		local n = 0
		for _ in pairs(value) do
			n = n + 1
		end
		return "table with "..n.." entries"
	end
	return kind.." "..tostring(value)
end

-- The engine functions may not exist under these names at all, and a missing global is a hard error in
-- Lua, so every call is guarded. "not exported" is itself a result worth logging.
local function Try(name, fn, ...)
	if type(fn) ~= "function" then
		log("FabProbe: "..name.." is not exported by this client")
		return nil
	end

	local ok, result = pcall(fn, ...)
	if not ok then
		log("FabProbe: "..name.." raised: "..tostring(result))
		return nil
	end

	log("FabProbe: "..name.." returned "..Describe(result))
	return result
end

local function ProbeCategory(category)
	local ids = Try("GetRecipeIds(\""..category.."\")", Game.GetRecipeIds, category)
	if type(ids) ~= "table" then
		return
	end

	local count = 0
	local first = nil
	for _, id in pairs(ids) do
		count = count + 1
		if first == nil then
			first = id
		end
	end

	log("FabProbe:   "..category..": "..count.." recipe ids")
	if first == nil then
		return
	end

	-- One id is enough to say whether the payload behind an id is populated or an empty shell.
	log("FabProbe:   first id "..tostring(first))
	local info = Try("GetRecipeInfo("..tostring(first)..")", Game.GetRecipeInfo, first)
	if type(info) == "table" then
		for key, value in pairs(info) do
			log("FabProbe:     "..tostring(key).." = "..Describe(value))
		end
	end
end

function ProbeAll()
	log("FabProbe: ---- begin")

	-- Which of the eight survive as callable Lua, before asking any of them for anything
	for _, name in pairs({ "GetRecipeIds", "GetRecipeInfo", "GetRecipe", "GetRecipeList",
	                       "AddRecipeToCart", "RemoveRecipeFromCart", "CanTrackRecipe", "IsTrackingRecipe" }) do
		log("FabProbe: Game."..name.." is "..type(Game[name]))
	end

	-- GetRecipeList takes no category in any surviving caller, so it is asked bare and may well fail;
	-- that failure is a result too.
	Try("GetRecipeList()", Game.GetRecipeList)

	for _, category in pairs(CATEGORIES) do
		ProbeCategory(category)
	end

	log("FabProbe: ---- end")
end

function OnComponentLoad()
	LIB_SLASH.BindCallback({
		slash_list = "fabprobe",
		description = "Log what the client's recipe API returns",
		func = ProbeAll,
	})
	log("FabProbe: loaded, /fabprobe to re-run")
end

-- Login is the interesting moment: if recipes arrive from the server rather than the client's own db,
-- this is the earliest point they could be there.
function OnPlayerReady()
	ProbeAll()
end
