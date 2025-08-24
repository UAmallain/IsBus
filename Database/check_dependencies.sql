-- =============================================
-- Check Dependencies on Old Tables
-- Find all views, functions, procedures that reference old tables
-- =============================================

USE bor_db;

-- =============================================
-- Check Views that reference old tables
-- =============================================
SELECT 'Views Referencing Old Tables' AS Report;

SELECT 
    TABLE_NAME AS view_name,
    VIEW_DEFINITION
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA = 'bor_db'
    AND (
        VIEW_DEFINITION LIKE '%`names`%'
        OR VIEW_DEFINITION LIKE '%`words`%'
        OR VIEW_DEFINITION LIKE '% names %'
        OR VIEW_DEFINITION LIKE '% words %'
    );

-- =============================================
-- Check Functions that reference old tables
-- =============================================
SELECT 'Functions Referencing Old Tables' AS Report;

SELECT 
    ROUTINE_NAME AS function_name,
    ROUTINE_TYPE,
    CREATED,
    LAST_ALTERED
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'bor_db'
    AND ROUTINE_TYPE = 'FUNCTION'
    AND (
        ROUTINE_DEFINITION LIKE '%names%'
        OR ROUTINE_DEFINITION LIKE '%words%'
    );

-- =============================================
-- Check Procedures that reference old tables
-- =============================================
SELECT 'Procedures Referencing Old Tables' AS Report;

SELECT 
    ROUTINE_NAME AS procedure_name,
    ROUTINE_TYPE,
    CREATED,
    LAST_ALTERED
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'bor_db'
    AND ROUTINE_TYPE = 'PROCEDURE'
    AND (
        ROUTINE_DEFINITION LIKE '%names%'
        OR ROUTINE_DEFINITION LIKE '%words%'
    );

-- =============================================
-- Check Triggers that reference old tables
-- =============================================
SELECT 'Triggers on Old Tables' AS Report;

SELECT 
    TRIGGER_NAME,
    EVENT_MANIPULATION,
    EVENT_OBJECT_TABLE,
    ACTION_TIMING
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = 'bor_db'
    AND EVENT_OBJECT_TABLE IN ('names', 'words');

-- =============================================
-- List all database objects for review
-- =============================================
SELECT 'All Database Objects' AS Report;

-- Tables
SELECT 'TABLES' AS object_type, COUNT(*) AS count
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = 'bor_db' AND TABLE_TYPE = 'BASE TABLE'
UNION ALL
-- Views
SELECT 'VIEWS' AS object_type, COUNT(*) AS count
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA = 'bor_db'
UNION ALL
-- Functions
SELECT 'FUNCTIONS' AS object_type, COUNT(*) AS count
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'bor_db' AND ROUTINE_TYPE = 'FUNCTION'
UNION ALL
-- Procedures
SELECT 'PROCEDURES' AS object_type, COUNT(*) AS count
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'bor_db' AND ROUTINE_TYPE = 'PROCEDURE'
UNION ALL
-- Triggers
SELECT 'TRIGGERS' AS object_type, COUNT(*) AS count
FROM information_schema.TRIGGERS
WHERE TRIGGER_SCHEMA = 'bor_db';

-- =============================================
-- Generate DROP statements for old views/functions
-- =============================================
SELECT 'DROP Statements for Old Objects' AS Report;

-- Generate DROP statements for views
SELECT CONCAT('DROP VIEW IF EXISTS ', TABLE_NAME, ';') AS drop_statement
FROM information_schema.VIEWS
WHERE TABLE_SCHEMA = 'bor_db'
    AND (
        VIEW_DEFINITION LIKE '%`names`%'
        OR VIEW_DEFINITION LIKE '%`words`%'
    )
UNION ALL
-- Generate DROP statements for functions
SELECT CONCAT('DROP FUNCTION IF EXISTS ', ROUTINE_NAME, ';') AS drop_statement
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = 'bor_db'
    AND ROUTINE_TYPE = 'FUNCTION'
    AND (
        ROUTINE_DEFINITION LIKE '%names%'
        OR ROUTINE_DEFINITION LIKE '%words%'
    );