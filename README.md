# Universal Market Board

Dalamud plugin that searches in-game item names and pulls live market board listings from Universalis.

## Install In Dalamud

1. Open Dalamud settings with `/xlsettings`.
2. Go to the `Experimental` tab.
3. Find `Custom Plugin Repositories`.
4. Paste this JSON URL:

   `https://raw.githubusercontent.com/CherryFairy78/UniversalMarketBoard/main/pluginmaster.json`

5. Click the `+` button.
6. Save the settings.
7. Open the plugin installer with `/xlplugins`.
8. Search for `Universal Market Board` and install it.

## Repository JSON

- Custom repo JSON: `https://raw.githubusercontent.com/CherryFairy78/UniversalMarketBoard/main/pluginmaster.json`
- Release zip: `https://github.com/CherryFairy78/UniversalMarketBoard/releases/download/v0.1.11/UniversalMarketBoard.zip`

## Features

- Search for any item from the game data sheets
- View prices for a full region, a data centre, or a specific world
- Toggle HQ and NQ listings independently
- Sort listings from highest to lowest price or lowest to highest
- Show totals with tax included and hover to see the base total and tax split
- Open the installed version with `/umb`
- Open the dev-loaded version with `/umbdev`

## Notes

- The plugin reads public market data from `https://universalis.app/api/v2`
- Item names come from the local game data available through Dalamud
