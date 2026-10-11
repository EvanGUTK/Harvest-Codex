using System;
using System.Collections.Generic;

namespace Harvest.Language
{
    /// <summary>Reference for the implemented language subset, in stable teaching order.</summary>
    public static class LanguageReference
    {
        public const int MaxEntries = 32;
        private static readonly IReadOnlyList<ReferenceEntry> entries = CreateEntries();

        public static IReadOnlyList<ReferenceEntry> Available(Capabilities capabilities)
        {
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            var available = new List<ReferenceEntry>();
            foreach (ReferenceEntry entry in entries)
            {
                bool allowed = true;
                foreach (string requirement in entry.RequiredCapabilities)
                    if (!capabilities.Allows(requirement)) { allowed = false; break; }
                if (allowed) available.Add(entry);
            }
            return available.AsReadOnly();
        }

        private static IReadOnlyList<ReferenceEntry> CreateEntries()
        {
            var result = new List<ReferenceEntry>
            {
                Entry("literals", "Values and source layout", "42\n\"wheat\"\nTrue\n# A comment\n",
                    "Values are checked signed 64-bit integers, strings of at most 4096 UTF-8 bytes, and True/False booleans. Negative numbers use arithmetic. Quotes support \\n, \\r, \\t, escaped quotes and backslash. Commands occupy separate lines; # starts a comment outside strings. Nested blocks use four spaces per level, never tabs. Parentheses group expressions. This is a Python-like subset: floats, lists, functions, imports, classes and arbitrary host members are unsupported. Source is limited to 64 KiB UTF-8 and nesting to 64 levels."),
                Entry("assignment", "Store a value", "rows = 3\nrows\n",
                    "name = expression stores a value in this program's private variables. Read a variable only after assigning it. At most 256 variables are allowed. Record fields are read-only; assignment cannot change the farm.", "assignment"),
                Entry("arithmetic", "Integer and string arithmetic", "log(2 + 3 * 4)\nlog(-7 // 3)\nlog(-7 % 3)\nlog(\"wheat\" + \" crop\")\n",
                    "Integers support unary +/-, +, -, *, // and %. Multiplication/division/remainder bind before addition/subtraction. // rounds toward negative infinity; % has the divisor's sign, or is zero. Overflow and division by zero stop this program with a source diagnostic. + also joins two strings within the 4096-byte limit. Booleans are not integers.", "arithmetic", "log"),
                Entry("comparisons", "Compare scalar values", "log(3 < 4)\nlog(\"wheat\" == \"wheat\")\n",
                    "<, <=, > and >= compare integers. == and != compare matching scalar types: integers, booleans or strings. Results are booleans. Records cannot be compared and different scalar types cause a type error. Chained comparisons are unsupported; combine comparisons with and. Comparisons require arithmetic capability.", "arithmetic", "log"),
                Entry("conditions", "Choose with if and else", "if True:\n    log(\"ready\")\nelse:\n    log(\"waiting\")\n",
                    "if requires a boolean condition. The indented body runs only when True; optional else runs when False. Integers and strings do not count as booleans. Use four-space blocks. There is no elif syntax.", "conditions", "log"),
                Entry("boolean-operators", "Combine booleans", "log(not False)\nlog(True and False)\nlog(True or False)\n",
                    "not, and and or require booleans. and/or short-circuit: they skip their right operand when the left determines the result. Comparisons bind before not, then and, then or. Farm actions in a skipped operand are not submitted.", "conditions", "log"),
                Entry("for", "Count with a lazy range", "for row in range(3):\n    log(row)\n",
                    "for name in range(n) visits 0 through n-1; range(0) has no iterations. n must be an integer from 0 to 1,000,000. range is lazy and only supported in this for syntax, not as a general callable or list. The loop variable is available in the body and remains at its last assigned value afterward. Empty ranges do not assign it. Changing the loop variable does not change the iteration count.", "for", "range", "log"),
                Entry("while", "Repeat while a boolean is true", "while True:\n    wait(1)\n",
                    "while repeats while its condition is a strict boolean. Valid infinite loops run indefinitely, yielding after the instruction budget each tick so controls remain responsive. There is no lifetime instruction cap and no break/continue syntax. Pause freezes time; Step while paused advances exactly one simulation tick, including bounded program work, active actions and crop growth. Stop cancels execution at the session boundary.", "while", "wait"),
                Entry("move-forward", "move_forward()", "move_forward()\n",
                    "No arguments. Propose one tile forward along the tractor's heading; boundaries do not wrap. Movement can fail for blocked, occupied or out-of-bounds targets or insufficient resources. This action yields until a terminal result on a later runtime pass; it returns a read-only record with .ok and .reason. Success reason is none. Duration, fuel and reservations are determined by the simulation rules.", "move_forward"),
                Entry("turn-left", "turn_left()", "turn_left()\n",
                    "No arguments. Turn heading left by one quarter turn when the simulation completes the action. It yields until a terminal result with .ok and .reason; success reason is none. It does not move the tractor. Duration and fuel come from simulation rules.", "turn_left"),
                Entry("turn-right", "turn_right()", "turn_right()\n",
                    "No arguments. Turn heading right by one quarter turn when the simulation completes the action. It yields until a terminal result with .ok and .reason; success reason is none. It does not move the tractor. Duration and fuel come from simulation rules.", "turn_right"),
                Entry("harvest", "harvest()", "harvest()\n",
                    "No arguments. Harvest the ripe crop on the tractor's current tile into cargo, subject to implement, fuel and capacity checks. The simulation reserves resources at acceptance and applies effects at completion. It yields for a terminal .ok/.reason result, including rejection; a failed action is a value, not a script type error. Success reason is none.", "harvest"),
                Entry("unload", "unload()", "unload()\n",
                    "No arguments. At a depot, transfer all cargo to depot stock when the action completes. The simulation validates depot location, fuel and capacity; there is no partial transfer. It yields for a terminal .ok/.reason result, with none on success. A script cannot silently discard cargo.", "unload"),
                Entry("refuel", "refuel()", "refuel()\n",
                    "No arguments. At a depot, fill the tractor to capacity using depot fuel stock when the action completes. Depot stock must cover the full refill after action cost; insufficient stock rejects atomically. It yields for a terminal .ok/.reason result, with none on success. Costs and reservations belong to the simulation.", "refuel"),
                Entry("action-results", "Inspect an action result", "result = harvest()\nif not result.ok:\n    log(result.reason)\n",
                    "Every action returns an immutable record with .ok (boolean) and .reason (lower-case code; none on success). Rejections also yield until the next runtime pass and return ok=False. Type, syntax and locked-capability errors instead stop the offending program with a source diagnostic. Actions are submitted once while pending; sensors never see effects later in the current tick.", "assignment", "harvest", "conditions", "log"),
                Entry("sense", "sense()", "log(sense().ripe)\n",
                    "No arguments. Return an immutable tick-start record with x/y (nonnegative integers; x east, y north), heading/crop/implement (strings), ripe (boolean), fuel/cargo (nonnegative integers). Fields use .field syntax and cannot be assigned. Fuel is available fuel, excluding reservations. Queries do not yield or mutate the farm; later actions in the same tick do not change this snapshot.", "sense", "log"),
                Entry("position", "position()", "position().x\nposition().y\n",
                    "No arguments. Return an immutable tick-start record with .x and .y, nonnegative integer grid coordinates. x increases east and y increases north; coordinates are zero-based. Reading fields does not move the tractor or yield. Other sensor fields are only available from sense().", "position"),
                Entry("fuel", "fuel()", "fuel()\n",
                    "No arguments. Return available tractor fuel as an integer from the tick-start snapshot, excluding reserved action cost. This query does not yield or refuel. Refueling requires the separate refuel() action at a depot.", "fuel"),
                Entry("cargo", "cargo()", "cargo()\n",
                    "No arguments. Return the carried cargo quantity as an integer from the tick-start snapshot. This query does not yield or unload. Cargo changes only through authoritative completed actions.", "cargo"),
                Entry("wait", "wait(ticks)", "wait(3)\n",
                    "One integer argument from 1 to 1,000,000. Suspend until the current simulation tick plus ticks, then return True. Wake-tick overflow is a source diagnostic. This is an interpreter wait, not a farm command. Pause freezes simulation ticks; Step advances one tick. Animation/frame speed does not determine the wake time.", "wait"),
                Entry("log", "log(value)", "log(\"ready\")\n",
                    "One value argument. Write an integer, boolean or string to the bounded event console; records display <immutable record>. Return True without yielding. At most 16 messages per tick and 256 UTF-8 bytes per message are kept; truncation is marked visibly. Logging neither mutates the farm nor bypasses the instruction budget. Sandbox unlocks all implemented capabilities while retaining the same execution, source and memory limits.", "log")
            };
            if (result.Count > MaxEntries) throw new InvalidOperationException("Reference entry bound exceeded.");
            return result.AsReadOnly();
        }

        private static ReferenceEntry Entry(string id, string title, string example, string description, params string[] requirements)
            => new ReferenceEntry(id, title, example, description, requirements);
    }
}
