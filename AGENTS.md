# Todo List Project Development Guidelines (Constitution)

This document is established based on the first phase MVP development process and results, serving as the highest guiding principle for all future requirement expansions and maintenance. When an Agent (AI assistant) or developer makes any modifications or adds new features, they must comply with the following guidelines.

## 1. Core Technology Stack

*   **Backend**: .NET 10 (C#), ASP.NET Core Web API (Controllers pattern)
*   **Database**: SQLite, Entity Framework Core 10
*   **Frontend**: React 19, TypeScript, Vite, CSS Modules
*   **Testing**: Playwright (E2E), xUnit (Backend), Vitest & React Testing Library (Frontend)

## 2. Project Structure and Commands

*   **Backend Path**: `apps/api/`
*   **Frontend Path**: `apps/web/`
*   **Start Development Servers**:
    *   Backend: `powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Task api` (runs on http://127.0.0.1:5050)
    *   Frontend: `powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Task web` (runs on http://127.0.0.1:5173)
*   **Run Tests**:
    *   E2E Tests: Go to `apps/web/` and execute `npm.cmd run test:e2e` (If other browsers download too slowly, append `--project=chromium` to test only Chromium)

## 3. Development and Architecture Guidelines

### 3.1 Backend API and Database
*   **Contract-First**: Must adhere to the OpenAPI specification. All routes must start with `/api/v1`.
*   **Input Validation**: Must use DTOs and DataAnnotations for strict validation. Do not accept unknown fields or invalid enum values.
*   **Time and Timezones**: The backend always stores UTC time, and transmissions use the ISO 8601 UTC format string. For simple dates (like dueDate), use `DateOnly?` without timezone conversions.
*   **Database**: SQLite single instance. Do not modify the DB structure directly during development; it must be done via EF Core Migrations.

### 3.2 Frontend and UI
*   **State and Error Handling**: Any API request must include state handling for Loading, Error, and Empty State.
*   **Failsafe Mechanisms**: Prevent duplicate form submissions. When an API fails, preserve the user's input so they can retry.
*   **Accessibility (A11y)**: Ensure smooth keyboard operation (e.g., Dialogs support closing with Escape), and clear focus states.
*   **Styling**: Strictly adhere to CSS Modules for component style isolation to avoid global pollution.

### 3.3 Testing Guidelines
*   Any newly added features must synchronously update or add E2E tests (located in `tests/e2e/todo.spec.ts`) to ensure existing flows are not broken.
*   If exceptions like 503 occur during Playwright testing, there should be a retry or prompt mechanism.
*   The backend should have integration tests with an independent SQLite database.

## 4. Change Process

1. **Read Guidelines**: Before developing, read this `AGENTS.md` and `docs/todo-list-development-plan.md`.
2. **Make a Plan**: If it is a major change, an Implementation Plan must be proposed and approved by the user first.
3. **Execute Modifications**: Write code according to the guidelines.
4. **Verify Tests**: Must pass compilation locally and pass E2E tests.
