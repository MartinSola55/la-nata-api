-- Sets Product.BreakPrice to NULL wherever it holds 0.
--
-- ############################################################################################
-- THIS IS NOT A MIGRATION. It lives outside Core/DAL/Migrations on purpose, so MigrationRunner
-- never picks it up: it updates existing business rows, which CLAUDE.md rules out for anything
-- that runs automatically at startup. Run it by hand, once, with a backup taken first.
-- ############################################################################################
--
-- Why: 0 was never a real break price, it was the legacy app's way of writing "this product has
-- none". The column is nullable and the code already reads it that way — ValidateProduct rejects a
-- BreakPrice of 0 outright, and the web turns 0 into null on the first edit of any product. So the
-- zeros are a legacy encoding that every layer above already disagrees with, and they make the UI
-- print "$ 0,00" as if breakage were free.
--
-- Safe to rerun: the WHERE clause stops matching once the rows are NULL, so a second run reports 0.
--
-- Nothing else is touched. ProductPriceHistory keeps its zeros because it is a log of values that
-- were actually observed at the time, not a set of values being entered.

SET NOCOUNT ON;

-- A negative break price would mean something other than "none" and is not this script's business
IF EXISTS (SELECT 1 FROM [dbo].[Product] WHERE [BreakPrice] < 0)
    THROW 51003, 'Product.BreakPrice contains negative values; review them by hand before running this.', 1;

DECLARE @before INT;
DECLARE @updated INT;
DECLARE @zeros INT;
DECLARE @nulls INT;
DECLARE @real INT;

SELECT @before = COUNT(*) FROM [dbo].[Product] WHERE [BreakPrice] = 0;

PRINT 'Productos con precio por rotura en 0 antes: ' + CAST(@before AS VARCHAR(20));

BEGIN TRANSACTION;

BEGIN TRY
    UPDATE [dbo].[Product]
    SET [BreakPrice] = NULL
    WHERE [BreakPrice] = 0;

    SET @updated = @@ROWCOUNT;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH

SELECT @zeros = COUNT(*) FROM [dbo].[Product] WHERE [BreakPrice] = 0;
SELECT @nulls = COUNT(*) FROM [dbo].[Product] WHERE [BreakPrice] IS NULL;
SELECT @real = COUNT(*) FROM [dbo].[Product] WHERE [BreakPrice] > 0;

PRINT 'Filas actualizadas: ' + CAST(@updated AS VARCHAR(20));
PRINT 'Productos con precio por rotura en 0 despues: ' + CAST(@zeros AS VARCHAR(20));
PRINT 'Productos sin precio por rotura (NULL): ' + CAST(@nulls AS VARCHAR(20));
PRINT 'Productos con precio por rotura real (> 0): ' + CAST(@real AS VARCHAR(20));
