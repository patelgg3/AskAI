USE [DataExposed]
GO

/****** Object:  StoredProcedure [dbo].[VectorUpdate]    Script Date: 7/27/2026 7:13:51 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[VectorUpdate]
AS
BEGIN

SET NOCOUNT ON;

DECLARE @Object INT;
DECLARE @URL VARCHAR(8000) = 'http://localhost/AskAI/VectorUpdateAuto.aspx';

EXEC sp_OACreate 'MSXML2.ServerXMLHTTP', @Object OUT;
EXEC sp_OAMethod @Object, 'open', NULL, 'GET', @URL, 'false';
EXEC sp_OAMethod @Object, 'send', NULL;
EXEC sp_OADestroy @Object;

END;

GO


