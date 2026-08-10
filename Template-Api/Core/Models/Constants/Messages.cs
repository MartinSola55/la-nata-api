namespace Template_Api.Core.Models.Constants
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

            public static string EmailException()
            {
                return "Ha ocurrido un error al intentar enviar el email. Por favor, intenta de nuevo.";
            }

            public static string WhatsAppException()
            {
                return "Ha ocurrido un error al intentar enviar el mensaje de WhatsApp. Por favor, intenta de nuevo.";
            }

            public static string WhatsAppNotConfigured()
            {
                return "El servicio de WhatsApp no se encuentra configurado.";
            }

            public static string MercadoPagoException()
            {
                return "Ha ocurrido un error al intentar procesar el pago con Mercado Pago. Por favor, intenta de nuevo.";
            }

            public static string MercadoPagoNotConfigured()
            {
                return "El servicio de Mercado Pago no se encuentra configurado.";
            }

            public static string AwsS3Exception()
            {
                return "Ha ocurrido un error al intentar procesar el archivo en el almacenamiento. Por favor, intenta de nuevo.";
            }

            public static string AwsS3NotConfigured()
            {
                return "El servicio de almacenamiento de archivos no se encuentra configurado.";
            }

            public static string InvalidPaymentNotification()
            {
                return "La notificación de pago no es válida.";
            }

            public static string ProductWithoutStock()
            {
                return "No hay stock suficiente para el producto seleccionado.";
            }

            public static string Unauthorized()
            {
                return "No tienes permisos para realizar esta operación.";
            }

            public static string EntityNotFound(string entityName, bool femine = false)
            {
                return entityName + " no encontrad" + (femine ? "a" : "o") + ".";
            }

            public static string EmailTemplateNotFound()
            {
                return "La plantilla de email no pudo ser encontrada.";
            }

            public static string FileTooLarge()
            {
                return "El archivo que intentas subir es demasiado grande. El tamaño máximo permitido es de 15 MB.";
            }

            public static string InvalidFileType()
            {
                return "El tipo de archivo que intentas subir no es válido. Los tipos permitidos son PDF, JPEG y PNG.";
            }

            public static string FileNotFound()
            {
                return "El archivo solicitado no pudo ser encontrado.";
            }

            public static string InvalidFileKey()
            {
                return "La referencia del archivo no es válida.";
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

            public static string InvalidField(string fieldName)
            {
                return "El campo " + fieldName + " no es válido.";
            }

            public static string UniqueField(string entityName, string fieldName, bool femineField = false, bool femine = false)
            {
                return "Ya existe un" + (femine ? "a" : "") + " " + entityName + " con es" + (femineField ? "a" : "e") + " " + fieldName + ".";
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

            public static string EntityActivated(string entityName, bool femine = false)
            {
                return entityName + " activad" + (femine ? "a" : "o") + " correctamente.";
            }

            public static string EntityDeactivated(string entityName, bool femine = false)
            {
                return entityName + " desactivad" + (femine ? "a" : "o") + " correctamente.";
            }
        }

        public class MercadoPago
        {
            public static string PreferenceCreated()
            {
                return "Preferencia de pago creada correctamente.";
            }

            public static string NotificationProcessed()
            {
                return "Notificación de pago procesada correctamente.";
            }

            public static string NotificationIgnored()
            {
                return "Notificación de pago ignorada correctamente.";
            }
        }
    }
}
