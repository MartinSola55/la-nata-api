namespace LaNata_Api.Audit.Models.Constants
{
    public static class AuditMessages
    {
        public static string EmptyValue()
        {
            return "(vacío)";
        }

        public static string FieldUpdated(string fieldName, string previousValue, string updatedValue)
        {
            return "Se modificó " + fieldName + " de «" + previousValue + "» a «" + updatedValue + "».";
        }

        public static string FieldSet(string fieldName, string value)
        {
            return "Se estableció " + fieldName + " en «" + value + "».";
        }

        public static string FieldCleared(string fieldName, string previousValue)
        {
            return "Se limpió " + fieldName + ", que antes era «" + previousValue + "».";
        }

        public static string ItemAdded(string collectionName, string itemName)
        {
            return "Se agregó «" + itemName + "» a " + collectionName + ".";
        }

        public static string ItemRemoved(string collectionName, string itemName)
        {
            return "Se quitó «" + itemName + "» de " + collectionName + ".";
        }

        public static string ActionRequired()
        {
            return "Debes indicar la acción de auditoría.";
        }

        public static string EntityRequired()
        {
            return "Debes indicar la entidad auditada.";
        }

        public static string UnknownAction(string actionCode)
        {
            return "La acción de auditoría \"" + actionCode + "\" no se encuentra definida.";
        }

        public static string AuditException()
        {
            return "Ha ocurrido un error al intentar registrar la auditoría.";
        }

        public static string EntryNotFound()
        {
            return "El registro de auditoría no fue encontrado.";
        }
    }
}
