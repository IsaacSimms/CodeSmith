# Update README / CLAUDE.md

Sync the project's documentation to reflect recent changes.

## Steps

1. Read `CLAUDE.md` and `README.md` (if it exists) to understand current documented state.
2. Run `git diff main...HEAD --stat` to identify what changed since the last merge.
3. For each changed area, update the relevant section(s):
   - New API endpoints → update the API Endpoints table in `CLAUDE.md`
   - New features or pages → update feature descriptions
   - New dev commands or env vars → update Dev Commands section
   - Stack changes (new deps, new tools) → update the Stack table
4. Do NOT rewrite sections that are still accurate. Only update what changed.
5. Present a diff of proposed changes and wait for explicit approval before writing.
