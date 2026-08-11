namespace LaNata_Api.Export.Models.Constants
{
    public static class ExportMessages
    {
        public static string ColumnsRequired()
        {
            return "Debes definir al menos una columna para exportar.";
        }

        public static string SheetsRequired()
        {
            return "Debes definir al menos una hoja para exportar.";
        }

        public static string NoRows()
        {
            return "No hay datos para exportar con los filtros aplicados.";
        }

        public static string ExportException()
        {
            return "Ha ocurrido un error al intentar generar el archivo. Por favor, intenta de nuevo.";
        }

        public static string TooManyRows(int maxRows)
        {
            return "La exportación supera el máximo de " + maxRows + " filas. Acota los filtros e intenta de nuevo.";
        }
    }
}
