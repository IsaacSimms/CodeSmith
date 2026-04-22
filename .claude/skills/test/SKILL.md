# Run Tests

Run the full test suite for this repo and report results.

## Steps

1. Run the .NET backend tests:
   ```
   cd CodeSmith.Tests && dotnet test
   ```
2. Run the frontend Vitest unit tests:
   ```
   cd CodeSmith.Web && npm test -- --run
   ```
3. Report results in this format for each suite:
   `[N/M passed] — Suite: <name>`
4. If any tests fail, paste the first 3 failure messages verbatim with file:line.

Do NOT attempt to fix failures until I explicitly confirm.
