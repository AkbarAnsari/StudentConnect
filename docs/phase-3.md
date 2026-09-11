# Phase 3 — C# ASP.NET Core Web API

## Architecture

```
                USER
                 |
                 v
             BROWSER
                 |
                 v
        ANGULAR FRONTEND
                 |
                 | HTTP GET
                 v
       ASP.NET CORE WEB API
                 |
                 v
        IN-MEMORY STUDENT DATA   <-- no database yet (Phase 4)
                 |
                 v
              JSON
                 |
                 v
        ANGULAR FRONTEND
                 |
                 v
              SCREEN
```

## What is a backend?
The backend is a separate application (the ASP.NET Core Web API) that owns the
student data and the rules for accessing it. Angular no longer stores or invents
any student data itself — it only asks the backend for it.

## Why do we need a backend?
If all data and logic lived in the browser, anyone could open developer tools and
see or change it. A backend keeps data and rules on a server the browser can't
directly inspect or tamper with, and lets many different frontends (web, mobile,
etc.) share the same data source.

## What is an API?
An API (Application Programming Interface) is a defined way for one application to
request information or perform an operation in another application. Here, Angular
is the caller and the ASP.NET Core Web API is the application being called.

## What is REST?
REST is a simple style for designing APIs around resources and URLs. A "student" is
a resource, and we access it with plain HTTP GET requests to predictable URLs like
`/api/students` and `/api/students/1`.

## What is HTTP?
HTTP is the protocol (the agreed set of rules) that browsers and servers use to
exchange requests and responses over a network. Angular's `HttpClient` sends an
HTTP request; ASP.NET Core sends back an HTTP response.

## What is JSON?
JSON (JavaScript Object Notation) is a simple text format for representing data
that both C# and TypeScript can easily read and write, which is why the API
returns JSON instead of a C#-specific format.

## What is ASP.NET Core?
ASP.NET Core is the framework used to build our C# web application/API — it
handles incoming HTTP requests, routes them to the right code, and turns C#
objects into JSON responses.

## What is a controller?
`StudentsController` is the class that receives incoming HTTP requests for
`/api/students...`, decides what data to return, and hands it back to ASP.NET Core
to be serialized as JSON.

## What happens when I open /students?
```
Browser loads /students
       ↓
Angular Students component activates
       ↓
StudentService.getStudents() sends GET http://localhost:5286/api/students
       ↓
StudentsController.GetStudents() reads the in-memory list
       ↓
ASP.NET Core serializes the list to JSON, HTTP 200 OK
       ↓
Angular receives the JSON and updates the page
```

## What happens when I open /students/1?
```
Browser loads /students/1
       ↓
Angular Router extracts the route parameter id = 1
       ↓
StudentDetail component asks StudentService.getStudentById(1)
       ↓
GET http://localhost:5286/api/students/1
       ↓
StudentsController.GetStudentById(1) looks the student up
       ↓
Found  → 200 OK + JSON student
Not found → 404 Not Found
       ↓
Angular shows the profile, or "Student not found."
```

## HTTP status codes used here
- `200 OK` — the request succeeded.
- `404 Not Found` — the requested student does not exist.
- `500 Internal Server Error` — something went wrong on the server (unexpected).

## CORS
The Angular dev server (`http://localhost:4200`) and the API
(`http://localhost:5286`) are different origins. Browsers block cross-origin
requests by default unless the server explicitly allows them — this is CORS
(Cross-Origin Resource Sharing). `Program.cs` adds a CORS policy that allows only
`http://localhost:4200`, not every origin.

## Security note
The API is intentionally open (no authentication) at this stage — authentication
is introduced in a later phase. No passwords or user accounts exist yet.

## What is still missing?
Student data lives in an in-memory C# list (`StudentData.cs`) — it resets every
time the API restarts. Phase 4 replaces this with a real SQL database:
```
Angular
   ↓
C# API
   ↓
SQL Database
```
