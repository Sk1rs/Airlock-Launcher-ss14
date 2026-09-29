## Strings for the drop-down window to manage your active account

account-drop-down-none-selected = No account selected
account-drop-down-not-logged-in = Not logged in
account-drop-down-log-out = Log out
account-drop-down-log-out-of = Log out of { $name }
account-drop-down-switch-account = Switch account:
account-drop-down-select-account = Select account:
account-drop-down-add-account = Add account

## Localization for the "add favorite server" dialog window

add-favorite-window-title = Favorite Server
add-favorite-window-fetch-button = Get name from server
add-favorite-window-submit-button = Confirm
add-favorite-window-address-invalid = Address is invalid
add-favorite-window-label-name = Name:
add-favorite-window-label-address = Address:
# 'Example' name shown as a watermark in the name input box
add-favorite-window-example-name = Honk Station

## Strings for the "connecting" menu that appears when connecting to a server.

connecting-title-connecting = Connecting…
connecting-title-content-bundle = Loading…
connecting-cancel = Cancel
connecting-status-none = Starting connection…
connecting-status-update-error =
    There was an error while downloading server content. If this persists try some of the following:
    - Try connecting to another game server to see if the problem persists.
    - Try disabling or enabling software such as VPNs, if you have any.

    If you are still having issues, first try contacting the server you are attempting to join before asking for support on the Official Space Station 14 Discord or Forums.

    Technical error: { $err }
connecting-status-update-error-no-engine-for-platform = This game is using an older version that does not support your current platform. Please try another server or try again later.
connecting-status-update-error-no-module-for-platform = This game requires additional functionality that is not yet supported on your current platform. Please try another server or try again later.
connecting-status-update-error-unknown = Unknown
connecting-status-update-error-stalled = The server stopped sending data mid-download. Check your connection or try again in a minute; if it keeps happening, the server's content host is having trouble.
connecting-status-updating = Updating: { $status }
connecting-status-connecting = Fetching connection info from server…
connecting-status-connection-failed = Failed to connect to server!
connecting-status-starting-client = Starting client…
connecting-status-not-a-content-bundle = File is not a valid content bundle!
connecting-status-client-crashed = Client seems to have crashed while starting. If this persists, please ask on Discord or GitHub for support.
connecting-update-status-checking-client-update = Checking for server content update…
connecting-update-status-downloading-engine = Downloading game engine…
connecting-update-status-downloading-content = Downloading server content…
connecting-update-status-fetching-manifest = Fetching server manifest…
connecting-update-status-verifying = Verifying download integrity…
connecting-update-status-culling-engine = Clearing old content…
connecting-update-status-culling-content = Clearing old server content…
connecting-update-status-ready = Update done!
connecting-update-status-checking-engine-modules = Checking for additional dependencies…
connecting-update-status-downloading-engine-modules = Downloading extra dependencies…
connecting-update-status-committing-download = Synchronizing to disk…
connecting-update-status-loading-into-db = Storing assets in database…
connecting-update-status-loading-content-bundle = Loading content bundle…
connecting-update-status-unknown = You shouldn't see this

connecting-privacy-policy-text = This server requires that you accept its privacy policy before connecting.
connecting-privacy-policy-text-version-changed = This server has updated its privacy policy since the last time you played. You must accept the new version before connecting.
connecting-privacy-policy-view = View privacy policy
connecting-privacy-policy-accept = Accept (continue)
connecting-privacy-policy-decline = Decline (disconnect)

## Strings for the "direct connect" dialog window.

direct-connect-title = Direct Connect
direct-connect-text = Enter server address to connect:
direct-connect-connect = Connect
direct-connect-address-invalid = Address is invalid

## Strings for the "select account" dialog window.
select-account-dialog-title = Select Account
select-account-dialog-header = Select Account
select-account-dialog-description =
    Your currently selected account is not allowed to connect to this server, pick another one using an allowed account provider to join.
    Allowed account providers for this server: { $allowedAuths }
select-account-dialog-error =
    You do not have any accounts logged in that can connect to this server, exit?

## Strings for the "hub settings" dialog window.

