#region license
// Razor: An Ultima Online Assistant
// Copyright (c) 2022 Razor Development Community on GitHub <https://github.com/markdwags/Razor>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using System;
using System.Collections.Generic;
using Assistant.Macros;
using Assistant.Scripts;

namespace Assistant
{
    /// <summary>
    /// PU shard: what the server lets Razor's automation do. Two inputs: the feature bits (0xF0/0xFE; bits 27 Scripts
    /// and 28 Macros stop all of them) and the player's current context (0xF0/0xFD ContextState: PvP flagged, mining
    /// ...), which turns off whole scripts/macros or only parts of them, such as walking. A part is refused only for
    /// what a script, a macro or an agent does; hotkeys and the player's own input are never touched.
    /// </summary>
    public static class AutomationGuard
    {
        [Flags]
        public enum Part : uint
        {
            None = 0,
            Scripts = 1 << 0,
            Macros = 1 << 1,
            Movement = 1 << 2,
            Targeting = 1 << 3,
            Casting = 1 << 4,
            Skills = 1 << 5,
            UseItems = 1 << 6,
            ItemMoves = 1 << 7,
            Attacks = 1 << 8,
            Agents = 1 << 9
        }

        private static int _automationDepth;
        private static int _hotkeyDepth;
        private static Part _warned;
        private static byte _lastReportedState = 0xFF;

        /// <summary>The parts the player's current context turns off (from the server).</summary>
        public static Part Blocked { get; private set; }

        /// <summary>The context's name(s) as the server sent them, for the player's message.</summary>
        public static string ContextName { get; private set; } = string.Empty;

        /// <summary>
        /// True while what Razor sends belongs to a script, macro or agent: inside a script/macro step, or while
        /// one runs, unless a hotkey (the player's own key) is being handled.
        /// </summary>
        public static bool Automating =>
            _automationDepth > 0 ||
            (_hotkeyDepth == 0 && (ScriptManager.Running || MacroManager.Playing));

        public static bool ScriptsAllowed =>
            Client.Instance.AllowBit(FeatureBit.Scripts) && (Blocked & Part.Scripts) == 0;

        public static bool MacrosAllowed =>
            Client.Instance.AllowBit(FeatureBit.Macros) && (Blocked & Part.Macros) == 0;

        /// <summary>An agent's switch (bit) and the context's Agents part.</summary>
        public static bool AgentAllowed(int bit) =>
            Client.Instance.AllowBit(bit) && (Blocked & Part.Agents) == 0;

        public static void EnterAutomation() => _automationDepth++;

        public static void ExitAutomation()
        {
            if (_automationDepth > 0)
            {
                _automationDepth--;
            }
        }

        public static void EnterHotkey() => _hotkeyDepth++;

        public static void ExitHotkey()
        {
            if (_hotkeyDepth > 0)
            {
                _hotkeyDepth--;
            }
        }

        public static void OnFeaturesChanged()
        {
            StopWhatIsOff();
        }

        /// <summary>The server's 0xF0/0xFD: the parts off from now on and the context's name.</summary>
        public static void SetContext(Part blocked, string name)
        {
            Part was = Blocked;
            Blocked = blocked;
            ContextName = name ?? string.Empty;

            if (blocked != was)
            {
                _warned = Part.None;

                if (blocked != Part.None)
                {
                    Message($"In {Describe()}: {Names(blocked)} off for scripts and macros.");
                }
                else if (was != Part.None)
                {
                    Message("Context over: scripts and macros may do everything again.");
                }
            }

            StopWhatIsOff();
        }

        /// <summary>Whether a script or macro may start now; tells the player why not.</summary>
        public static bool CanStartScript()
        {
            if (ScriptsAllowed)
            {
                return true;
            }

            Message(Client.Instance.AllowBit(FeatureBit.Scripts)
                ? $"Scripts are off in {Describe()}."
                : "This server has turned off Razor scripts.");
            return false;
        }

        public static bool CanStartMacro()
        {
            if (MacrosAllowed)
            {
                return true;
            }

            Message(Client.Instance.AllowBit(FeatureBit.Macros)
                ? $"Macros are off in {Describe()}."
                : "This server has turned off Razor macros.");
            return false;
        }

        /// <summary>Called for every packet Razor sends to the server. False = drop it.</summary>
        public static bool AllowSend(byte[] data, int length) =>
            !Automating || Allow(Classify(data, length));

        /// <summary>Called before Razor asks the client to walk (the walk script command, the walk macro step).</summary>
        public static bool AllowMove() => !Automating || Allow(Part.Movement);

