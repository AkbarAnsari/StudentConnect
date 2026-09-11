# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.1.7.

## Current Architecture (Phase 3)

```
Browser
   ↓
Angular
   ↓
HTTP
   ↓
ASP.NET Core API
   ↓
In-memory data
```

`StudentService` calls the ASP.NET Core Web API in `../backend/StudentConnect.Api`
over HTTP. The API base URL is configured per-environment in
`src/environments/` (not hard-coded in components). See
[../docs/phase-3.md](../docs/phase-3.md) for the full explanation.

## Future Phase 4

```
Browser
   ↓
Angular
   ↓
HTTP
   ↓
ASP.NET Core API
   ↓
SQL Database
```

Phase 4 will replace the API's in-memory student list with a real SQL database;
Angular does not need to change.

## Running the API

The API must be running for the Students pages to load data:

```bash
cd ../backend/StudentConnect.Api
dotnet run --launch-profile http
```

This starts the API at `http://localhost:5286`.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
