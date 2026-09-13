-- StudentConnect demo/seed data — matches the original EF Core HasData seed exactly.
-- Apply with: psql -U <user> -d studentconnect_db -f seed.sql
-- Safe to re-run (ON CONFLICT DO NOTHING); only inserts rows that don't already exist by Id.

INSERT INTO "Grades" ("Id", "Name") VALUES
    (1, 'Grade 8'),
    (2, 'Grade 9'),
    (3, 'Grade 10')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "AcademicYears" ("Id", "Name", "StartDate", "EndDate", "IsCurrent") VALUES
    (1, '2026-2027', DATE '2026-06-01', DATE '2027-04-30', true)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Terms" ("Id", "AcademicYearId", "Name", "StartDate", "EndDate") VALUES
    (1, 1, 'Term 1', DATE '2026-06-01', DATE '2026-10-31'),
    (2, 1, 'Term 2', DATE '2026-11-01', DATE '2027-04-30')
ON CONFLICT ("Id") DO NOTHING;

-- Two sections per grade, matching the original demo students' A/B split.
INSERT INTO "Sections" ("Id", "GradeId", "Name") VALUES
    (1, 1, 'A'), (2, 1, 'B'),
    (3, 2, 'A'), (4, 2, 'B'),
    (5, 3, 'A'), (6, 3, 'B')
ON CONFLICT ("Id") DO NOTHING;

-- Migrates an installation that already had students under the old free-text "Section"
-- column (pre-Sections-table) — resolves each student's text value to a real Section row
-- (falling back to the grade's first section if no exact name match), then drops the
-- legacy column. Runs BEFORE the Students insert below, since that insert's column list
-- omits "Section" entirely and would violate its NOT NULL constraint otherwise. A fresh
-- install never has a "Section" column, so this whole block is a no-op there.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Students' AND column_name = 'Section'
    ) THEN
        UPDATE "Students" s
        SET "SectionId" = sec."Id"
        FROM "Sections" sec
        WHERE sec."GradeId" = s."GradeId" AND sec."Name" = s."Section" AND s."SectionId" IS NULL;

        UPDATE "Students" s
        SET "SectionId" = (SELECT "Id" FROM "Sections" WHERE "GradeId" = s."GradeId" ORDER BY "Id" LIMIT 1)
        WHERE s."SectionId" IS NULL;

        ALTER TABLE "Students" DROP COLUMN "Section";
    END IF;
END $$;

INSERT INTO "Students" ("Id", "StudentId", "FirstName", "LastName", "Email", "GradeId", "SectionId") VALUES
    (1, 'STU001', 'Ananya', 'Sharma', 'ananya@example.com', 1, 1),
    (2, 'STU002', 'Rahul', 'Verma', 'rahul@example.com', 1, 1),
    (3, 'STU003', 'Aisha', 'Khan', 'aisha@example.com', 1, 2),
    (4, 'STU004', 'Arjun', 'Nair', 'arjun@example.com', 1, 2),
    (5, 'STU005', 'Priya', 'Iyer', 'priya@example.com', 2, 3),
    (6, 'STU006', 'Vikram', 'Rao', 'vikram@example.com', 2, 3),
    (7, 'STU007', 'Meera', 'Pillai', 'meera@example.com', 2, 4),
    (8, 'STU008', 'Karan', 'Singh', 'karan@example.com', 3, 5),
    (9, 'STU009', 'Divya', 'Menon', 'divya@example.com', 3, 5),
    (10, 'STU010', 'Rohan', 'Gupta', 'rohan@example.com', 3, 6)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Subjects" ("Id", "Name", "Code") VALUES
    (1, 'Mathematics', 'MATH'),
    (2, 'Science', 'SCI'),
    (3, 'English', 'ENG'),
    (4, 'Computer Science', 'CS')
ON CONFLICT ("Id") DO NOTHING;


-- Every student studies all four subjects.
INSERT INTO "StudentSubjects" ("Id", "StudentId", "SubjectId")
SELECT (s.n - 1) * 4 + sub.n, s.n, sub.n
FROM generate_series(1, 10) AS s(n)
CROSS JOIN generate_series(1, 4) AS sub(n)
ON CONFLICT ("Id") DO NOTHING;

-- Mathematics attendance for Grade 8A (students 1-4), 10 days from 2026-09-01.
INSERT INTO "Attendance" ("Id", "StudentId", "SubjectId", "Date", "Status")
SELECT
    (st.n - 1) * 10 + d.n,
    st.n,
    1,
    DATE '2026-09-01' + (d.n - 1),
    CASE WHEN (d.n - 1 + st.n) % 6 = 0 THEN 'Absent' ELSE 'Present' END
FROM generate_series(1, 4) AS st(n)
CROSS JOIN generate_series(1, 10) AS d(n)
ON CONFLICT ("Id") DO NOTHING;