        public static bool Allow(Part part)
        {
            if (part == Part.None || (Blocked & part) == 0)
            {
                return true;
            }

            if ((_warned & part) == 0)
            {
                _warned |= part;
                Message($"{Names(part)} skipped: off for scripts and macros in {Describe()}.");
            }

            return false;
        }

        /// <summary>Which part a client-to-server packet belongs to.</summary>
        public static Part Classify(byte[] data, int length)
        {
            if (data == null || length < 1)
            {
                return Part.None;
            }

            switch (data[0])
            {
                case 0x02: return Part.Movement;
                case 0x05: return Part.Attacks;
                case 0x06: return Part.UseItems;
                case 0x07:
                case 0x08:
                case 0x13: return Part.ItemMoves;
                case 0x6C:
                    // flags 3 = the cursor was cancelled: never refuse letting go of a target
                    return length >= 7 && data[6] == 3 ? Part.None : Part.Targeting;
                case 0x12:
                    if (length < 4)
                    {
                        return Part.None;
                    }

                    switch (data[3])
                    {
                        case 0x24: return Part.Skills;
                        case 0x27:
                        case 0x56: return Part.Casting;
                        default: return Part.None;
                    }
                case 0xBF:
                    if (length < 5)
                    {
                        return Part.None;
                    }

                    switch ((data[3] << 8) | data[4])
                    {
                        case 0x1C:
                        case 0x2D: return Part.Casting;
                        case 0x2C: return Part.UseItems;
                        case 0x2E: return Part.Skills;
                        default: return Part.None;
                    }
                default: return Part.None;
            }
        }

        public static string Names(Part parts)
        {
            if (parts == Part.None)
            {
                return "nothing";
            }

            List<string> names = new List<string>();

            foreach (Part p in Enum.GetValues(typeof(Part)))
            {
                if (p != Part.None && (parts & p) != 0)
                {
                    names.Add(Name(p));
                }
            }

            return string.Join(", ", names);
        }

        private static string Name(Part p)
        {
            switch (p)
            {
                case Part.Scripts: return "scripts";
                case Part.Macros: return "macros";
                case Part.Movement: return "walking";
                case Part.Targeting: return "targeting";
                case Part.Casting: return "casting";
                case Part.Skills: return "skills";
                case Part.UseItems: return "using items";
                case Part.ItemMoves: return "moving items";
                case Part.Attacks: return "attacking";
                case Part.Agents: return "agents";
                default: return p.ToString();
            }
        }

        private static string Describe() =>
            string.IsNullOrEmpty(ContextName) ? "this context" : ContextName;

        /// <summary>Stops a running script or macro whose switch or context is off.</summary>
        public static void StopWhatIsOff()
        {
            if (Client.Instance == null)
            {
                return;
            }

            if (!ScriptsAllowed && (ScriptManager.Running || ScriptManager.Paused))
            {
                ScriptManager.StopScript();
                Message(Client.Instance.AllowBit(FeatureBit.Scripts)
                    ? $"Script stopped: scripts are off in {Describe()}."
                    : "Script stopped: this server has turned off Razor scripts.");
            }

            if (!MacrosAllowed && (MacroManager.Playing || MacroManager.StepThrough))
            {
                MacroManager.Stop();
                Message(Client.Instance.AllowBit(FeatureBit.Macros)
                    ? $"Macro stopped: macros are off in {Describe()}."
                    : "Macro stopped: this server has turned off Razor macros.");
            }
        }

        /// <summary>
        /// 0xF0/0xFC AutomationState to the server when it changes: bit0 a script runs, bit1 a macro plays. Called
        /// from the script and macro timers. Honest Razor only; the server logs it beside plugin-marked actions.
        /// </summary>
        public static void ReportState()
        {
            byte state = (byte) ((ScriptManager.Running ? 1 : 0) | (MacroManager.Playing ? 2 : 0));

            if (state == _lastReportedState || World.Player == null || !Client.Instance.ClientRunning)
            {
                return;
            }

            _lastReportedState = state;
            Client.Instance.SendToServer(new RazorAutomationState(state));
        }

        /// <summary>A new connection: the server knows nothing yet, and the old context no longer holds.</summary>
        public static void Reset()
        {
            Blocked = Part.None;
            ContextName = string.Empty;
            _warned = Part.None;
            _lastReportedState = 0xFF;
        }

        private static void Message(string text)
        {
            // Force: shown whatever the player's message level, so they always learn why a step was skipped
            World.Player?.SendMessage(MsgLevel.Force, text);
        }
    }
}
