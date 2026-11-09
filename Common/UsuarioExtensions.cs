using System.Security.Claims;

namespace RedWineStock.Common;

public static class UsuarioExtensions
{
    /// <summary>Id del usuario logueado (claim NameIdentifier del token). Solo usar en endpoints con [Authorize].</summary>
    public static int IdUsuario(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static bool EsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