-- Ananya (student 1) also has Science/English/Computer Science attendance.
INSERT INTO "Attendance" ("Id", "StudentId", "SubjectId", "Date", "Status")
SELECT
    40 + (sub.i - 1) * 10 + d.n,
    1,
    sub.subject_id,
    DATE '2026-09-01' + (d.n - 1),
    CASE WHEN (d.n - 1 + sub.subject_id) % 7 = 0 THEN 'Absent' ELSE 'Present' END
FROM (VALUES (1, 2), (2, 3), (3, 4)) AS sub(i, subject_id)
CROSS JOIN generate_series(1, 10) AS d(n)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Assessments" ("Id", "Title", "SubjectId", "Date", "MaximumMarks", "AssessmentType", "Description") VALUES
    (1, 'Unit Test 1', 1, DATE '2026-09-15', 100, 'UnitTest', 'Algebra basics'),
    (2, 'Unit Test 2', 1, DATE '2026-09-22', 100, 'UnitTest', 'Geometry basics'),
    (3, 'Unit Test 1', 2, DATE '2026-09-18', 100, 'UnitTest', 'Physical and chemical changes'),
    (4, 'Grammar Test 1', 3, DATE '2026-09-20', 50, 'Quiz', 'Tenses and parts of speech')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Marks" ("Id", "StudentId", "AssessmentId", "MarksObtained") VALUES
    (1, 1, 1, 87), (2, 2, 1, 72), (3, 3, 1, 94), (4, 4, 1, 81),
    (5, 1, 2, 91), (6, 2, 2, 68), (7, 3, 2, 89), (8, 4, 2, 77),
    (9, 1, 3, 84), (10, 2, 3, 75), (11, 3, 3, 90), (12, 4, 3, 79),
    (13, 1, 4, 46), (14, 2, 4, 38), (15, 3, 4, 48), (16, 4, 4, 41)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Teachers" ("Id", "TeacherId", "FirstName", "LastName", "Email") VALUES
    (1, 'T001', 'Priya', 'Mehta', 'priya.mehta@example.com'),
    (2, 'T002', 'David', 'Thomas', 'david.thomas@example.com'),
    (3, 'T003', 'Kavita', 'Nair', 'kavita.nair@example.com')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Homework" ("Id", "Title", "Description", "SubjectId", "GradeId", "TeacherId", "CreatedDate", "DueDate") VALUES
    (1, 'Algebra Practice', 'Complete questions 1-20 from the Algebra worksheet.', 1, 1, 1, DATE '2026-09-10', DATE '2026-09-25'),
    (2, 'States of Matter', 'Read chapter 3 and answer the review questions.', 2, 1, 2, DATE '2026-09-10', DATE '2026-09-27'),
    (3, 'Grammar Exercise', 'Complete the grammar worksheet on tenses.', 3, 1, 3, DATE '2026-09-10', DATE '2026-09-28')
ON CONFLICT ("Id") DO NOTHING;

-- Aisha (student 3) has no submission for Homework 1, demonstrating "Not Submitted".
INSERT INTO "HomeworkSubmissions" ("Id", "HomeworkId", "StudentId", "SubmittedDate", "Status", "Comments") VALUES
    (1, 1, 1, TIMESTAMPTZ '2026-09-20 14:30:00Z', 'Submitted', 'Completed all 20 questions.'),
    (2, 1, 2, TIMESTAMPTZ '2026-09-21 09:15:00Z', 'Submitted', 'Done, some questions were tricky.'),
    (3, 1, 4, TIMESTAMPTZ '2026-09-24 18:00:00Z', 'Submitted', 'Finished last night.'),
    (4, 2, 1, TIMESTAMPTZ '2026-09-28 10:00:00Z', 'Late', 'Sorry, submitted a day late.')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "Activities" ("Id", "Title", "Description", "Date", "Location", "GradeId", "Capacity", "CreatedDate") VALUES
    (1, 'Science Exhibition', 'Showcase science projects to the school.', TIMESTAMPTZ '2026-10-05 10:00:00Z', 'School Auditorium', 1, 50, TIMESTAMPTZ '2026-09-11 00:00:00Z'),
    (2, 'Coding Workshop', 'Learn the basics of programming through hands-on activities.', TIMESTAMPTZ '2026-10-15 14:00:00Z', 'Computer Lab', 1, 20, TIMESTAMPTZ '2026-09-11 00:00:00Z'),
    (3, 'Inter-School Debate', 'Represent the school in a friendly debate competition.', TIMESTAMPTZ '2026-10-20 11:00:00Z', 'Activity Hall', 2, 30, TIMESTAMPTZ '2026-09-11 00:00:00Z'),
    (4, 'Football Tournament', 'Inter-house football tournament.', TIMESTAMPTZ '2026-10-25 09:00:00Z', 'School Ground', 1, 40, TIMESTAMPTZ '2026-09-11 00:00:00Z'),
    (5, 'Art Competition', 'Show your creativity in a school-wide art competition.', TIMESTAMPTZ '2026-10-18 13:00:00Z', 'Art Room', 3, 25, TIMESTAMPTZ '2026-09-11 00:00:00Z')
