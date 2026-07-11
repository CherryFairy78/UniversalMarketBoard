# Release Notes

This file collects the published release notes in one place for quick reference.

## Latest

### v0.1.13
- Removed Market Flippers from the live plugin to keep the core market board experience stable.
- Fixed the item-search disposed-token crash.
- Updated `Refresh Prices` so it forces a fresh Universalis fetch instead of reusing cached listing data.

## Previous

### v0.1.12
- Re-published the latest plugin package with corrected release metadata after the `v0.1.11` asset was distributed with the wrong embedded version.

### v0.1.11
- Added combined region scopes for Japan, North America, and Europe.
- Updated the world picker to show all worlds inside those combined scopes.
- Changed the `Total` column to include market tax and added a hover breakdown for base total and tax.

### v0.1.10
- Fixed world-specific searches incorrectly showing data centre-wide listings.
- Backfilled world names for single-world Universalis responses so the `World` column stays populated.
- Published the updated package and repo metadata for Dalamud updates.

### v0.1.9
- Added the new plugin icon to the public repo manifest.
- Added the icon URL to the local plugin manifest.
- Published the icon asset from the repository for Dalamud to display.

### v0.1.8
- Moved the right-click market board entry to the bottom of the menu.
- Kept compatibility with plugins that expect priority access to the top of the list.

### v0.1.7
- Removed vendor and shop right-click integration entirely.
- Kept stable right-click support for chat, inventory, item search, and marketboard flows.
- Bumped the public plugin manifest and release package to `0.1.7`.

### v0.1.6
- Added vendor and shop right-click support so items from vendor windows could be sent directly to Universal Market Board.
- Updated the live package metadata to `0.1.6`.

### v0.1.5
- Removed the dev slash command from the public plugin description so users only see `/umb` in the live listing.

### v0.1.4
- Fixed the published package version mismatch that blocked updates from `v0.1.1`.
- Updated the release zip so the manifest and assembly versions matched.

### v0.1.3
- Added a version label in the title bar and corrected old saved window titles to `Universal Market Board`.
- Fixed the updated-column hover tooltip position.
- Added scrolling for long search result lists.
- Improved item search responsiveness.
- Reduced blank or slow Universalis listing loads with lighter requests and short-term caching.

### v0.1.2
- Added right-click support for in-game marketboard Item Search entries.
- Added automatic cleanup for plugin log files older than 30 days.
- Included the latest item context-menu fixes and release metadata updates.

### v0.1.1
- Corrected the author name to `Meghann`.
- Split commands between installed (`/umb`) and dev-loaded (`/umbdev`) builds.
- Updated package metadata to match the published plugin name.

### v0.1.0
- Initial public release of Universal Market Board.
- Included item search by data centre or world.
- Included HQ and NQ listing filters.
- Included price sorting.
- Included Lifestream travel buttons.
- Included appearance customisation.
- Included right-click item integration.
