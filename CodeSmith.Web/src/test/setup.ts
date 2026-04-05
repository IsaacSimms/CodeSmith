// == Vitest Test Setup == //
import { vi } from "vitest";
import "@testing-library/jest-dom/vitest";

// jsdom does not implement scrollIntoView
Element.prototype.scrollIntoView = () => {};

// Monaco Editor does not render in jsdom, mock it to display code as plain text
import { createElement } from "react";
vi.mock("@monaco-editor/react", () => ({
  default: ({ value }: { value: string }) => createElement("pre", { "data-testid": "monaco-editor" }, value),
}));
