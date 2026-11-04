param(
    [Parameter(Mandatory = $true)][string]$MySqlExe,
    [string]$MySqlDefaultsFile,
    [string]$MySqlHost = '127.0.0.1',
    [int]$MySqlPort = 3306,
    [string]$MySqlUser = 'root',
    [string]$SqlConnectionString = 'Server=.;Database=imprentacollantesbd;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
)

$ErrorActionPreference = 'Stop'

function SourceRows([string]$query) {
    $arguments = @()
    if ($MySqlDefaultsFile) { $arguments += "--defaults-extra-file=$MySqlDefaultsFile" }
    $arguments += @("--host=$MySqlHost", "--port=$MySqlPort", "--user=$MySqlUser", '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names', "--execute=$query")
    $rows = & $MySqlExe @arguments
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer MySQL. Comprueba la conexión y el archivo privado de credenciales.' }
    return @($rows)
}

function Unhex([string]$hex) {
    if ($hex.Length % 2 -ne 0) { throw 'Texto hexadecimal inválido en la fuente.' }
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16) }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

function NullableValue([string]$value, [type]$type) {
    if ($value -eq '') { return [DBNull]::Value }
    if ($type -eq [int]) { return [int]::Parse($value, [Globalization.CultureInfo]::InvariantCulture) }
    if ($type -eq [byte]) { return [byte]::Parse($value, [Globalization.CultureInfo]::InvariantCulture) }
    if ($type -eq [bool]) { return $value -eq '1' }
    if ($type -eq [datetime]) { return [datetime]::ParseExact($value, 'yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture) }
    return $value
}

$docColumns = @('iddocumentoidentidadtipo','codigo','nombre','abreviatura','longitud','estado','eliminado','idusuario_crea','idusuario_modifica','idusuario_elimina','fecha_crea','fecha_modifica','fecha_elimina')
$docTypes = @([int],[string],[string],[string],[byte],[bool],[bool],[int],[int],[int],[datetime],[datetime],[datetime])
$docs = New-Object System.Data.DataTable
for ($i = 0; $i -lt $docColumns.Count; $i++) { [void]$docs.Columns.Add($docColumns[$i], $docTypes[$i]) }

$docQuery = @'
SELECT iddocumentoidentidadtipo, codigo, HEX(CONVERT(nombre USING utf8mb4)),
IFNULL(HEX(CONVERT(abreviatura USING utf8mb4)),''), IFNULL(longitud,''), estado, eliminado,
IFNULL(idusuario_crea,''), IFNULL(idusuario_modifica,''), IFNULL(idusuario_elimina,''),
DATE_FORMAT(fecha_crea,'%Y-%m-%d %H:%i:%s'), IFNULL(DATE_FORMAT(fecha_modifica,'%Y-%m-%d %H:%i:%s'),''),
IFNULL(DATE_FORMAT(fecha_elimina,'%Y-%m-%d %H:%i:%s'),'')
FROM facturuna.mae_documentoidentidad_tipo ORDER BY iddocumentoidentidadtipo
'@
foreach ($line in (SourceRows $docQuery)) {
    $parts = $line -split "`t", 13
    if ($parts.Count -ne 13) { throw 'Fila de tipo de documento incompleta.' }
    $parts[2] = Unhex $parts[2]
    if ($parts[3] -ne '') { $parts[3] = Unhex $parts[3] }
    $row = $docs.NewRow()
    for ($i = 0; $i -lt 13; $i++) { $row[$i] = NullableValue $parts[$i] $docTypes[$i] }
    $docs.Rows.Add($row)
}

$geoColumns = @('idubigeo','ubigeo','descripcion','parent','idpais','idparent')
$geoTypes = @([int],[string],[string],[string],[int],[int])
$geos = New-Object System.Data.DataTable
for ($i = 0; $i -lt $geoColumns.Count; $i++) { [void]$geos.Columns.Add($geoColumns[$i], $geoTypes[$i]) }
$geoQuery = @'
SELECT idubigeo,ubigeo,HEX(CONVERT(descripcion USING utf8mb4)),parent,IFNULL(idpais,''),IFNULL(idparent,'')
FROM facturuna.mae_ubigeo ORDER BY idubigeo
'@
$uniqueGeo = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($line in (SourceRows $geoQuery)) {
    $parts = $line -split "`t", 6
    if ($parts.Count -ne 6 -or $parts[1] -notmatch '^[0-9]{6}$' -or -not $uniqueGeo.Add($parts[1])) { throw 'Ubigeo incompleto o duplicado.' }
    $parts[2] = Unhex $parts[2]
    $row = $geos.NewRow()
    for ($i = 0; $i -lt 6; $i++) { $row[$i] = NullableValue $parts[$i] $geoTypes[$i] }
    $geos.Rows.Add($row)
}
if ($docs.Rows.Count -eq 0 -or $geos.Rows.Count -eq 0 -or ($docs.Select("codigo = '1'").Count -ne 1) -or ($docs.Select("codigo = '6'").Count -ne 1)) {
    throw 'La fuente no contiene catálogos o faltan DNI/RUC. No se importó nada.'
}

$connection = New-Object System.Data.SqlClient.SqlConnection($SqlConnectionString)
$connection.Open()
try {
    $transaction = $connection.BeginTransaction()
    try {
        $check = $connection.CreateCommand()
        $check.Transaction = $transaction
        $check.CommandText = 'SELECT (SELECT COUNT(*) FROM dbo.ubigeo) + (SELECT COUNT(*) FROM dbo.documentoidentidad_tipo)'
        if ([int]$check.ExecuteScalar() -ne 0) { throw 'El destino no está vacío. Revisa el estado antes de volver a importar.' }
        foreach ($batch in @(@('dbo.documentoidentidad_tipo',$docs,$docColumns), @('dbo.ubigeo',$geos,$geoColumns))) {
            $copy = New-Object System.Data.SqlClient.SqlBulkCopy($connection, [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity, $transaction)
            try {
                $copy.DestinationTableName = $batch[0]
                foreach ($column in $batch[2]) { [void]$copy.ColumnMappings.Add($column, $column) }
                $copy.WriteToServer($batch[1])
            } finally { $copy.Dispose() }
        }
        $backfill = $connection.CreateCommand()
        $backfill.Transaction = $transaction
        $backfill.CommandText = @'
UPDATE c SET idDocumentoIdentidadTipo = t.iddocumentoidentidadtipo, numeroDocumento = LTRIM(RTRIM(c.dni))
FROM dbo.Cliente c JOIN dbo.documentoidentidad_tipo t ON t.codigo = '1'
WHERE c.idDocumentoIdentidadTipo IS NULL AND c.numeroDocumento IS NULL
AND LEN(LTRIM(RTRIM(c.dni))) = 8 AND LTRIM(RTRIM(c.dni)) NOT LIKE '%[^0-9]%'
'@
        $recovered = $backfill.ExecuteNonQuery()
        $check.CommandText = 'SELECT COUNT(*) FROM dbo.ubigeo'
        if ([int]$check.ExecuteScalar() -ne $geos.Rows.Count) { throw 'El recuento de ubigeos no coincide.' }
        $check.CommandText = 'SELECT COUNT(*) FROM dbo.documentoidentidad_tipo'
        if ([int]$check.ExecuteScalar() -ne $docs.Rows.Count) { throw 'El recuento de documentos no coincide.' }
        $transaction.Commit()
        Write-Output "Importación completa: $($geos.Rows.Count) ubigeos, $($docs.Rows.Count) tipos de documento; $recovered clientes con DNI conservado."
    } catch { $transaction.Rollback(); throw }
} finally { $connection.Dispose() }
