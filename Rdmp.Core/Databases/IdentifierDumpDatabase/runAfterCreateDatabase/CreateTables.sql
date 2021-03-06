/****** Object:  UserDefinedTableType [dbo].[ColumnInfo]    Script Date: 07/09/2015 14:18:03 ******/
CREATE TYPE [dbo].[ColumnInfo] AS TABLE(
	[RuntimeName] [varchar](500) NOT NULL,
	[DataType] [varchar](100) NOT NULL,
	PRIMARY KEY CLUSTERED 
(
	[RuntimeName] ASC,
	[DataType] ASC
)WITH (IGNORE_DUP_KEY = OFF)
)
GO

CREATE PROCEDURE [dbo].[sp_createIdentifierDump]
	-- Add the parameters for the stored procedure here
	@liveTableName varchar(1000),
	@primaryKeys ColumnInfo READONLY, 
	@dumpIdentifiers ColumnInfo READONLY

AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	--make *table_name*_Identifiers - see SELECT * FROM [aSMR01_Identifiers]
	
	---------------------------------
	--get PK fields.
	---------------------------------
	/*
	--testing
	DECLARE @primaryKeys ColumnInfo, @liveTableName VARCHAR(500)
	SET @liveTableName = 'TestingTable'
	INSERT INTO @primaryKeys SELECT 'test', 'int', 0
	INSERT INTO @primaryKeys SELECT 'test2', 'varchar(60)', 0
	INSERT INTO @primaryKeys SELECT 'test3', 'varchar(60)', 0
	INSERT INTO @primaryKeys SELECT 'test4', 'varchar(60)', 0
	INSERT INTO @primaryKeys SELECT 'test5', 'varchar(60)', 0
	--SELECT * FROM @primaryKeys
	--testing
	*/

	DECLARE @fieldName VARCHAR(500), @dataType VARCHAR(100), @tableName VARCHAR(100), @addComma BIT
	DECLARE @sqlCreateTable VARCHAR(MAX), @sqlCreatePKConstraint VARCHAR(MAX)
	
	SET @tableName = 'ID_'+@liveTableName
	
	--create table syntax ready for the field list
	SET @sqlCreateTable = N' IF OBJECT_ID('''+@tableName+''') IS NULL 	
									CREATE TABLE '+@tableName+' ('
	
	--create index syntax ready for the field list
	SET @sqlCreatePKConstraint = N'ALTER TABLE '+@tableName+' ADD CONSTRAINT PK_'+@tableName+' PRIMARY KEY NONCLUSTERED ('

	DECLARE pkFieldCursor CURSOR FOR SELECT RuntimeName, DataType FROM @primaryKeys
	OPEN pkFieldCursor
	FETCH NEXT FROM pkFieldCursor INTO @fieldName, @dataType

	WHILE @@FETCH_STATUS = 0
	BEGIN
		--Column list
		SET @sqlCreateTable = @sqlCreateTable+' '+@fieldName+' '+@dataType+' NOT NULL,'
		
		--PK constraint
		--	EPISODE_RECORD_KEY ASC,	SENDING_LOCATION ASC
		SET @sqlCreatePKConstraint = @sqlCreatePKConstraint+' '+@fieldName+' ASC,'
		
		FETCH NEXT FROM pkFieldCursor INTO @fieldName, @dataType
	END

	CLOSE pkFieldCursor

	--get rid of extra commas on the end
	IF RIGHT(@sqlCreateTable, 1) = ','
		SET @sqlCreateTable = LEFT(@sqlCreateTable, LEN(@sqlCreateTable)-1)

	SET @sqlCreateTable = @sqlCreateTable + '	);'

	IF RIGHT(@sqlCreatePKConstraint, 1) = ','
		SET @sqlCreatePKConstraint = LEFT(@sqlCreatePKConstraint, LEN(@sqlCreatePKConstraint)-1)

	SET @sqlCreatePKConstraint = @sqlCreatePKConstraint + ' );'

	/*
	--testing
	PRINT @sqlCreateTable
	PRINT @sqlCreatePKConstraint
	--testing
	*/

	EXEC(@sqlCreateTable)
	EXEC(@sqlCreatePKConstraint)

	--get other fields.
	DECLARE @sqlOtherFields VARCHAR(MAX)

	SET @sqlOtherFields = N' '

	DECLARE fieldCursor CURSOR FOR SELECT RuntimeName, DataType FROM @dumpIdentifiers
	OPEN fieldCursor
	FETCH NEXT FROM fieldCursor INTO @fieldName, @dataType

	WHILE @@FETCH_STATUS = 0
	BEGIN
		--Column list
		SET @sqlOtherFields = 'ALTER TABLE '+@tableName+' ADD '+ @fieldName +' '+ @dataType
		
		EXEC(@sqlOtherFields)

		FETCH NEXT FROM fieldCursor INTO @fieldName, @dataType
	END

	CLOSE fieldCursor

SET NOCOUNT OFF;

END



GO
