# Referrals

The first authenticated login for an account created after the referral program starts shows an optional referral dialog. Existing players can enter a link or code from Settings. A player can apply only one referral; it cannot be changed, and their own code is rejected. Socials > Refer & Earn displays the player's link and earned balances. The link is copied or entered manually; opening it does not yet pass the code into the app automatically or carry it across installation.

The referred player's normal 30-minute Fuel refill tops their balance up to six instead of five. Purchased or otherwise awarded Fuel above the cap is preserved. The referrer earns one percent of each subsequently quoted and confirmed shop purchase, including season passes, in its payment currency. Rewards are rounded down to the smallest atomic unit per purchase and recorded once per payment. The API stores the original network, token mint, program and precision alongside the reward.

Rewards accumulate but cannot yet be withdrawn. Before enabling claims, implement funded, audited payout delivery, wallet ownership and replay checks, appropriate minimums/fee handling, and abuse controls. In particular, checking that a player does not enter their own code cannot prevent a person from using a different wallet or identity to refer themselves.

The server must be deployed with database migration `036_referrals` before a client using these endpoints is released. Old accounts are not prompted automatically but can still enter a code in Settings. Existing purchases are not credited retroactively; a referral must be active when the purchase quote is issued. This feature is not active on a server until the API and its migration are deployed, and the game client must also be released.
