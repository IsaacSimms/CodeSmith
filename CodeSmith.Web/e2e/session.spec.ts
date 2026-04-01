import { test, expect } from "@playwright/test";

test.describe("Session flow", () => {
  test("renders difficulty selector on home page", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("CodeSmith")).toBeVisible();
    await expect(page.getByRole("button", { name: "Easy" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Medium" })).toBeVisible();
    await expect(page.getByRole("button", { name: "Hard" })).toBeVisible();
  });
});
