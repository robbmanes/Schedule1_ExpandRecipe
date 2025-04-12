# ExpandRecipe - Schedule 1
A Product Manager recipe expansion mod for the game [Schedule 1](https://store.steampowered.com/app/3164500/Schedule_I/).
**This mod only functions with the Il2cpp version of the game, not the Mono verison**.

## About
When using the Product Manager App on your phone, instead of having to click on each product in succession to determine what mixers you need, this mod expands all of them at once including the base product you need (type of weed, meth, cocaine, etc).

Currently due to the recursive nature of needing to look up these recipes only a single solution is presented in the Product Manager app.  Ideally I'd show all possible solutions but attempts to do so were not very performant and caused long delays/hangs in the game, so I opted to show just a single solution (for now).

## Installation
- Install MelonLoader (https://melonwiki.xyz/#)
- Download the `ExpandRecipe.dll` file.
- Place the `ExpandRecipe.dll` file in the Mods folder in your Schedule 1 installation folder.
- The mod will automatically start with MelonLoader, and the next item you click on in the Product Manager app should be expanded.

## Source/Contributing
This mod is published under the open source MIT license and all source code is available here:
- ExpandRecipe GitHub (https://github.com/robbmanes/Schedule1_ExpandRecipe/)
- NexusMods (https://www.nexusmods.com/schedule1/mods/453)
- Thunderstore (https://thunderstore.io/c/schedule-i/p/robbmanes/ExpandRecipe/)

## Reporting Issues
This is my first mod ever, please be gentle with your reports.  I am a programmer but am very new to Unity, and wanted to try my hand at improving the Product Manager.  Feel free to either report issues within NexusMods or as an issue in the above GitHub link.

## ToDo
- Make the arrow icons properly sized/not stretched and still fit within the horizontalLayoutGroup
- Show and calculate costs of items including profit
- Don't show the last recusion for base weed mixes (Grandaddy Purple + Flu Medicine = Grandaddy Purple)