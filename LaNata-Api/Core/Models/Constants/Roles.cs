using System.Collections.Generic;
using System.Linq;

namespace LaNata_Api.Core.Models.Constants
{
    // These are the rows already sitting in the legacy [Rol] table (id 1 and 2). They are the values
    // that travel in the JWT role claim, so they must match the column text exactly.
    public static class Roles
    {
        public const string Admin = "Administrador";
        public const string Employee = "Empleado";

        public static bool Validate(string role)
        {
            return !string.IsNullOrEmpty(role) && GetRoles().Any(x => x == role);
        }

        public static List<string> GetRoles()
        {
            return [Admin, Employee];
        }
    }
}
