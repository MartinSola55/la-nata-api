using System;
using System.Globalization;

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

            public static string FieldMax(string fieldName, decimal max)
            {
                var value = max.ToString("#,##0", CultureInfo.InvariantCulture).Replace(",", ".");

                return "El campo " + fieldName + " no puede superar " + value + ".";
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

            public static string CategoryInUse()
            {
                return "No se puede eliminar la categoría porque tiene productos asociados.";
            }

            public static string ProductInUse()
            {
                return "No se puede eliminar el producto porque está reservado en pedidos futuros.";
            }

            public static string ProductStockBelowReserved(DateTime date, int reserved)
            {
                return "No se puede reducir el stock: el " + Day(date) + " hay " + ReservedUnits(reserved) + ".";
            }

            public static string VariantStockBelowReserved(string variantName, DateTime date, int reserved)
            {
                return "No se puede reducir el stock de la variante \"" + variantName + "\": el " + Day(date) + " hay " + ReservedUnits(reserved) + ".";
            }

            public static string VariantWithoutStock(string variantName)
            {
                return "La variante \"" + variantName + "\" no cuenta con suficiente stock.";
            }

            public static string VariantNotFromProduct()
            {
                return "La variante seleccionada no pertenece al producto.";
            }

            public static string VariantInUse(string variantName)
            {
                return "No se puede eliminar la variante \"" + variantName + "\" porque está reservada en pedidos futuros.";
            }

            public static string TooManyVariants(int max)
            {
                return "Un producto no puede tener más de " + max + " variantes.";
            }

            public static string OrderWithoutProducts()
            {
                return "No hay ningún producto cargado en el pedido.";
            }

            public static string OrderAlreadyConfirmed()
            {
                return "El pedido ya está confirmado.";
            }

            public static string OrderWithoutStockToConfirm()
            {
                return "No se puede confirmar el pedido: no hay stock suficiente en la fecha del evento.";
            }

            public static string OrderDraftCannotBePaid()
            {
                return "No se puede cobrar un presupuesto sin confirmar.";
            }

            public static string OrderAlreadyPaid()
            {
                return "El pedido ya figura como cobrado.";
            }

            public static string OrderNotPaid()
            {
                return "El pedido no figura como cobrado.";
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

            private static string Day(DateTime date)
            {
                return date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }

            private static string ReservedUnits(int quantity)
            {
                return quantity + (quantity == 1 ? " unidad reservada" : " unidades reservadas");
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

            public static string EntityConfirmed(string entityName, bool femine = false)
            {
                return entityName + " confirmad" + (femine ? "a" : "o") + " correctamente.";
            }

            public static string EntityPaid(string entityName, bool femine = false)
            {
                return entityName + " marcad" + (femine ? "a" : "o") + " como cobrad" + (femine ? "a" : "o") + ".";
            }

            public static string EntityUnpaid(string entityName, bool femine = false)
            {
                return "Se anuló el cobro de" + (femine ? " la " : "l ") + entityName.ToLower() + ".";
            }
        }

        public class Report
        {
            public static string BudgetValidity(int days, string validUntil)
            {
                return "Este presupuesto tiene una validez de " + days + " días corridos desde su emisión, por lo que vence el "
                    + validUntil + ". Pasada esa fecha los precios pueden variar y debe solicitarse un nuevo presupuesto.";
            }
        }

    }
}