ON CONFLICT ("Id") DO NOTHING;

-- Includes an active Cancelled row to show it doesn't count toward capacity.
INSERT INTO "ActivityRegistrations" ("Id", "StudentId", "ActivityId", "RegistrationDate", "Status") VALUES
    (1, 1, 1, TIMESTAMPTZ '2026-09-11 10:00:00Z', 'Registered'),
    (2, 2, 1, TIMESTAMPTZ '2026-09-11 10:05:00Z', 'Registered'),
    (3, 3, 1, TIMESTAMPTZ '2026-09-11 10:10:00Z', 'Registered'),
    (4, 1, 2, TIMESTAMPTZ '2026-09-11 11:00:00Z', 'Registered'),
    (5, 4, 2, TIMESTAMPTZ '2026-09-11 11:05:00Z', 'Registered'),
    (6, 5, 3, TIMESTAMPTZ '2026-09-11 12:00:00Z', 'Registered'),
    (7, 2, 4, TIMESTAMPTZ '2026-09-11 13:00:00Z', 'Registered'),
    (8, 3, 4, TIMESTAMPTZ '2026-09-11 13:05:00Z', 'Cancelled')
ON CONFLICT ("Id") DO NOTHING;

-- Development-only demo accounts (see docs/phase-10.md) — passwords are the literal strings
-- "Student123!", "Teacher123!", "Admin123!", hashed once with ASP.NET Core's PasswordHasher<T>.
-- Never plaintext, never real credentials, never used outside local learning/testing.
INSERT INTO "Users" ("Id", "Username", "PasswordHash", "Role", "StudentId", "TeacherId") VALUES
    (1, 'student1', 'AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==', 'Student', 1, NULL),
    (2, 'student2', 'AQAAAAIAAYagAAAAEM6+LRrGSrEilLctUvvHkNPBPFS6xbNkllKri/okOz1huRxYZhS5pCDOutMjWMe0Lg==', 'Student', 2, NULL),
    (3, 'teacher1', 'AQAAAAIAAYagAAAAEN3zBs3zE/9wQna3A6hG+O0aoOTolBtGKV+URht1R3XF51rb2m6HOMF20KJUMsQd1w==', 'Teacher', NULL, 1),
    (4, 'admin1', 'AQAAAAIAAYagAAAAEMW0cZzVj7vYgZmGyQH8eOYSgADtCkf3lTIBWPwRQtsGAB5ab1k7Pna/nLZokToH8A==', 'Admin', NULL, NULL)
ON CONFLICT ("Id") DO NOTHING;

-- Advance every identity sequence past the highest explicit Id inserted above, so the next
-- app-generated insert doesn't collide with a seeded row.
SELECT setval(pg_get_serial_sequence('"Grades"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Grades"), 1));
SELECT setval(pg_get_serial_sequence('"AcademicYears"', 'Id'), COALESCE((SELECT MAX("Id") FROM "AcademicYears"), 1));
SELECT setval(pg_get_serial_sequence('"Terms"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Terms"), 1));
SELECT setval(pg_get_serial_sequence('"Sections"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Sections"), 1));
SELECT setval(pg_get_serial_sequence('"Students"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Students"), 1));
SELECT setval(pg_get_serial_sequence('"Subjects"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Subjects"), 1));
SELECT setval(pg_get_serial_sequence('"StudentSubjects"', 'Id'), COALESCE((SELECT MAX("Id") FROM "StudentSubjects"), 1));
SELECT setval(pg_get_serial_sequence('"Attendance"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Attendance"), 1));
SELECT setval(pg_get_serial_sequence('"Assessments"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Assessments"), 1));
SELECT setval(pg_get_serial_sequence('"Marks"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Marks"), 1));
SELECT setval(pg_get_serial_sequence('"Teachers"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Teachers"), 1));
SELECT setval(pg_get_serial_sequence('"Homework"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Homework"), 1));
SELECT setval(pg_get_serial_sequence('"HomeworkSubmissions"', 'Id'), COALESCE((SELECT MAX("Id") FROM "HomeworkSubmissions"), 1));
SELECT setval(pg_get_serial_sequence('"Activities"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Activities"), 1));
SELECT setval(pg_get_serial_sequence('"ActivityRegistrations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "ActivityRegistrations"), 1));
SELECT setval(pg_get_serial_sequence('"Users"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Users"), 1));
