param(
    [Parameter(Mandatory = $true)][string]$MySqlExe,
    [string]$MySqlDefaultsFile,
    [string]$MySqlHost = '127.0.0.1',
    [int]$MySqlPort = 3306,
    [string]$MySqlUser = 'root',
    [string]$SqlConnectionString = 'Server=.;Database=imprentacollantesbd;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
)

$ErrorActionPreference = 'Stop'

function Utf8FromHex([string]$hex) {
    if ($hex.Length % 2 -ne 0) { throw 'Cadena hexadecimal inválida en MySQL.' }
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16) }
    return [System.Text.Encoding]::UTF8.GetString($bytes)
}

$arguments = @()
if ($MySqlDefaultsFile) { $arguments += "--defaults-extra-file=$MySqlDefaultsFile" }
$arguments += @("--host=$MySqlHost", "--port=$MySqlPort", "--user=$MySqlUser",
    '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names',
    "--execute=SELECT idunidadmedida, HEX(CONVERT(descripcion USING utf8mb4)), IFNULL(HEX(CONVERT(desccorta USING utf8mb4)), ''), IFNULL(HEX(CONVERT(codigosunat USING utf8mb4)), ''), IFNULL(estado, 0) FROM bmotors.mae_unidadmedida ORDER BY idunidadmedida")
$lines = & $MySqlExe @arguments
if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer bmotors.mae_unidadmedida. Revisa la conexión MySQL.' }

$table = New-Object System.Data.DataTable
$columns = @('idUnidadMedida','descripcion','descripcionCorta','codigoSunat','activo')
[void]$table.Columns.Add($columns[0], [int])
for ($i = 1; $i -lt 4; $i++) { [void]$table.Columns.Add($columns[$i], [string]) }
[void]$table.Columns.Add($columns[4], [bool])
$ids = New-Object 'System.Collections.Generic.HashSet[int]'
foreach ($line in $lines) {
    $parts = $line -split "`t", 5
    if ($parts.Count -ne 5) { throw 'Registro incompleto en el catálogo MySQL.' }
    $id = [int]::Parse($parts[0], [Globalization.CultureInfo]::InvariantCulture)
    if (-not $ids.Add($id)) { throw 'Se encontró un identificador duplicado.' }
    $description = Utf8FromHex $parts[1]
    if ([string]::IsNullOrWhiteSpace($description)) { throw 'La unidad carece de descripción.' }
    $row = $table.NewRow()
    $row[0] = $id
    $row[1] = $description
    $row[2] = Utf8FromHex $parts[2]
    $row[3] = Utf8FromHex $parts[3]
    $row[4] = $parts[4] -eq '1'
    $table.Rows.Add($row)
}
if ($table.Rows.Count -eq 0) { throw 'El catálogo fuente está vacío: no se insertó nada.' }

$connection = New-Object System.Data.SqlClient.SqlConnection($SqlConnectionString)
$connection.Open()
try {
    $transaction = $connection.BeginTransaction()
    try {
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = 'SELECT COUNT(*) FROM dbo.UnidadMedida'
        if ([int]$command.ExecuteScalar() -ne 0) { throw 'UnidadMedida ya tiene registros. Verifica el estado antes de volver a importar.' }

        $copy = New-Object System.Data.SqlClient.SqlBulkCopy($connection, [System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity, $transaction)
        try {
            $copy.DestinationTableName = 'dbo.UnidadMedida'
            foreach ($column in $columns) { [void]$copy.ColumnMappings.Add($column, $column) }
            $copy.WriteToServer($table)
        } finally { $copy.Dispose() }

        if ([int]$command.ExecuteScalar() -ne $table.Rows.Count) { throw 'El número de unidades importadas no coincide.' }
        $transaction.Commit()
        Write-Output "Unidades de medida importadas: $($table.Rows.Count). No se importaron presentaciones ni stock."
    } catch { $transaction.Rollback(); throw }
} finally { $connection.Dispose() }
