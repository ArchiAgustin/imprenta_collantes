# Configurar el correo con una cuenta personal de Gmail

El sistema envía la cotización en PDF al correo del cliente desde **Administración → Configuración → Parámetros**. Se usa `System.Net.Mail.SmtpClient` con `EnableSsl`, lo que en Gmail significa **STARTTLS en el puerto 587**. El puerto 465 (TLS implícito) no funciona con esta implementación.

> Usa una **cuenta personal de Gmail**, no una cuenta corporativa de Google Workspace. Si el dominio es de Workspace, el administrador puede bloquear las contraseñas de aplicación y el envío SMTP.

## 1. Preparar la cuenta

1. Entra a la cuenta de Gmail que enviará los correos.
2. Activa la **verificación en dos pasos**: <https://myaccount.google.com/signinoptions/two-step-verification>.
   - Sin verificación en dos pasos, Google no permite crear contraseñas de aplicación.
3. Confirma que la cuenta no tenga restricciones de seguridad que bloqueen el inicio de sesión SMTP.

## 2. Crear la contraseña de aplicación

1. Abre <https://myaccount.google.com/apppasswords>.
2. Escribe un nombre reconocible, por ejemplo `Imprenta Collantes`.
3. Pulsa **Crear**.
4. Copia los **16 caracteres** que muestra Google (se pueden escribir sin espacios).
5. Guárdala solo en el formulario del sistema. **No la pegues en el repositorio, en capturas ni en el chat.**

La contraseña de aplicación es distinta de la contraseña normal de la cuenta. Si se pierde, se revoca desde la misma página y se crea otra.

## 3. Configurar Parámetros en el sistema

Inicia sesión como **Admin** y abre **Administración → Configuración → Parámetros**. En la sección de correo completa:

| Campo | Valor |
| --- | --- |
| Servidor SMTP | `smtp.gmail.com` |
| Puerto | `587` |
| Usar TLS | Activado |
| Usuario | La dirección completa, por ejemplo `tucuenta@gmail.com` |
| Correo remitente | La misma dirección de Gmail |
| Nombre remitente | `Imprenta Collantes` (o el nombre comercial) |
| Contraseña | La contraseña de aplicación de 16 caracteres |

Guarda los cambios. La contraseña se cifra con **ASP.NET Core Data Protection** antes de guardarse en `ConfiguracionCorreo`; nunca se devuelve al navegador.

## 4. Probar el envío

1. En la misma pantalla, usa **Enviar prueba**.
2. El sistema envía al **correo del usuario Admin que tiene la sesión abierta**, no necesariamente al remitente SMTP.
3. Revisa la bandeja de entrada y la carpeta de spam.
4. Si el envío falla, revisa el detalle en el mensaje de error y la consola del servidor.

Para una prueba real de cotización, abre una cotización emitida y usa la opción de enviarla al correo del cliente; el PDF se adjunta automáticamente.

## 5. Problemas frecuentes

| Síntoma | Causa probable | Solución |
| --- | --- | --- |
| `535-5.7.8 Username and Password not accepted` | Se usó la contraseña normal o una contraseña de aplicación revocada | Crea una nueva contraseña de aplicación y vuelve a guardarla |
| `534-5.7.9 Application-specific password required` | La cuenta no tiene verificación en dos pasos | Actívala y crea la contraseña de aplicación |
| Tiempo de espera al conectar | Puerto 465 o salida bloqueada | Usa `smtp.gmail.com:587` con TLS (STARTTLS) |
| El correo no llega | Llegó a spam, dirección del cliente incorrecta o límite diario de Gmail | Revisa spam, la dirección y la cuota de la cuenta |
| `5.7.0 Authentication Required` | Usuario o remitente no coinciden con la cuenta autenticada | Usa la misma dirección en Usuario y Correo remitente |

Gmail impone límites diarios de envío a las cuentas personales (aproximadamente 500 destinatarios al día). Para volumen mayor se necesita un proveedor transaccional, que queda fuera del alcance actual.

## 6. Seguridad

- No compartas la contraseña de aplicación ni la guardes en `appsettings.json`, variables de entorno del repositorio o documentación.
- Si sospechas que se filtró, revócala de inmediato en <https://myaccount.google.com/apppasswords> y crea otra.
- El anillo de Data Protection que cifra la contraseña vive en `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` (Windows) o `~/.aspnet/DataProtection-Keys` (Unix). Si se borra, la contraseña guardada deja de poder descifrarse y hay que volver a ingresarla.
- En producción, restringe el acceso a esa carpeta y respáldala como secreto.
