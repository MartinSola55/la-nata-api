namespace LaNata_Api.Report.Models.Constants
{
    public static class ReportDimensions
    {
        public const float PageMargin = 30f;

        public const float SpacingXs = 2f;
        public const float SpacingSm = 4f;
        public const float SpacingMd = 8f;
        public const float SpacingLg = 14f;
        public const float SpacingXl = 20f;

        public const float CellPaddingX = 5f;
        public const float CellPaddingY = 5f;

        // Anchos relativos de las columnas que llevan un valor de largo conocido. A tamaño de
        // impresión, "$ 1.234.567,00" o una fecha ya no entran en una unidad y cortarían en dos
        // líneas, así que piden algo más que una columna de texto común
        public const float ColumnWidthMoney = 1.45f;
        public const float ColumnWidthDate = 1.2f;

        public const float BorderThin = 0.5f;
        public const float BorderRegular = 1f;

        // Escala pensada para papel: el reporte se lee impreso, no en pantalla, así que el cuerpo
        // de la tabla (FontSm) no baja de 11. Las columnas son relativas, por lo que un texto largo
        // corta en varias líneas en vez de desbordar la hoja.
        public const float FontXs = 9f;
        public const float FontSm = 11f;
        public const float FontMd = 13f;
        public const float FontLg = 16f;
        public const float FontXl = 21f;

        public const float LogoMaxHeight = 58f;
    }
}
