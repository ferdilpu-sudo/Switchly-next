# TASK-003 Results — Priority Provider Compatibility (M-001, tier 1)

**Date:** 2026-11-24
**Scope:** Unauthenticated public-page smoke check. No accounts, no sign-in, no
user-agent spoofing, no bypassing of provider safeguards.

## Environment

Windows 11 Enterprise LTSC 2024 (build 10.0.26100) · CefSharp.OffScreen.NETCore
156.0.300 / CEF 156.0.30 + chromium-156.0.8078.96 · default embedded-browser UA ·
session elevated (uses `do-not-de-elevate`; production must run non-elevated).

## Compatibility matrix (public page, no sign-in)

| Provider | Requested URL | Final URL | HTTP | Title (public page) |
|---|---|---|---|---|
| Google Flow | https://flow.google | https://flow.google.com/about | 200 | Google Flow - AI Creative Studio for Video, Images & Custom Tools |
| ChatGPT | https://chatgpt.com | https://chatgpt.com/ | 200 | ChatGPT: Chat, Work, Create & Code with AI |
| Dola | https://dola.com | https://www.dola.com/chat/ | 200 | Dola AI - Your everyday AI assistant |
| SeaArt | https://seaart.ai | https://www.seaart.ai/ | 200 | AI Creativity Community for Art, Video, Chat & Tools \| SeaArt AI |

**Result:** all four providers load normally in the embedded CEF browser at the
public-page level. Redirects are canonical-domain normalization
(flow.google→flow.google.com, dola.com→www.dola.com), not rejection.

## Caveats / limitations (do not over-claim)

1. **Sign-in NOT tested.** This is tier 1 only. Google sign-in in particular is
   known to sometimes block embedded/CEF browsers ("This browser or app may not
   be secure") even when the public page loads. Tier-2 authenticated checks are
   required before claiming provider compatibility for login workflows.
2. **Elevated session.** The PoC runs elevated (`do-not-de-elevate`); production
   non-elevated behavior should be re-confirmed during M-002/M-008.
3. **OffScreen flakiness.** An intermittent native crash (`0xC0000005` in
   `CefBrowserHost.CreateBrowser` during `OnContextInitialized`) was observed
   twice across runs and mitigated with a 1.5 s settle delay after
   `Cef.Initialize`. Needs re-validation on a non-elevated, WPF-hosted engine.

## Not covered here

- Uploads, downloads, popups, clipboard, media/WebGL per provider (tier 2).
- Authenticated workflows (tier 2, requires accounts).

## Sign-in reachability (non-account follow-up, 2026-11-24)

| Provider | Sign-in URL | Result |
|---|---|---|
| Google | accounts.google.com/v3/signin/identifier | HTTP 400 "Error 400 (Bad Request)" — inconclusive: could be embedded-browser rejection OR the direct URL lacking flow context |
| ChatGPT | chatgpt.com/auth/login | HTTP 200, loads (title "ChatGPT") |
| Dola / SeaArt | — | SPA-gated (sign-in behind JS buttons); not reachable headlessly |

**Conclusion:** sign-in cannot be fully validated in the headless harness.
ChatGPT's login page loads; Google's sign-in surface returned 400 (needs
investigation); Dola/SeaArt sign-in is JS-gated. Definitive sign-in validation
requires the WPF shell (M-002) and the user's accounts.

## Tier-2 sign-in (authenticated) — product-owner manual check (2026-11-24)

**Result: PASS** — the product owner signed in to all four providers (Google
Flow, ChatGPT, Dola, SeaArt) through the embedded WPF browser. The earlier
headless Google 400 was a direct-URL artifact, not an embedded-browser block.


