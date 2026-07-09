# Project History

## Overview

Universal Market Board started as a lightweight Universalis browser for Dalamud and quickly grew into a more complete in-game market workflow tool.

## Timeline

### 2026-07-06: First public release
- `v0.1.0` launched the core plugin.
- The first release focused on item search, HQ/NQ filtering, sorting, travel support, appearance options, and right-click item opening.

### 2026-07-06: Plugin identity and command cleanup
- `v0.1.1` corrected the author name to Meghann.
- Main and dev commands were split into `/umb` and `/umbdev`.

### 2026-07-06 to 2026-07-07: Marketboard workflow expansion
- `v0.1.2` added right-click support for in-game marketboard Item Search results.
- `v0.1.3` improved responsiveness, title bar versioning, and search result handling.
- `v0.1.4` fixed an update-blocking version mismatch.

### 2026-07-07: Public listing and vendor experimentation
- `v0.1.5` hid the dev command from the public plugin listing.
- `v0.1.6` briefly added vendor and shop item right-click support.
- `v0.1.7` removed vendor and shop integration again after stability issues and kept the safer right-click flows.

### 2026-07-07 to 2026-07-08: Compatibility and presentation polish
- `v0.1.8` moved the plugin’s right-click entry to the bottom for better compatibility with other plugins.
- `v0.1.9` added the plugin icon so the public listing had a polished visual identity.

### 2026-07-08 to 2026-07-09: Scope accuracy and broader market coverage
- `v0.1.10` fixed world-specific searches that were incorrectly returning wider data centre results.
- `v0.1.11` introduced combined region scopes for Japan, North America, and Europe.
- `v0.1.11` also updated totals to include market tax and added a hover breakdown for tax and base total.

## Design Direction

The project has gradually shifted toward three goals:
- Fast in-game access to live Universalis data.
- Better compatibility with other plugins and normal game flows.
- A polished interface that still stays easy to use while showing more market detail.
