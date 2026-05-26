SET NOCOUNT ON;
SELECT TOP 3
  Operation,
  CASE WHEN NewValuesJson LIKE '%"PasswordHash":"***"%' THEN 'REDACTED'
       WHEN NewValuesJson LIKE '%PasswordHash%' THEN 'LEAKED'
       ELSE 'absent' END AS PasswordHashStatus,
  CASE WHEN NewValuesJson LIKE '%"SecurityStamp":"***"%' THEN 'REDACTED'
       WHEN NewValuesJson LIKE '%SecurityStamp%' THEN 'LEAKED'
       ELSE 'absent' END AS SecurityStampStatus,
  CASE WHEN NewValuesJson LIKE '%"ConcurrencyStamp":"***"%' THEN 'REDACTED'
       WHEN NewValuesJson LIKE '%ConcurrencyStamp%' THEN 'LEAKED'
       ELSE 'absent' END AS ConcurrencyStampStatus
FROM [identity].[RowAudits]
WHERE TableName='AspNetUsers'
  AND OccurredAt > DATEADD(MINUTE,-15,SYSUTCDATETIME())
ORDER BY OccurredAt DESC;
