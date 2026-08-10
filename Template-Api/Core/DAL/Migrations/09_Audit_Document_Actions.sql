MERGE [audit].[AuditCategory] AS target
USING (VALUES
    ('DOCUMENT', N'Documentos', 0)
) AS source ([CategoryCode], [CategoryName], [IsInternal])
ON target.[CategoryCode] = source.[CategoryCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([CategoryCode], [CategoryName], [IsInternal])
    VALUES (source.[CategoryCode], source.[CategoryName], source.[IsInternal]);

MERGE [audit].[AuditAction] AS target
USING (VALUES
    ('EXPORTED', N'Exportación de datos', 'DOCUMENT', 0),
    ('REPORT_GENERATED', N'Generación de reporte', 'DOCUMENT', 0)
) AS source ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
ON target.[ActionCode] = source.[ActionCode]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([ActionCode], [ActionName], [CategoryCode], [IsInternal])
    VALUES (source.[ActionCode], source.[ActionName], source.[CategoryCode], source.[IsInternal]);
