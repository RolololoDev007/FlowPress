# FlowPress

Plataforma web para **gestionar, desplegar y operar instancias WordPress** desde un panel centralizado.

FlowPress está construida con **ASP.NET Core Razor Pages** y se apoya en **Supabase** para autenticación y persistencia de datos. Su objetivo es simplificar la operación de múltiples sitios WordPress (alta, estado, arranque, parada, reinicio y eliminación lógica).

## Finalidad del proyecto

FlowPress nace para resolver un problema operativo: administrar varios WordPress de forma rápida sin tener que entrar servidor por servidor.

Con FlowPress puedes:
- Registrar y autenticar usuarios.
- Crear instancias WordPress asociadas a cada usuario.
- Listar todas tus instancias desde un panel único.
- Ver estado de contenedor y métricas básicas de salud.
- Ejecutar acciones operativas sobre cada instancia: iniciar, apagar, reiniciar y eliminar.

## Tecnologías

- **.NET 9**
- **ASP.NET Core Razor Pages**
- **Autenticación por cookies**
- **Supabase (Auth + tablas Postgres)**
- **HTML/CSS/JS**

## Estructura principal

```text
FlowPress/
├── Program.cs
├── Services/
│   └── SupabaseService.cs
├── Pages/
│   ├── Index.cshtml
│   ├── Authentication/
│   │   ├── LoginPage.cshtml
│   │   └── RegisterPage.cshtml
│   └── Instances/
│       ├── WordPressInstances.cshtml
│       ├── WordPressCreateInstance.cshtml
│       └── WordPressInstanceInfo.cshtml
└── wwwroot/
```

## Funcionamiento (flujo)

1. El usuario entra a FlowPress.
2. Se registra o inicia sesión.
3. FlowPress crea una cookie de autenticación.
4. El usuario entra al panel de instancias.
5. Puede crear nuevas instancias WordPress.
6. Desde la vista de detalle puede monitorizar y ejecutar acciones operativas.

## Ventanas del sistema y capturas

> Guarda las imágenes en `docs/screenshots/` con estos nombres para que se vean automáticamente en GitHub.

### 1) Inicio (`/`)
Pantalla de bienvenida con resumen de capacidades y accesos rápidos a gestión y creación de instancias.

![Pantalla de inicio](docs/screenshots/01-inicio.png)

### 2) Login (`/login`)
Formulario de acceso por correo y contraseña.

![Pantalla de login](docs/screenshots/02-login.png)

### 3) Registro (`/register`)
Formulario de alta de usuario con validaciones y medidor de fortaleza de contraseña.

![Pantalla de registro](docs/screenshots/03-registro.png)

### 4) Listado de instancias (`/instances`)
Tabla principal con dominio, estado y acciones por instancia.

![Listado de instancias](docs/screenshots/04-instancias.png)

### 5) Crear instancia (`/create`)
Formulario para lanzar una nueva instancia WordPress (nombre + subdominio).

![Crear instancia](docs/screenshots/05-crear-instancia.png)

### 6) Detalle de instancia (`/instanceinfo/{id}`)
Vista operativa con:
- Estado de contenedor.
- Resumen de monitoreo y salud.
- Acciones de iniciar/apagar/reiniciar.
- Zona de peligro para eliminación.

![Detalle de instancia](docs/screenshots/06-detalle-instancia.png)

## Configuración y ejecución local

### Requisitos

- SDK **.NET 9**
- Proyecto Supabase accesible

### Variables/configuración

El proyecto usa claves en `appsettings.json` y `appsettings.Development.json` dentro de la sección `Supabase`.

Ejemplo:

```json
"Supabase": {
  "Url": "http://tu-supabase:8000",
  "Key": "tu-service-role-o-anon-key"
}
```

### Ejecutar

```bash
cd FlowPress
dotnet restore
dotnet run
```

URL por defecto (según `launchSettings.json`):
- `http://localhost:5266`
- `https://localhost:7071`

## Estado actual del proyecto

FlowPress ya cubre el flujo base de autenticación y gestión operativa de instancias. Como evolución natural, el siguiente paso sería reforzar observabilidad, trazabilidad de acciones y automatización de despliegue de extremo a extremo.

## Autoría

Proyecto FlowPress.
