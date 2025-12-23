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
                var username = await SelectUsername(session);
                
                // Crear los claims (información que se guarda en la cookie)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, session.User.Email),
                    new Claim("username", username),
                    new Claim("userid", session.User.Id)
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
        private async Task<string> SelectUsername(Session? session)
        {
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
        public async Task<List<InstancesModel>> SelectInstances()
        {
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
        public async Task<InstancesModel?> SelectInstanceById(Guid id)
        {
            // Obtener los registros de la instancia
            var result = await _client
                .From<InstancesModel>()
                .Select("*")
                .Where(p => p.Id == id)
                .Single();

            return result;
        }
        
        #endregion

        #region ----------- DELETE REGISTROS ----------
        
        // INFO
        /// Elimina el registro de la instancia seleccionada.
        public async Task<bool> DeleteInstanceById(Guid id)
        {
            try
            {
                await _client
                    .From<InstancesModel>()
                    .Where(x => x.Id == id)
                    .Set(x => x.EliminatedAt, DateTime.Now)
                    .Update();

                return true;
            }
            catch (Exception ex)
            {
                // log ex
                return false;
            }
        }
        
        #endregion
    }
}