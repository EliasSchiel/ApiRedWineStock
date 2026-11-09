namespace RedWineStock.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operador = "Operador";

    // Para [Authorize(Roles = Roles.Staff)]: cualquiera de los dos
    public const string Staff = Admin + "," + Operador;

    public const int IdAdmin = 1;
    public const int IdOperador = 2;
}