hub-settings-title = Hub Settings
hub-settings-button-done = Done
hub-settings-button-cancel = Cancel
hub-settings-button-reset = Reset
hub-settings-button-reset-tooltip = Reset to default settings
hub-settings-button-add-tooltip = Add hub
hub-settings-button-remove-tooltip = Remove hub
hub-settings-button-increase-priority-tooltip = Increase priority
hub-settings-button-decrease-priority-tooltip = Decrease priority
hub-settings-explanation = Here you can add extra hubs to fetch game servers from. You should only add hubs that you trust, as they can 'spoof' game servers from other hubs. The order of the hubs matters; if two hubs advertise the same game server the hub with the higher priority (higher in the list) will take precedence.
hub-settings-heading-default = Default
hub-settings-heading-custom = Custom
hub-settings-warning-invalid = Invalid hub (don't forget http(s)://)
hub-settings-warning-duplicate = Duplicate hubs

## Strings for the login screen

login-log-launcher = Log Launcher

## Error messages for login

login-error-invalid-credentials = Invalid login credentials
login-error-account-unconfirmed = The email address for this account still needs to be confirmed. Please confirm your email address before trying to log in
login-error-account-2fa-required = 2-factor authentication required
login-error-account-2fa-invalid = 2-factor authentication code invalid
login-error-account-account-locked = Account has been locked. Please contact your account provider if you believe this to be in error.
login-error-unknown = Unknown error
login-errors-button-ok = Ok

## Strings for 2FA login

login-2fa-title = 2-factor authentication required
login-2fa-message = Please enter the authentication code from your app.
login-2fa-input-watermark = Authentication code
login-2fa-button-confirm = Confirm
login-2fa-button-recovery-code = Recovery code
login-2fa-button-cancel = Cancel

## Strings for the "login expired" view on login

login-expired-title = Login expired
login-expired-message =
    The session for this account has expired.
    Please re-enter your password.
login-expired-password-watermark = Password
login-expired-button-log-in = Log in
login-expired-button-log-out = Log out
login-expired-button-forgot-password = Forgot your password?

## Strings for the "forgot password" view on login

login-forgot-title = Forgot password?
login-forgot-message = If you've forgotten your password, you can enter the email address associated with your account here to receive a reset link.
login-forgot-email-watermark = Your email address
login-forgot-button-submit = Submit
login-forgot-button-back = Back to login
login-forgot-busy-sending = Sending email…
login-forgot-success-title = Reset email sent
login-forgot-success-message = A reset link has been sent to your email address.
login-forgot-error = Error

## Strings for the "login" view on login

login-login-title = Log in
login-login-username-watermark = Username or email
login-login-password-watermark = Password
login-login-show-password = Show Password
login-login-auth-server = Account Provider
login-login-auth-Space-Wizards-Federation = Space Wizards
login-login-auth-Space-Wizards = Space Wizards (Legacy)
login-login-auth-SimpleStation = SimpleStation
login-login-auth-guest = Guest
login-login-auth-Custom = Custom
login-login-button-log-in = Log in
login-login-button-forgot = Forgot your password?
login-login-button-resend = Resend email confirmation
login-login-button-register = Don't have an account? Register with { $server }!
login-login-busy-logging-in = Logging in…
login-login-error-title = Unable to log in

## Strings for the "register confirmation" view on login

login-confirmation-confirmation-title = Register confirmation
login-confirmation-confirmation-message = Please check your email to confirm your account. Once you have confirmed your account, press the button below to log in.
login-confirmation-button-confirm = I have confirmed my account
login-confirmation-button-cancel = Cancel
login-confirmation-busy = Logging in…

## Strings for the general main window layout of the launcher

main-window-title = Airlock Launcher
main-window-header-link-telegram = Telegram
main-window-header-link-website = GitHub
main-window-out-of-date = Launcher out of date
main-window-out-of-date-desc =
    This launcher is out of date.
    Please download a new version from our website.
main-window-out-of-date-desc-steam =
    This launcher is out of date.
    Please allow Steam to update the game.
main-window-out-of-date-exit = Exit
main-window-out-of-date-ignore = Ignore
main-window-out-of-date-download-manual = Download (manual)
main-window-intel-degrade-title = Intel 13th/14th Generation CPU detected.
main-window-intel-degrade-desc =
    The Intel 13th/14th generation CPUs are known to silently degrade permanently and die due to a microcode bug by Intel. We sadly can't tell if you are currently affected by this bug, so this warning appears for all users with these CPUs.

    We STRONGLY encourage you to update your motherboard's BIOS to the latest version to ensure prevention of further damage. If you are having stability issues/failing to start the game, downclock your CPU to get it stable again and use your warranty to ask about getting it replaced.

    We are not responsible to help with any issues that may arise from affected processors, unless you took the precautions and are sure your CPU is stable. This message will not appear again after you accept it.
main-window-intel-degrade-accept = I understand and have taken the necessary precautions.
main-window-rosetta-title = You are running the game using Rosetta 2!
main-window-rosetta-desc =
    You seem to be on an Apple Silicon Mac and are running the game using Rosetta 2. You may enjoy better performance and battery life by running the game natively.

    To do this, right click the launcher in Finder, select "Get Info" and uncheck "Open using Rosetta". After that, restart the launcher.

    If you are intentionally running the game using Rosetta 2, you can dismiss this message and it will not appear again. Although if you are doing this in an attempt to fix a problem, please make a bug report.
main-window-rosetta-accept = Continue
main-window-drag-drop-prompt = Drop to run game
main-window-busy-checking-update = Checking for launcher update…
main-window-busy-checking-login-status = Refreshing login status…
main-window-busy-checking-account-status = Checking account status
main-window-error-connecting-auth-server = Error connecting to authentication server
main-window-error-unknown = Unknown error occurred

## Long region names for server tag filters (shown in tooltip)

region-africa-central = Africa Central
region-africa-north = Africa North
region-africa-south = Africa South
region-antarctica = Antarctica
region-asia-east = Asia East
region-asia-north = Asia North
region-asia-south-east = Asia South East
region-central-america = Central America
region-europe-east = Europe East
region-europe-west = Europe West
region-greenland = Greenland
region-india = India
region-middle-east = Middle East
region-the-moon = The Moon
region-north-america-central = North America Central
region-north-america-east = North America East
region-north-america-west = North America West
region-oceania = Oceania
region-south-america-east = South America East
region-south-america-south = South America South
region-south-america-west = South America West

## Short region names for server tag filters (shown in filter check box)

region-short-africa-central = Africa Central
region-short-africa-north = Africa North
region-short-africa-south = Africa South
region-short-antarctica = Antarctica
region-short-asia-east = Asia East
region-short-asia-north = Asia North
region-short-asia-south-east = Asia South East
region-short-central-america = Central America
region-short-europe-east = Europe East
region-short-europe-west = Europe West
region-short-greenland = Greenland
region-short-india = India
region-short-middle-east = Middle East
region-short-the-moon = The Moon
region-short-north-america-central = NA Central
region-short-north-america-east = NA East
region-short-north-america-west = NA West
region-short-oceania = Oceania
region-short-south-america-east = SA East
region-short-south-america-south = SA South
region-short-south-america-west = SA West

## Strings for the "servers" tab

tab-servers-title = Servers
tab-servers-byond-title = BYOND Servers
tab-servers-byond-error-msg = BYOND not installed or found
tab-servers-byond-error-desc = To connect to BYOND servers, please install BYOND from https://www.byond.com/download/ and ensure it is set as the default program for handling byond:// links.
tab-servers-byond-error-link-text = Download BYOND
tab-servers-byond-exception-msg = Failed to connect to BYOND server
tab-servers-byond-exception-desc = An error occurred while trying to launch BYOND:

tab-servers-refresh = Refresh
filters = Filters ({ $filteredServers } / { $totalServers })
tab-servers-search-watermark = Search For Servers…
tab-servers-table-players = Players
tab-servers-table-name = Server Name
tab-servers-table-round-time = Time
tab-servers-list-status-error = There was an error fetching the master server lists. Maybe try refreshing?
tab-servers-list-status-partial-error = Failed to fetch some of the server lists. Ensure your hub configuration is correct and try refreshing.
tab-servers-list-status-updating-master = Fetching master server list…
tab-servers-list-status-none-filtered = No servers match your search or filter settings.
tab-servers-list-status-none = There are no public servers. Ensure your hub configuration is correct.

## Strings for the server filters menu

filters-title = Filters
filters-title-language = Language
filters-title-region = Region
filters-title-rp = Role-play level
filters-title-player-count = Player count
filters-title-18 = 18+
filters-title-hub = Hub
filters-18-yes = Yes
filters-18-yes-desc = Yes
filters-18-no = No
filters-18-no-desc = No
filters-player-count-hide-empty = Hide empty
filters-player-count-hide-empty-desc = Servers with no players will not be shown
filters-player-count-hide-full = Hide full
filters-player-count-hide-full-desc = Servers that are full will not be shown
filters-player-count-minimum = Minimum:
filters-player-count-minimum-desc = Servers with less players will not be shown
filters-player-count-maximum = Maximum:
filters-player-count-maximum-desc = Servers with more players will not be shown
filters-unspecified-desc = Unspecified
filters-unspecified = Unspecified

## Better strings for the server tags
tag-base-18 = 18+
tag-base-minage = Min Age
tag-base-region = Region
tag-base-lang = Language
tag-base-rp = RP Level

tag-region-af_c = Central Africa
tag-region-af_n = North Africa
tag-region-af_s = South Africa
tag-region-ata = Antarctica
tag-region-as_e = East Asia
tag-region-as_n = North Asia
tag-region-as_se = South East Asia
tag-region-am_c = Central America
tag-region-eu_e = East Europe
tag-region-eu_w = West Europe
tag-region-grl = Greenland
tag-region-ind = India
tag-region-me = Middle East
tag-region-luna = The Moon
tag-region-am_n_c = Central North America
tag-region-am_n_e = East North America
tag-region-am_n_w = West North America
tag-region-oce = Oceania
tag-region-am_s_e = East South America
tag-region-am_s_s = South South America
tag-region-am_s_w = West South America

tag-lang-cs = Czech
tag-lang-de = German
tag-lang-el = Greek
tag-lang-en = English
tag-lang-es = Spanish
tag-lang-et = Estonian
tag-lang-fi = Finnish
tag-lang-fil = Filipino
tag-lang-fr = French
tag-lang-he = Hebrew
tag-lang-id = Indonesian
tag-lang-it = Italian
tag-lang-ja = Japanese
tag-lang-nl = Dutch
tag-lang-pl = Polish
tag-lang-pt = Portuguese
tag-lang-pt_br = Portuguese (Brazil)
tag-lang-pt_pt = Portuguese (Portugal)
tag-lang-ru = Russian
tag-lang-sv = Swedish
tag-lang-th = Thai
tag-lang-tok = Toki Pona
tag-lang-tr = Turkish
tag-lang-uk = Ukrainian
tag-lang-zh_Hans = Chinese (Simplified)

tag-rp-none = None
tag-rp-low = Low
tag-rp-med = Medium
tag-rp-high = High

## Server roleplay levels for the filters menu

filters-rp-none = None
filters-rp-none-desc = None
filters-rp-low = Low
filters-rp-low-desc = Low
filters-rp-medium = Medium
filters-rp-medium-desc = Medium
filters-rp-high = High
filters-rp-high-desc = High

## Strings for entries in the server list (including home page)

server-entry-connect = Connect
server-entry-update-info = Edit
server-entry-add-favorite = Favorite
server-entry-remove-favorite = Unfavorite
server-entry-offline = OFFLINE
server-entry-player-count =
    { $players } / { $max ->
        [0] ∞
       *[1] { $max }
    }
server-entry-round-time = { $hours ->
 [0] Round { $mins }m
*[1] Round { $hours }h { $mins }m
}
server-entry-fetching = Fetching…
server-entry-description-offline = Unable to contact server
server-entry-description-fetching = Fetching server status…
server-entry-description-error = Error while fetching server description
server-entry-description-none = No server description provided
server-entry-status-lobby = Lobby
server-entry-status-post-round = Round over
server-entry-status-in-round = In round
server-entry-tags = Tags:
server-entry-allowed-auths = Allowed Account Providers:
server-fetched-from-hub = Fetched from { $hub }
server-entry-raise = Raise
server-entry-lower = Lower

## Strings for the "Development" tab
## These aren't shown to users so they're not very important

tab-development-title = { "[" }DEV]
tab-development-title-override = { "[" }DEV (override active!!!)]
tab-development-disable-signing = Disable Engine Signature Checks
tab-development-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-development-enable-engine-override = Enable engine override
tab-development-enable-engine-override-desc = Override path to load engine zips from (release/ in RobustToolbox)

