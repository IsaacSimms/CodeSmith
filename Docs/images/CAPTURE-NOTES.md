# README screenshots — capture notes

The root [`README.md`](../../README.md) references three images from this folder. Until they exist, GitHub renders broken-image placeholders, so capture these before making the repo public.

Capture against the **live site** (`https://www.code-smith.cc`), signed in — the real URL in frame is part of the point. Keep the browser window at a wide desktop size; GitHub renders README images at roughly 850px wide, so anything narrower looks cramped and anything with tiny text becomes unreadable.

| File | Screen | What must be visible |
|---|---|---|
| `tutoring.png` | `/pairedprogrammer` — the hero image, top of the README | Split-screen: generated problem on one side, real code in the Monaco editor on the other, and a chat reply **mid-stream** so the token streaming reads as live. Bonus if the terminal shows a completed run. |
| `prompt-lab.png` | `/prompt-lab` — results view after a submission | Per-test pass/fail rows **and** the per-criterion rubric scores with evaluator feedback. The mix of passes and failures tells the story better than a perfect score. |
| `system-lab.png` | `/system-lab` — evaluation view after a submission | Total score, per-criterion rubric breakdown, and the cross-cutting dimension deductions. The deductions are the distinctive part — make sure at least one is non-zero. |

Notes:

- Prefer whichever theme looks better against GitHub's default; both light and dark render fine, but be consistent across all three.
- Crop out browser chrome you don't need, but keep the address bar in `tutoring.png`.
- PNG, and keep each under ~1MB — GitHub serves them on every README view.
- Alt text is already written into the README; update it there if a shot ends up showing something different.
