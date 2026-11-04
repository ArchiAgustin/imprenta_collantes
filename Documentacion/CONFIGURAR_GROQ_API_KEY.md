# Configurar la API key de Groq (desarrollo local)

> **Groq** es el proveedor de IA que usa esta aplicación; **Grok** es otro producto. Esta guía configura `Groq:ApiKey` sin añadir la clave al repositorio.

## 1. Obtener la clave

Entra a [Groq Console](https://console.groq.com/keys) con tu cuenta, crea una API key y cópiala. No la envíes por chat ni la incluyas en capturas de pantalla, commits, `appsettings.json`, `launchSettings.json` o archivos del proyecto.

## 2. Guardarla en este equipo

Abre **PowerShell** en la carpeta raíz del repositorio (donde está `ProyectoFinal.sln`). El proyecto ya contiene un `UserSecretsId`; no necesitas ejecutar `dotnet user-secrets init`. Ejecuta este bloque y pega la clave solo cuando aparezca el prompt (la entrada no se muestra):

```powershell
$clave = Read-Host 'Pega tu API key de Groq' -AsSecureString
try {
    $texto = [System.Net.NetworkCredential]::new('', $clave).Password
    @{ 'Groq:ApiKey' = $texto } | ConvertTo-Json -Compress | dotnet user-secrets set --project .\ProyectoFinal\ProyectoFinal.csproj
} finally {
    Remove-Variable clave,texto -ErrorAction SilentlyContinue
}
```

El comando recibe JSON por la entrada estándar; **no escribas la clave como argumento del comando**, para evitar que quede en el historial de PowerShell. `user-secrets` almacena el valor **fuera del repositorio**, asociado a este proyecto y a tu usuario de Windows. No es un almacén cifrado: protege el acceso a tu cuenta de Windows y utiliza un gestor de secretos apropiado para producción.

Si aparece un error del SDK, comprueba que tengas instalado **.NET SDK 8** (`dotnet --version`) y que abriste PowerShell en la carpeta correcta. Si Groq rechaza la clave, vuelve a guardarla con este procedimiento; no pegues el valor en una incidencia ni en el chat. Evita `dotnet user-secrets list` si compartes pantalla o registras la terminal: muestra las claves **y sus valores**.

## 3. Reiniciar y verificar el flujo

Detén la instancia anterior de la aplicación (incluido Visual Studio si la ejecuta), vuelve a iniciarla en **Development**:

```powershell
dotnet run --project .\ProyectoFinal\ProyectoFinal.csproj --launch-profile http
```

Abre `http://localhost:5092`, inicia sesión con una cuenta que tenga permiso de **crear cotizaciones** y ve a **Nueva cotización → Preparar desde solicitud con IA**. Usa un ejemplo sin datos personales, como «Necesito 100 volantes A5 a color para el viernes». Revisa y aprueba el **texto exacto** que se enviará. Tras analizarlo, selecciona cliente y productos, carga el formulario, revisa cantidad, costo/margen y precio antes de guardar. La prueba puede consumir créditos de tu cuenta de Groq.

Si no hay clave, Groq no responde, tarda más de cinco segundos o devuelve datos inválidos, la aplicación ofrece el formulario manual; **ninguna sugerencia se guarda ni se aplica como precio automáticamente**. La revisión humana es obligatoria: la anonimización automática no garantiza detectar todos los nombres y direcciones.

## Despliegue (fuera del equipo de desarrollo)

`user-secrets` se carga en **Development**, no es una solución de producción. Configura la variable de entorno `Groq__ApiKey` desde un gestor de secretos del servidor o la plataforma de despliegue, restringe el acceso y rota la clave si se expone. El modelo predeterminado es `openai/gpt-oss-20b`; cambiar `Groq:Model` requiere un modelo compatible con JSON Schema estricto. Nunca confirmes secretos en Git.
