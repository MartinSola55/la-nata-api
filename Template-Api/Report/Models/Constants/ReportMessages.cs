namespace Template_Api.Report.Models.Constants
{
    public static class ReportMessages
    {
        public static string ColumnsRequired()
        {
            return "Debes definir al menos una columna para el reporte.";
        }

        public static string DocumentRequired()
        {
            return "Debes indicar el documento a generar.";
        }

        public static string NoRows()
        {
            return "No hay datos para incluir en el reporte con los filtros aplicados.";
        }

        public static string TooManyRows(int maxRows)
        {
            return "El reporte supera el máximo de " + maxRows + " filas. Acota los filtros e intenta de nuevo.";
        }

        public static string ReportException()
        {
            return "Ha ocurrido un error al intentar generar el reporte. Por favor, intenta de nuevo.";
        }

        public static string EmptyTable()
        {
            return "Sin datos para mostrar.";
        }

        public static string GeneratedAt()
        {
            return "Generado el";
        }

        public static string Page()
        {
            return "Página";
        }

        public static string Of()
        {
            return "de";
        }

        public static string TotalRecords()
        {
            return "Total de registros";
        }
    }
}