## Strings for the "home" tab

tab-home-title = Home
tab-home-favorite-servers = Favorite Servers
tab-home-add-favorite = Add favorite
tab-home-refresh = Refresh
tab-home-direct-connect = Direct connect to server
tab-home-run-content-bundle = Run content bundle/replay
tab-home-go-to-servers-tab = Go to the servers tab
tab-home-favorites-guide = Mark servers as favorite for easy access here

## Strings for the "news" tab

tab-news-title = News
tab-news-recent-news = Recent News:
tab-news-pulling-news = Pulling news…

## Strings for the "options" tab

tab-options-title = Options
tab-options-flip = { "*" }flip
tab-options-clear-engines = Clear installed engines
tab-options-clear-content = Clear installed server content
tab-options-clear-content-close-client = Close running clients first
tab-servers-table-ping = Ping
tab-servers-sort-label = Sort the server list
tab-servers-sort-players = Most players
tab-servers-sort-ping = Lowest ping
tab-servers-sort-name = Name
server-entry-ping = {$ping} ms
tab-home-recent-servers = Recently played
tab-home-recent-clear = Clear
tab-home-reconnect = Reconnect
tab-servers-list-status-cached = No hub could be reached. Showing the last known server list, it may be out of date.
server-entry-copy-address = Copy address
tab-currency-title = Currency
currency-converter-title = Converter
currency-rates-title = Rates
currency-swap = Swap
currency-refresh = Update
currency-updating = Updating...
currency-updated = Bank of Russia rates for {$date}
currency-not-loaded = Rates have not been downloaded yet
currency-result-invalid = Enter an amount
currency-result-no-rates = No rate available, hit Update
currency-source = Rates come from the Central Bank of Russia (cbr.ru) and are published once per business day.
tab-stats-title = Statistics
stats-summary = {$time} played across {$servers} servers, {$sessions} sessions
stats-duration-hours = {$hours} h {$minutes} min
stats-duration-minutes = {$minutes} min
stats-sessions = {$sessions} sessions
stats-last-played = last: {$date}
stats-empty = Nothing played yet. Statistics show up after your first game.
stats-note = Only time spent in the SS14 client counts. BYOND servers are launched by a separate program the launcher cannot watch, sessions under 30 seconds are ignored, and a session is lost if the launcher is closed while the game is still running.
stats-clear = Clear statistics
stats-clear-confirm = Delete all statistics?
stats-clear-yes = Yes
stats-clear-no = Cancel
tab-options-import = Import from another launcher
tab-options-import-desc = Bring favourites and already downloaded game files over from another SS14 launcher installed on this PC.
import-title = Import from another launcher
import-intro = Other SS14 launchers keep their data in their own folder, so this build starts out empty. This window copies what can be copied over. The biggest win is the downloaded game files: they are several gigabytes and are identical between launchers, so importing them means not downloading the game a second time.
import-note-stats = No launcher records playtime, but they all log when the game started and stopped, so past sessions can be rebuilt from those logs. Only as far back as the log files still on disk, and only sessions the launcher saw end.
import-note-accounts = Accounts are not imported either. Logging in again takes a moment and beats moving login tokens between installs.
import-note-restart = Restart the launcher after importing game files.
import-sources = Launchers found on this PC
import-none = No other launchers found. This window looks for %APPDATA%/<name>/launcher/settings.db.
import-source-description = {$favorites} favourites, {$hubs} hubs, {$sessions} sessions in the logs, {$content} of game files ({$dir})
import-source-no-content = none
import-what = What to import
import-favorites = Favourite servers
import-hubs = Hubs
import-content = Downloaded game files
import-run = Import
import-close = Close
import-busy = Importing, copying game files can take a couple of minutes...
import-done = Done. Favourites: {$favorites}, hubs: {$hubs}.
import-content-ok = Game files imported, restart the launcher.
import-content-error-not-empty = Game files were not imported: this build already has downloaded files. Clear them in the options first.
import-content-error-schema = Game files were not imported: the other launcher stores them in a different format.
import-content-error-no-content = Game files were not imported: that launcher has not downloaded any.
import-content-error-failed = Game files were not imported, an error occurred. See the log for details.
import-stats = Playtime statistics (rebuilt from the other launcher's logs)
import-stats-ok = Statistics: {$sessions} past sessions recovered.
import-stats-error-not-empty = Statistics were not imported: this build has already recorded playtime. Clear it on the statistics tab first, otherwise sessions would be counted twice.
import-stats-error-nothing = Statistics were not imported: the other launcher's logs have no finished sessions left in them.
tab-character-title = Character
character-fork = Fork
character-species = Species
character-female = Female
character-direction = Facing
character-direction-south = Front
character-direction-north = Back
character-direction-east = Right
character-direction-west = Left
character-skin-tone = Skin tone
character-skin-color = Skin
character-eye-color = Eyes
character-marking-none = none
character-export = Export PNG
character-refresh = Re-download sprites
character-loading = Downloading sprites and prototypes...
character-loaded = {$species} species, {$markings} markings
character-load-failed = Could not load this fork's resources. See the log for details.
character-render-empty = Nothing to draw: this species has no body sprites in the expected places.
character-render-failed = Could not draw the character. See the log for details.
character-exported = Saved to {$path}
character-export-failed = Could not save the file. See the log for details.
character-layer-chest = Chest
character-layer-head = Head
character-layer-snout = Snout
character-layer-snoutcover = Snout cover
character-layer-eyes = Eyes
character-layer-hair = Hair
character-layer-facialhair = Facial hair
character-layer-headtop = Head top
character-layer-headside = Head side
character-layer-tail = Tail
character-layer-tailoverlay = Tail overlay
character-layer-overlay = Overlay
character-layer-larm = Left arm
character-layer-rarm = Right arm
character-layer-lleg = Left leg
character-layer-rleg = Right leg
character-layer-lhand = Left hand
character-layer-rhand = Right hand
character-layer-lfoot = Left foot
character-layer-rfoot = Right foot
character-layer-undergarmenttop = Undergarment top
character-layer-undergarmentbottom = Undergarment bottom
character-limited = GitHub would not give out the file list (it limits that per hour), so only the standard species and markings were found. Try "Re-download sprites" later for the fork-specific ones.
character-job = Job
character-job-none = no uniform
character-fast-download = Fast download
character-fast-download-desc = Fetches many files at once instead of one after another, which is many times quicker. Turn it off if your connection dislikes parallel requests.
character-name = Name
character-age = Age
character-profile-export = Save character
character-profile-export-desc = Saves the character into the game's characters folder, in the same format the game uses, so it appears in the game's list on the next launch.
character-profile-import = Load character
character-profile-import-desc = Reads a character file saved by the game or by this editor.
character-profile-exported = Character saved to {$path}
character-profile-imported = Loaded {$name}
character-profile-imported-partial = Loaded {$name}, but this fork has no match for {$missing} of its markings.
character-profile-import-invalid = That file is not a character.
tab-options-netdiag = Network check
tab-options-netdiag-desc = Checks every host the launcher depends on (hubs, accounts, engine downloads, GitHub) and shows which ones are slow or blocked from here.
netdiag-title = Network check
netdiag-intro = Each host gets a small request; the ones that serve big files are read for a few seconds to see whether data really flows. A host marked "throttled" answers but sends almost nothing, which is what makes a download look frozen.
netdiag-run = Check again
netdiag-close = Close
netdiag-running = Checking…
netdiag-pending = …
netdiag-ok = OK
netdiag-slow = slow
netdiag-throttled = throttled
netdiag-unreachable = unreachable
netdiag-all-good = Everything answers and flows normally.
netdiag-summary = Problems: {$bad} host(s) blocked or unreachable, {$slow} slow.
netdiag-group-hubs = Server list
netdiag-group-auth = Accounts
netdiag-group-engine = Game engine
netdiag-group-github = Character editor
character-gallery = Gallery
character-gallery-desc = Every character saved on this PC, by this launcher or any other, with a preview.
gallery-title = Characters
gallery-intro = Characters from this launcher and from every other SS14 launcher on this PC. Previews are drawn as the selected fork would show them; markings that fork lacks are simply missing from the picture.
gallery-load = Load
gallery-open-folder = Open folder
gallery-close = Close
gallery-empty = No characters yet. Save one from the editor, or play a round: the game writes them to {$folder}.
gallery-count = {$count} character(s)
gallery-source-ours = this launcher
server-entry-watch = Watch
server-entry-unwatch = Watching
server-entry-watch-desc = Get a notification when this server goes back to the lobby for a new round (or reaches the player count set in the options).
tab-options-watch-threshold = Notify at player count
tab-options-watch-threshold-desc = For watched servers: also notify when the player count reaches this number. 0 turns that off; the new-round notification stays.
watch-notification-title = New round
watch-notification-connect = Join
watch-notification-dismiss = Later
watch-notification-lobby = A new round is in the lobby. Players online: {$players}.
watch-notification-players = Players online: {$players}.
tab-guides-title = Guides
guides-intro = Wikis and role guides the community keeps. Each link opens in your browser; the tag says which language it is in.
guides-screenshots = Screenshots
guides-screenshots-desc = Opens the folder where the game saves screenshots taken while playing from this launcher.
guides-section-wiki = Wiki
guides-section-roles = Role guides
guides-wiki-corvax = Corvax wiki
guides-wiki-fandom = Fandom wiki
guides-role-chemistry = Chemistry
guides-role-bartender = Bartender: drink recipes
disclaimer-title = About this launcher
disclaimer-heading = Before you go in
disclaimer-text = This is a community fork. The game is made by Space Wizards and hundreds of contributors; the launcher is built on SimpleStation's. Everything else is on the forker.
disclaimer-upstream = The game
disclaimer-fork = This fork
disclaimer-base = Built on
disclaimer-close = Continue
disclaimer-wait = {$seconds} s
tab-options-auto-connect = Connect to the last server on startup
tab-options-auto-connect-desc = Skips the launcher and goes straight to the server you played last. Only happens when an account is signed in.
tab-options-copy-diagnostics = Copy diagnostics
tab-options-copy-diagnostics-desc = Copies launcher version, system details, folder locations and how much space downloads take, ready to paste when asking for help.
screenshots-title = Screenshots
screenshots-open = Open
screenshots-copy = Copy
screenshots-delete = Delete
screenshots-open-folder = Open folder
screenshots-close = Close
screenshots-count = {$count} screenshot(s), newest first
screenshots-empty = No screenshots yet. The game saves them to {$folder}.
screenshots-deleted = Deleted {$name}
character-randomize = Random
character-randomize-desc = Rolls a random species, colours and markings. A starting point, not a finished character.
character-copy = Copy PNG
character-copy-desc = Puts the drawn character on the clipboard as a file, ready to paste into a chat.
server-entry-note = Note
server-entry-note-hint = why you keep this one, rules, your character…
tab-options-storage = Disk usage
tab-options-storage-busy = Measuring…
tab-options-zapret = Blocking bypass
tab-options-zapret-desc = Set up or control zapret, which gets the game's hubs and download servers through provider-level throttling.
zapret-title = Blocking bypass
zapret-heading = Trouble reaching servers?
zapret-intro = Some providers throttle the hubs and the servers the game downloads from, which shows up as an empty server list or a download that crawls. zapret works around that by reshaping the traffic. It ships with this launcher — downloading it yourself is awkward when the connection it fixes is the one blocking the download — and the launcher only tells it which of the game's hosts to cover. It is a separate open-source project (MIT).
zapret-warning = It needs administrator rights and installs a traffic filtering driver (WinDivert), which antivirus software often reacts to. Nothing is installed unless you press the button.
zapret-install = Download and set up
zapret-install-desc = Fetches the latest release from the project's GitHub page into the launcher's data folder. About 1.5 MB.
zapret-strategy = Strategy
zapret-strategy-desc = Which trick works depends on the provider. If one does not help, stop it and try another.
zapret-start = Start
zapret-stop = Stop
zapret-refresh = Refresh
zapret-autostart = Start along with the launcher
zapret-hosts-note = The launcher keeps the game's hosts in the project's own user list, so its own files stay untouched.
zapret-open-folder = Open folder
zapret-project = Project page
zapret-close = Close
zapret-status-missing = Not installed.
zapret-status-stopped = Installed, not running.
zapret-status-running = Running.
zapret-starting = Starting, confirm the administrator prompt…
zapret-step-fetching = Looking up the latest release…
zapret-step-downloading = Downloading…
zapret-step-unpacking = Unpacking…
zapret-install-failed = Could not set it up: {$reason}. If the download is what is blocked, grab the archive from the project page by hand and use "From archive".
zapret-install-file = From archive
zapret-install-file-desc = Set up from a zapret release archive you already downloaded. Use this when the download cannot get through.
zapret-remove = Remove
zapret-remove-desc = Deletes it entirely. Do this if your connection is fine without it; the launcher can download it again later.
zapret-removed = Removed.
zapret-remove-failed = Could not remove it: {$reason}
tab-servers-list-status-error-hubs = No hub answered ({$answered} of {$asked}). Check your connection, your hub settings, or turn on the blocking bypass in the options.
tab-servers-list-status-partial-hubs = Only {$answered} of {$asked} hubs answered, so servers may be missing from this list.
tab-options-background = Background picture
tab-options-background-desc = Put a picture behind the launcher. Strength is how much of it shows; dim lays darkness over it so text stays readable.
tab-options-background-pick = Choose picture
tab-options-background-clear = Remove
tab-options-background-fill = Crop to fill
tab-options-background-strength = Strength
tab-options-background-dim = Dim
tab-options-open-log-directory = Open log directory
tab-options-open-data-directory = Open data folder
tab-options-open-exports-directory = Open character exports
tab-options-theme = UI theme
tab-options-theme-desc = Colour palette for the launcher. Applies right away.
theme-name-default = Default
theme-name-midnight = Midnight (OLED)
theme-name-slate = Slate
theme-name-nanotrasen = Nanotrasen
theme-name-amber = Amber
theme-name-syndicate = Syndicate
theme-name-atmospherics = Atmospherics
theme-name-hydroponics = Hydroponics
theme-name-epistemics = Epistemics
theme-name-daylight = Daylight (light)
theme-name-oceanic = Oceanic
theme-name-sakura = Sakura
theme-name-rust = Rust
theme-name-monochrome = Monochrome
tab-options-account-settings = Account Settings
tab-options-account-settings-desc = You can manage your account settings, such as changing email or password, through our website.
tab-options-compatibility-mode = Compatibility Mode
tab-options-compatibility-mode-desc = This forces the game to use a different graphics backend, which is less likely to suffer from driver bugs. Try this if you are experiencing graphical issues or crashes.
tab-options-log-client = Log Client
tab-options-log-client-desc = Enables logging of any game client output. Useful for developers.
tab-options-log-launcher = Log Launcher
tab-options-log-launcher-desc = Enables logging of the launcher. Useful for developers. (requires launcher restart)
tab-options-verbose-launcher-logging = Verbose Launcher Logging
tab-options-verbose-launcher-logging-desc = For when the developers are *very* stumped with your problem. (requires launcher restart)
tab-options-ui-scaling = UI Scale
tab-options-ui-scaling-lock = Lock X and Y together
tab-options-ui-scaling-save = Save
tab-options-seasonal-branding = Seasonal Branding
tab-options-seasonal-branding-desc = Whatever temporally relevant icons and logos we can come up with.
tab-options-disable-signing = Disable Engine Signature Checks
tab-options-disable-signing-desc = { "[" }DEV ONLY] Disables verification of engine signatures. DO NOT ENABLE UNLESS YOU KNOW EXACTLY WHAT YOU'RE DOING.
tab-options-hub-settings = Hub Settings
tab-options-hub-settings-desc = Change what hub server or servers you would like to use to fetch the server list.
tab-options-desc-incompatible = This option is incompatible with your platform and has been disabled.

## For the language selection menu.

# Text on the button that opens the menu.
language-selector-label = Language
# "Save" button.
language-selector-save = Save
# "Cancel" button.
language-selector-cancel = Cancel
language-selector-help-translate = Want to help translate? You can!
language-selector-system-language = System language ({ $languageName })
# Used for contents of each language button.
language-selector-language = { $languageName } ({ $englishName })

## Miscellaneous

# Generic "Done!" message used for some buttons.
button-done = Done!
