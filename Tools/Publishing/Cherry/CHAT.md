# Cherry home chat

The Cherry-only home button mounts a Cherry chat iframe on demand. It hides
the complete panel when the player leaves the home screen. The button and
token request are inactive outside a trusted Cherry launch.

The separate chat embed uses **app-trusted+wallet**, must allow the
`https://cherry.battlecities.com` origin, and allow its configured room. The
existing website embed uses a different authentication mode: changing its
mode or secret would affect that website. Whether a new embed can use the
existing community room must be confirmed in the portal. The player approves
Cherry's chat authentication message when requested.

The API requires these server-only environment variables:

* `BATTLECITY_CHERRY_CHAT_EMBED_ID`: the chat embed ID (not the mini-app ID).
* `BATTLECITY_CHERRY_CHAT_ROOM_ID`: a room allowed by that embed.
* `BATTLECITY_CHERRY_CHAT_SECRET`: that embed's signing secret. Never put it in
  the game, the Pages repository, the browser, test output, or source control.

`POST /api/auth/cherry-chat` accepts the existing Cherry bearer session and
creates a five-minute embed token for the wallet in that verified session. It
does not accept a caller-provided wallet. The route returns unavailable until
the configuration is present. Real Cherry host, embed sign-in, messaging,
and mobile layout must be tested before publishing this feature.
