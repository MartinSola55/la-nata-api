namespace LaNata_Api.Core.Models.Constants
{
    public class Messages
    {
        public class Error
        {
            public static string Exception()
            {
                return "Ha ocurrido un error inesperado. Por favor, intenta de nuevo.";
            }

            public static string TooManyRequests()
            {
                return "Demasiadas solicitudes. Por favor, espera unos minutos e intenta de nuevo.";
            }

            public static string Unauthorized()
            {
                return "No tienes permisos para realizar esta operación.";
            }

            public static string EntityNotFound(string entityName, bool femine = false)
            {
                return entityName + " no encontrad" + (femine ? "a" : "o") + ".";
            }

            public static string FieldsRequired()
            {
                return "Debes ingresar todos los campos obligatorios.";
            }

            public static string FieldsRequired(string[] fields)
            {
                return "Debes ingresar todos los campos obligatorios: " + string.Join(", ", fields);
            }

            public static string FieldRequired(string fieldName)
            {
                return "El campo " + fieldName + " es requerido.";
            }

            public static string FieldGraterThanZero(string fieldName)
            {
                return "El campo " + fieldName + " debe ser mayor a cero.";
            }

            public static string FieldTooLong(string fieldName, int maxLength)
            {
                return "El campo " + fieldName + " debe tener menos de " + maxLength + " caracteres.";
            }

            public static string InvalidField(string fieldName)
            {
                return "El campo " + fieldName + " no es válido.";
            }

            public static string InvalidDateRange()
            {
                return "El rango de fechas seleccionado no es válido.";
            }

            public static string UniqueField(string entityName, string fieldName, bool femineField = false, bool femine = false)
            {
                return "Ya existe un" + (femine ? "a" : "") + " " + entityName + " con es" + (femineField ? "a" : "e") + " " + fieldName + ".";
            }

            public static string ProductWithoutStock()
            {
                return "El producto no cuenta con suficiente stock.";
            }

            public static string OrderWithoutProducts()
            {
                return "No hay ningún producto cargado en el pedido.";
            }

            public static string InvalidPassword()
            {
                return "La contraseña debe contener al menos 8 caracteres, una letra mayúscula, una letra minúscula y un número.";
            }

            public static string InvalidEmail()
            {
                return "El email ingresado no es válido.";
            }

            public static string DuplicateEmail()
            {
                return "El email ingresado ya se encuentra registrado.";
            }

            public static string InvalidLogin()
            {
                return "Email y/o contraseña inválidos.";
            }

            public static string ExpiredToken()
            {
                return "El token ha expirado. Por favor, inicia sesión nuevamente.";
            }

            public static string TokenCreation()
            {
                return "Ha ocurrido un error al intentar crear el token. Por favor, intenta de nuevo.";
            }

            public static string UserWithoutRole()
            {
                return "El usuario con el que intentas acceder no posee un rol.";
            }
        }

        public class CRUD
        {
            public static string EntityCreated(string entityName, bool femine = false)
            {
                return entityName + " cread" + (femine ? "a" : "o") + " correctamente.";
            }

            public static string EntityUpdated(string entityName, bool femine = false)
            {
                return entityName + " editad" + (femine ? "a" : "o") + " correctamente.";
            }

            public static string EntityDeleted(string entityName, bool femine = false)
            {
                return entityName + " eliminad" + (femine ? "a" : "o") + " correctamente.";
            }
        }

    }
}
