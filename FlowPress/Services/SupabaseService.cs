using System.Security.Claims;
using FlowPress.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Supabase;
using Client = Supabase.Client;
using Supabase.Gotrue;
using Supabase.Postgrest.Models;

namespace FlowPress.Services
{
    public class SupabaseService
    {
        private readonly Client _client;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private bool _initialized = false;
        
        // INFO
        /// Cadena de conexión a la base de datos los datos de conexion están en el archivo appsettings.Development.json
        public SupabaseService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            var url = configuration["Supabase:Url"];
            var key = configuration["Supabase:Key"];
            _client = new Client(url!, key, new SupabaseOptions { AutoConnectRealtime = true });
        }

        // INFO
        /// Con este metodo nos aseguraremos de que el cliente Supabase este iniciado de manera correcta
        private async Task EnsureInitializedAsync()
        {
            if (!_initialized)
            {
                await _client.InitializeAsync();
                _initialized = true;
            }
        }

        #region ---------- AUTENTICACIÓN ----------
        // INFO
        /// Metodo el cual se llamara en la pantalla de Login/Inicio de Sesión
        public async Task<bool> SignInAsync(string email, string password, HttpContext httpContext)
        {
            await EnsureInitializedAsync();
            try
            {
                // Iniciar sesión en Supabase
                var session = await _client.Auth.SignInWithPassword(email, password);
                if (session?.User == null)
                    return false;
                
                // Hacemos una llamada al metodo para obtener el nombre del usuario
                var username = await SelectUsernameAsync(session);
                
                // Crear los claims (información que se guarda en la cookie)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, session.User.Email ?? email),
                    new Claim("username", username),
                    new Claim("userid", session.User.Id ?? string.Empty)
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                // Crear la cookie de autenticación
                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    claimsPrincipal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                    });

                return true;
            }
            catch (Exception ex)
            {
                // Si algo falla en el Login, imprimira por consola la excepción/error que devuelva 
                // el login fallido
                Console.WriteLine($"❌ Error en SignInAsync: {ex.Message}");
                return false;
            }
        }
        // INFO
        /// Metodo el cual se llamara en la pantalla de Register
        public async Task<Session?> SignUpAsync(string email, string password)
        {
            await EnsureInitializedAsync();
            var session = await _client.Auth.SignUp(email, password);
            return session;
        }
        // INFO 
        /// Metodo el cual se llamara en el botón de Sign Out/Cerrar Sesión
        public async Task SignOutAsync(HttpContext httpContext)
        {
            await EnsureInitializedAsync();
            await _client.Auth.SignOut();

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        #endregion

        #region ---------- INSERCIÓN EN TABLAS CUSTOM ----------
        // INFO
        /// Este metodo nos permitira hacer inserts en la tabla que nosotros deseemos pasandole como variables el modelo que nosotros queramos
        public async Task InsertAsync<T>(T model) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            await _client.From<T>().Insert(model);
        }
        #endregion
        
        #region ----------- SELECT TABLAS CUSTOM ----------
        // INFO
        /// Devuelve el nombre del usuario autenticado.
        private async Task<string> SelectUsernameAsync(Session? session)
        {
            if (session?.User?.Id == null)
                return "Usuario";

            // Obtener el username desde la tabla UsersInfo
            var result = await _client
                .From<UsersInfoModel>()
                .Where(p => p.id == session.User.Id)
                .Get();
            
            // Almacena los datos de la query, y coge el primer valor que encuentra para guardarlos en una 
            // variable, la cual almacenara los datos del modelo (ID y Username) 
            var user = result.Models.FirstOrDefault();
            
            // En esta variable, guardaremos el resultado del campo Username que hemos guardado en la variable
            // anterior para luego poder mostrarlo donde sea necesario (en la NavMenu)
            var username = user?.username ?? "Usuario";
            return username;
        }
        
        // INFO
        /// Devuelve las instancias asociadas al usuario autenticado.
        public async Task<List<InstancesModel>> SelectInstancesAsync()
        {
            await EnsureInitializedAsync();

            // Hacemos una llamada al HttpContext para que nos devuelva
            // el ID del usuario autenticado, que posteriormente necesitaremos
            // para poder hacer el filtro del select y que solo nos muestre 
            // las instancias que tiene disponible ese usuario
            var userId = _httpContextAccessor.HttpContext?.User.FindFirst("userid")?.Value;
            
            // Obtener las instancias desde la tabla Instances
            var result = await _client
                .From<InstancesModel>()
                .Where(p => p.IdUser == userId)
                .Where(p => p.EliminatedAt == null)
                .Get();
            
            // Almacena los datos de la query, y coge el primer valor que encuentra para guardarlos en la variable
            var instances = result.Models;
            
            return instances;
        }
        
        // INFO
        /// Devuelve los registros/datos de la instancia seleccionada.
        public async Task<InstancesModel?> SelectInstanceByIdAsync(Guid id)
        {
            await EnsureInitializedAsync();

            // Obtener los registros de la instancia
            var result = await _client
                .From<InstancesModel>()
                .Select("*")
                .Where(p => p.Id == id)
                .Single();

            return result;
        }

        // INFO
        /// Devuelve una instancia solo si pertenece al usuario autenticado.
        public async Task<InstancesModel?> SelectOwnedInstanceByIdAsync(Guid id)
        {
            await EnsureInitializedAsync();

            var userId = _httpContextAccessor.HttpContext?.User.FindFirst("userid")?.Value;
            if (string.IsNullOrEmpty(userId))
                return null;

            var result = await _client
                .From<InstancesModel>()
                .Where(p => p.Id == id)
                .Where(p => p.IdUser == userId)
                .Where(p => p.EliminatedAt == null)
                .Get();

            return result.Models.FirstOrDefault();
        }
        
        // INFO
        /// Si hay un registro que contenga el mismo siteaddress que el enviado,
        /// lo devolvera para a continuación hacer una comprobación
        public async Task<InstancesModel?> SelectInstancesSiteAddressAsync(string siteAddressToCheck)
        {
            await EnsureInitializedAsync();

            // Obtener los registros de la instancia
            var result = await _client
                .From<InstancesModel>()
                .Where(i => i.SiteAddress == siteAddressToCheck)
                .Get();
        
            return result.Models.FirstOrDefault();
        }
        
        #endregion

        #region ----------- UPDATE REGISTROS ----------
        
        // INFO
        /// Marca el campo de eliminado con la hora y la fecha actual
        /// del registro de la instancia seleccionada.
        public async Task<bool> DeleteInstanceByIdAsync(Guid id)
        {
            try
            {
                await EnsureInitializedAsync();

                var response = await _client
                    .From<InstancesModel>()
                    .Where(x => x.Id == id)
                    .Set(x => x.EliminatedAt!, DateTime.UtcNow)
                    .Set(x => x.DockerStatus, "deleting")
                    .Update();

                return response != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
        
        // INFO
        /// Establece el estado de la instancia
        public async Task<bool> InstanceStatusAsync(Guid id, string status)
        {
            try
            {
                await EnsureInitializedAsync();

                await _client
                    .From<InstancesModel>()
                    .Where(x => x.Id == id)
                    .Set(x => x.DockerStatus, status)
                    .Update();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<bool> UpdateProvisioningStatusAsync(Guid id, string provisioningStatus)
        {
            try
            {
                await EnsureInitializedAsync();

                await _client
                    .From<InstancesModel>()
                    .Where(x => x.Id == id)
                    .Set(x => x.ProvisioningStatus, provisioningStatus)
                    .Update();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
        
        #endregion
    }
}
