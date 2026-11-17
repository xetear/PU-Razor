# Razor: An Ultima Online Assistant

![Razor Logo](https://imgur.com/jTtHLVF.png)

Razor is a free tool designed to help with simple tasks while playing Ultima Online.

[![Build](https://github.com/markdwags/Razor/actions/workflows/build.yml/badge.svg)](https://github.com/markdwags/Razor/actions/workflows/build.yml) [![Chat on Discord status](https://img.shields.io/discord/458277173208547350.svg?logo=discord)](https://discord.gg/VdyCpjQ)

## Community Edition (CE)

The goal of this project is to revive and continue development and maintenance of the abandoned Razor project, focusing on "quality of life" improvements while attempting to keep the spirit and vision the original developers had for Razor and not driving down the path of advanced automation and scripting that's found in other UO assistants.

It's recommended that you use the [ClassicUO](https://github.com/ClassicUO/ClassicUO) client with this version of Razor, however it should will work with the original 5.x and 7.x clients though support for any bugs may be limited.

Learn more about this version of Razor by exploring the links below.

- [Home](https://www.razorce.com)
- [Download](https://www.razorce.com/download/)
- [Release Notes](https://www.razorce.com/releasenotes/)
- [Project History](https://www.razorce.com/history/)
- [Help Overview](https://www.razorce.com/help/)
- [Scripting Guide](https://www.razorce.com/guide/)

## Installation & Help

- [Installing Razor on Windows](https://www.razorce.com/install/windows/)
- [Installing Razor on Linux](https://www.razorce.com/install/linux/)

If you need more help, [please go here](https://www.razorce.com/help) and review the documentation and/or join Discord and ask there.

[![Chat on Discord status](https://img.shields.io/discord/458277173208547350.svg?logo=discord)](https://discord.gg/VdyCpjQ)

## Razor Scripting Engine

Introduced in 1.6.4.2, Razor supports a command-based script language. For a comprehensive guide on the language and syntax, visit the [Razor Scripting Guide](https://www.razorce.com/guide/).

## Project Ultima changes

PU-Razor is Razor CE `master` at `d1c7b24` (1.10.0.0, November 2024) with these changes. Everything here is public:
nothing secret lives in Razor. The checks that must stay private (the client gate's salt and its answers) are in the
PU client and the server.

**About tab.** "Project Ultima Version" and a link to projectultimauo.com.

**Feature switches.** The server's 0xF0/0xFE "disallowed features" mask now works above bit 31 (`AllowBit` shifts
`1UL`; with `1U` bits 32 and up wrapped onto 0-31). PU adds five bits (`FeatureBit` in `Razor/Client/Client.cs`; the
same numbers as the server's `AssistantFeatures`; a change on one side needs the same change on the other):

| Bit | Name | When the server turns it off |
|---|---|---|
| 27 | Scripts | no script may start; a running one stops when the bit arrives |
| 28 | Macros | the same for macros |
| 29 | ScavengerAgent | the scavenger stops picking up |
| 30 | OrganizerAgent | the organizer refuses to run |
| 31 | DressLists | dress and undress lists refuse to run |

A bit that is off also locks its controls in the Razor window.

**Razor names itself.** Razor's reply to the server's feature negotiation (0xF0/0xFF) carries `u8 length` + ASCII
`PU-Razor/<version>` after the subcommand. Stock servers read only the subcommand, so other shards see no difference.

**Context rules (`Razor/Core/AutomationGuard.cs`).** The server can turn parts of automation off while the player is
in a context (PvP flagged, in the heat of battle, mining...):

- Server → Razor, 0xF0/0xFD ContextState: `u32 blocked` (the parts off: bit0 scripts, 1 macros, 2 movement,
  3 targeting, 4 casting, 5 skills, 6 useItems, 7 itemMoves, 8 attacks, 9 agents), `u8 length` + ASCII context name.
  `blocked` 0 ends the context.
- Razor → server, 0xF0/0xFC AutomationState: `u8 state`, bit0 a script runs, bit1 a macro plays. Sent when it changes.

Razor refuses an action in a part that is off only when a script, a macro or an agent sends it, and tells the player
once ("walking skipped: off for scripts and macros in ..."). Hotkeys and the player's own input are never touched. If the context turns
scripts or macros off as a whole, the running one stops. The five switches' names in the "disallowed features" list
are texts 1429-1433 in `etc/Language/Razor_lang.enu`.

## Contributing & Code of Conduct

Please read [CONTRIBUTING](CONTRIBUTING.md) for more information on how to contribute to this project.

Please note we have a [Code of Conduct](CODE_OF_CONDUCT.md), please follow it in all your interactions with this project.

## License

This work is released under the GPLv3 license. This project does not distribute any copyrighted game assets. In order to run this application you'll need to legally obtain a copy of the Ultima Online Classic Client.

See the [LICENSE](LICENSE.md) file for details.
