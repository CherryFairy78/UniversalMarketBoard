# Changelog

All notable changes to Universal Market Board will be documented in this file.

## [0.1.15] - 2026-07-14
- Reworked Settings into dedicated Appearance, Debug, and Changelog panels in one window.
- Added Lifestream detection, a direct link to its GitHub page, and a copyable support report with current error status.

## [0.1.14] - 2026-07-12
- Fixed the distributed plugin manifest so the packaged zip version matches the repo version.
- Hardened shutdown and update cleanup to avoid disposed-token errors during unload.

## [0.1.13] - 2026-07-12
- Removed the Market Flippers feature from the live plugin to keep the main market board focused and stable.
- Fixed an item-search cancellation crash caused by disposed search tokens.
- Made `Refresh Prices` bypass cached listing data so manual refreshes pull a fresh Universalis snapshot.

## [0.1.12] - 2026-07-09
- Re-published the latest plugin package with corrected release metadata after the `v0.1.11` asset was distributed with the wrong embedded version.

## [0.1.11] - 2026-07-09
- Added combined region scopes for Japan, North America, and Europe.
- Updated the world picker to show all worlds inside those combined scopes.
- Changed the `Total` column to include market tax and added a hover breakdown for base total and tax.

## [0.1.10] - 2026-07-08
- Fixed world-specific searches incorrectly showing data centre-wide listings.
- Backfilled world names for single-world Universalis responses so the `World` column stays populated.
- Published the updated package and repo metadata for Dalamud updates.

## [0.1.9] - 2026-07-08
- Added the new plugin icon to the public repo manifest.
- Added the icon URL to the local plugin manifest.
- Published the icon asset from the repository for Dalamud to display.

## [0.1.8] - 2026-07-07
- Moved the right-click market board entry to the bottom of the menu.
- Kept compatibility with plugins that expect priority access to the top of the list.

## [0.1.7] - 2026-07-07
- Removed vendor and shop right-click integration entirely.
- Kept stable right-click support for chat, inventory, item search, and marketboard flows.
- Bumped the public plugin manifest and release package to `0.1.7`.

## [0.1.6] - 2026-07-07
- Added vendor and shop right-click support so items from vendor windows could be sent directly to Universal Market Board.
- Updated the live package metadata to `0.1.6`.

## [0.1.5] - 2026-07-07
- Removed the dev slash command from the public plugin description so users only see `/umb` in the live listing.

## [0.1.4] - 2026-07-07
- Fixed the published package version mismatch that blocked updates from `v0.1.1`.
- Updated the release zip so the manifest and assembly versions matched.

## [0.1.3] - 2026-07-07
- Added a version label in the title bar and corrected old saved window titles to `Universal Market Board`.
- Fixed the updated-column hover tooltip position.
- Added scrolling for long search result lists.
- Improved item search responsiveness.
- Reduced blank or slow Universalis listing loads with lighter requests and short-term caching.

## [0.1.2] - 2026-07-06
- Added right-click support for in-game marketboard Item Search entries.
- Added automatic cleanup for plugin log files older than 30 days.
- Included the latest item context-menu fixes and release metadata updates.

## [0.1.1] - 2026-07-06
- Corrected the author name to `Meghann`.
- Split commands between installed (`/umb`) and dev-loaded (`/umbdev`) builds.
- Updated package metadata to match the published plugin name.

## [0.1.0] - 2026-07-06
- Initial public release of Universal Market Board.
- Included item search by data centre or world.
- Included HQ and NQ listing filters.
- Included price sorting.
- Included Lifestream travel buttons.
- Included appearance customisation.
- Included right-click item integration.
