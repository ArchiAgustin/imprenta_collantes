param(
    [string]$MySqlExe = 'C:\laragon\bin\mysql\mysql-8.0.30-winx64\bin\mysql.exe',
    [string]$MySqlHost = '127.0.0.1',
    [int]$MySqlPort = 3306,
    [string]$MySqlDatabase = 'kipu',
    [string]$MySqlUser = 'root',
    [string]$MySqlOptionsFile,
    [string]$SqlConnectionString = 'Server=.;Database=imprentacollantesbd;Integrated Security=True;TrustServerCertificate=True;',
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

if ($MySqlDatabase -notmatch '^[A-Za-z0-9_]+$') { throw 'El nombre de base MySQL contiene caracteres no admitidos.' }
if (-not (Test-Path -LiteralPath $MySqlExe -PathType Leaf)) { throw "No se encontró mysql.exe: $MySqlExe" }
if ($MySqlOptionsFile -and -not (Test-Path -LiteralPath $MySqlOptionsFile -PathType Leaf)) { throw 'No se encontró el archivo privado de opciones MySQL.' }

$catalogos = @(
    @{ Tabla = 'mae_entidadfinanciera_tipo'; Clave = 'identidadfinancieratipo'; Esperadas = 9; Columnas = @(
        @{ Nombre = 'identidadfinancieratipo'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_tipodocumentopago'; Clave = 'idtipodocumentopago'; Esperadas = 141; Columnas = @(
        @{ Nombre = 'idtipodocumentopago'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'mostrar_rc'; Tipo = [bool]; Nulo = $true }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_moneda'; Clave = 'idmoneda'; Esperadas = 2; Columnas = @(
        @{ Nombre = 'idmoneda'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'simbolo'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'codigoiso'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_detraccion'; Clave = 'iddetraccion'; Esperadas = 37; Columnas = @(
        @{ Nombre = 'iddetraccion'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigo'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'porcentaje'; Tipo = [decimal]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_tipomediopago'; Clave = 'idtipomediopago'; Esperadas = 28; Columnas = @(
        @{ Nombre = 'idtipomediopago'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'monederodigital'; Tipo = [bool]; Nulo = $true }, @{ Nombre = 'cuentainterna'; Tipo = [bool]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_motivonc'; Clave = 'idmotivonc'; Esperadas = 13; Columnas = @(
        @{ Nombre = 'idmotivonc'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_motivond'; Clave = 'idmotivond'; Esperadas = 3; Columnas = @(
        @{ Nombre = 'idmotivond'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_motivotraslado'; Clave = 'idmotivotraslado'; Esperadas = 14; Columnas = @(
        @{ Nombre = 'idmotivotraslado'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_entidadfinanciera'; Clave = 'identidadfinanciera'; Esperadas = 82; Columnas = @(
        @{ Nombre = 'identidadfinanciera'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'identidadfinancieratipo'; Tipo = [int]; Nulo = $false },
        @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_tipoactividad'; Clave = 'idtipoactividad'; Esperadas = 433; Columnas = @(
        @{ Nombre = 'idtipoactividad'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $true }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_tipocontribuyente'; Clave = 'idtipocontribuyente'; Esperadas = 51; Columnas = @(
        @{ Nombre = 'idtipocontribuyente'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_via_tipo'; Clave = 'idviatipo'; Esperadas = 32; Columnas = @(
        @{ Nombre = 'idviatipo'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'mae_zona_tipo'; Clave = 'idzonatipo'; Esperadas = 32; Columnas = @(
        @{ Nombre = 'idzonatipo'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'codigosunat'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false }, @{ Nombre = 'desc_corta'; Tipo = [string]; Nulo = $true },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) },
    @{ Tabla = 'ven_estadosunat'; Clave = 'idestadosunat'; Esperadas = 8; Columnas = @(
        @{ Nombre = 'idestadosunat'; Tipo = [int]; Nulo = $false }, @{ Nombre = 'descripcion'; Tipo = [string]; Nulo = $false },
        @{ Nombre = 'estado'; Tipo = [bool]; Nulo = $false }) }
)

function Get-MySqlJsonRows([string]$table, [object[]]$columns) {
    $pairs = ($columns | ForEach-Object { "'" + $_.Nombre + "'," + $_.Nombre }) -join ','
    $query = "SELECT COALESCE(JSON_ARRAYAGG(JSON_OBJECT($pairs)), JSON_ARRAY()) FROM ``$MySqlDatabase``.``$table``"
    $arguments = @()
    if ($MySqlOptionsFile) { $arguments += "--defaults-extra-file=$MySqlOptionsFile" }
    $arguments += @("--host=$MySqlHost", "--port=$MySqlPort", "--user=$MySqlUser", '--default-character-set=utf8mb4', '--batch', '--raw', '--skip-column-names')
    $arguments += @('-e', $query)
    $output = & $MySqlExe @arguments 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Falló la lectura local de kipu.$table (código $LASTEXITCODE). No se escribió SQL Server." }
    $json = (@($output) -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($json)) { throw "MySQL no devolvió JSON para kipu.$table." }
    $rows = ConvertFrom-Json -InputObject $json
    return $rows
}

function Convert-MySqlValue($value, $column) {
    if ($null -eq $value) {
        if (-not $column.Nulo) { throw "kipu devolvió NULL para $($column.Nombre), que se esperaba obligatorio." }
        return [DBNull]::Value
    }
    if ($column.Tipo -eq [string]) { return [string]$value }
    if ($column.Tipo -eq [int]) { return [Convert]::ToInt32($value, [Globalization.CultureInfo]::InvariantCulture) }
    if ($column.Tipo -eq [bool]) { return ([Convert]::ToInt32($value, [Globalization.CultureInfo]::InvariantCulture) -ne 0) }
    if ($column.Tipo -eq [decimal]) { return [decimal]::Parse([string]$value, [Globalization.CultureInfo]::InvariantCulture) }
    throw "Tipo no admitido para $($column.Nombre)."
}

function New-CatalogDataTable($definition, [object[]]$rows) {
    $table = [System.Data.DataTable]::new($definition.Tabla)
    foreach ($column in $definition.Columnas) {
        $dataColumn = [System.Data.DataColumn]::new($column.Nombre, $column.Tipo)
        $dataColumn.AllowDBNull = [bool]$column.Nulo
        [void]$table.Columns.Add($dataColumn)
    }
    foreach ($sourceRow in $rows) {
        $row = $table.NewRow()
        foreach ($column in $definition.Columnas) {
            $sourceValue = $sourceRow.PSObject.Properties[$column.Nombre].Value
            $row[$column.Nombre] = Convert-MySqlValue $sourceValue $column
        }
        [void]$table.Rows.Add($row)
    }
    return ,$table
}

function Get-TargetCount($connection, $transaction, [string]$table) {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = "SELECT COUNT_BIG(*) FROM dbo.[$table]"
    return [long]$command.ExecuteScalar()
}

function Test-ExistingDataEqual($connection, $transaction, $definition, $sourceRows) {
    $columns = @($definition.Columnas | ForEach-Object { "[$($_.Nombre)]" }) -join ','
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = "SELECT $columns FROM dbo.[$($definition.Tabla)] ORDER BY [$($definition.Clave)]"
    $reader = $command.ExecuteReader()
    try {
        $sourceOrdered = @($sourceRows | Sort-Object { [int]$_.PSObject.Properties[$definition.Clave].Value })
        $index = 0
        while ($reader.Read()) {
            if ($index -ge $sourceOrdered.Count) { return $false }
            $sourceRow = $sourceOrdered[$index]
            foreach ($column in $definition.Columnas) {
                $left = $sourceRow.PSObject.Properties[$column.Nombre].Value
                $right = if ($reader.IsDBNull($reader.GetOrdinal($column.Nombre))) { $null } else { $reader[$column.Nombre] }
                if ($null -eq $left -and $null -eq $right) { continue }
                if ($null -eq $left -or $null -eq $right) { return $false }
                if ($column.Tipo -eq [string]) {
                    if (-not [string]::Equals(([string]$left).TrimEnd(), ([string]$right).TrimEnd(), [StringComparison]::Ordinal)) { return $false }
                } elseif ($column.Tipo -eq [bool]) {
                    if (([Convert]::ToInt32($left) -ne 0) -ne [Convert]::ToBoolean($right)) { return $false }
                } elseif ($column.Tipo -eq [int]) {
                    if ([Convert]::ToInt32($left) -ne [Convert]::ToInt32($right)) { return $false }
                } elseif ($column.Tipo -eq [decimal]) {
                    if ([decimal]$left -ne [decimal]$right) { return $false }
                }
            }
            $index++
        }
        return $index -eq $sourceOrdered.Count
    } finally {
        $reader.Dispose()
        $command.Dispose()
    }
}

function Import-Catalog($connection, $transaction, $definition, $sourceRows, [bool]$previewOnly) {
    $count = Get-TargetCount $connection $transaction $definition.Tabla
    if ($count -gt 0) {
        if ($count -eq $sourceRows.Count -and (Test-ExistingDataEqual $connection $transaction $definition $sourceRows)) {
            Write-Host "Ya conciliado: dbo.$($definition.Tabla) ($count filas)."
            return
        }
        throw "dbo.$($definition.Tabla) ya contiene $count filas y difiere de kipu ($($sourceRows.Count)); se cancela para no sobrescribir ni duplicar datos."
    }
    if ($sourceRows.Count -eq 0) { throw "kipu.$($definition.Tabla) está vacío; se esperaba el catálogo validado." }
    if ($previewOnly) {
        Write-Host "Listo para importar: kipu.$($definition.Tabla) ($($sourceRows.Count) filas)."
        return
    }

    $dataTable = New-CatalogDataTable $definition $sourceRows
    $bulk = [System.Data.SqlClient.SqlBulkCopy]::new(
        $connection,
        [System.Data.SqlClient.SqlBulkCopyOptions]::CheckConstraints,
        $transaction)
    try {
        $bulk.DestinationTableName = "dbo.[$($definition.Tabla)]"
        $bulk.BatchSize = 500
        foreach ($column in $definition.Columnas) { [void]$bulk.ColumnMappings.Add($column.Nombre, $column.Nombre) }
        $bulk.WriteToServer($dataTable)
    } finally {
        $bulk.Dispose()
        $dataTable.Dispose()
    }
    Write-Host "Importado: kipu.$($definition.Tabla) ($($sourceRows.Count) filas)."
}

function Get-SourceRows([string]$table, [string[]]$columns) {
    $definition = @{ Tabla = $table; Columnas = @($columns | ForEach-Object { @{ Nombre = $_; Tipo = [string]; Nulo = $true } }) }
    return Get-MySqlJsonRows $table $definition.Columnas
}

function Reconcile-ExistingCatalogs($connection, $transaction) {
    $docs = Get-SourceRows 'mae_documentoidentidad_tipo' @('codigosunat','descripcion','desc_corta','longitud')
    $cmd = $connection.CreateCommand(); $cmd.Transaction = $transaction
    $cmd.CommandText = 'SELECT codigo,nombre,abreviatura,longitud FROM dbo.documentoidentidad_tipo WHERE eliminado = 0'
    $reader = $cmd.ExecuteReader(); $target = @{}
    try { while ($reader.Read()) { $target[[string]$reader['codigo']] = @{ Nombre=[string]$reader['nombre']; Corta=if($reader.IsDBNull(2)){$null}else{[string]$reader['abreviatura']}; Longitud=if($reader.IsDBNull(3)){$null}else{[int]$reader['longitud']} } } }
    finally { $reader.Dispose(); $cmd.Dispose() }
    foreach ($row in $docs) {
        $code = ([string]$row.codigosunat).TrimEnd()
        if (-not $target.ContainsKey($code)) { throw "Falta el tipo de documento SUNAT '$code' en dbo.documentoidentidad_tipo; requiere conciliación explícita." }
        $existing = $target[$code]
        if ($existing.Nombre -ne [string]$row.descripcion -or $existing.Corta -ne [string]$row.desc_corta) {
            throw "El tipo de documento SUNAT '$code' tiene nombre/abreviatura distintos en Collantes; no se sobrescribió." 
        }
        if ($null -eq $existing.Longitud -and $null -ne $row.longitud) {
            $update = $connection.CreateCommand(); $update.Transaction = $transaction
            $update.CommandText = 'UPDATE dbo.documentoidentidad_tipo SET longitud=@longitud WHERE codigo=@codigo AND longitud IS NULL'
            $p1 = $update.Parameters.Add('@longitud',[System.Data.SqlDbType]::TinyInt); $p1.Value=[byte]$row.longitud
            $p2 = $update.Parameters.Add('@codigo',[System.Data.SqlDbType]::NVarChar,5); $p2.Value=$code
            [void]$update.ExecuteNonQuery(); $update.Dispose()
        } elseif ($null -ne $existing.Longitud -and $null -ne $row.longitud -and $existing.Longitud -ne [int]$row.longitud) {
            throw "La longitud del tipo de documento '$code' difiere en Collantes; no se sobrescribió."
        }
    }
    Write-Host "Verificado: tipos de documento existentes; solo se completa longitud cuando estaba NULL."

    $units = Get-SourceRows 'mae_unidadmedida' @('codigosunat','descripcion','desccorta','estado')
    $cmd = $connection.CreateCommand(); $cmd.Transaction = $transaction
    $cmd.CommandText = 'SELECT codigoSunat,descripcion,descripcionCorta,activo FROM dbo.UnidadMedida'
    $reader = $cmd.ExecuteReader(); $unitKeys = @{}
    try {
        while ($reader.Read()) {
            $code = if ($reader.IsDBNull(0)) { '' } else { ([string]$reader['codigoSunat']).TrimEnd() }
            $name = if ($reader.IsDBNull(1)) { '' } else { ([string]$reader['descripcion']).TrimEnd() }
            $short = if ($reader.IsDBNull(2)) { $null } else { ([string]$reader['descripcionCorta']).TrimEnd() }
            $unitKeys[$code + [char]31 + $name] = @{ Corta=$short; Activa=([Convert]::ToBoolean($reader['activo'])) }
        }
    } finally { $reader.Dispose(); $cmd.Dispose() }
    foreach ($row in $units) {
        $code = if ($null -eq $row.codigosunat) { '' } else { ([string]$row.codigosunat).TrimEnd() }
        $name = if ($null -eq $row.descripcion) { '' } else { ([string]$row.descripcion).TrimEnd() }
        $key = $code + [char]31 + $name
        if ($unitKeys.ContainsKey($key)) {
            $sourceShort = if ($null -eq $row.desccorta) { $null } else { ([string]$row.desccorta).TrimEnd() }
            $sourceActive = [Convert]::ToInt32($row.estado) -ne 0
            $targetUnit = $unitKeys[$key]
            if ($targetUnit.Corta -ne $sourceShort -or $targetUnit.Activa -ne $sourceActive) {
                Write-Warning "Unidad existente '$name' ($code): se conserva abreviatura/activo de Collantes; el origen Kipu difiere."
            }
            continue
        }
        $insert = $connection.CreateCommand(); $insert.Transaction = $transaction
        $insert.CommandText = 'INSERT dbo.UnidadMedida (descripcion,descripcionCorta,codigoSunat,activo) VALUES (@descripcion,@corta,@codigo,@activo)'
        foreach ($p in @(
            @{Name='@descripcion';Type=[System.Data.SqlDbType]::NVarChar;Size=100;Value=$row.descripcion},
            @{Name='@corta';Type=[System.Data.SqlDbType]::NVarChar;Size=100;Value=$row.desccorta},
            @{Name='@codigo';Type=[System.Data.SqlDbType]::NVarChar;Size=100;Value=$row.codigosunat},
            @{Name='@activo';Type=[System.Data.SqlDbType]::Bit;Size=0;Value=([Convert]::ToInt32($row.estado) -ne 0)})) {
            $parameter = $insert.Parameters.Add($p.Name,$p.Type)
            if($p.Size -gt 0){$parameter.Size=$p.Size}
            if($null -eq $p.Value){$parameter.Value=[DBNull]::Value}else{$parameter.Value=$p.Value}
        }
        [void]$insert.ExecuteNonQuery(); $insert.Dispose(); $unitKeys[$key] = @{ Corta=$row.desccorta; Activa=([Convert]::ToInt32($row.estado) -ne 0) }
    }
    Write-Host "Verificado/importado sin reemplazar IDs en uso: unidades de medida. El factor de origen no se importa como conversión de producto."

    $ubigeos = Get-SourceRows 'mae_ubigeo' @('ubigeo','descripcion','parent')
    $cmd = $connection.CreateCommand(); $cmd.Transaction = $transaction
    $cmd.CommandText = 'SELECT ubigeo,descripcion,parent FROM dbo.ubigeo'
    $reader = $cmd.ExecuteReader(); $targetUbigeos = @{}
    try { while ($reader.Read()) { $targetUbigeos[([string]$reader['ubigeo']).TrimEnd()] = ([string]$reader['descripcion']).TrimEnd() + [char]31 + ([string]$reader['parent']).TrimEnd() } }
    finally { $reader.Dispose(); $cmd.Dispose() }
    foreach ($row in $ubigeos) {
        $code = ([string]$row.ubigeo).TrimEnd()
        $value = ([string]$row.descripcion).TrimEnd() + [char]31 + ([string]$row.parent).TrimEnd()
        if (-not $targetUbigeos.ContainsKey($code) -or $targetUbigeos[$code] -ne $value) {
            throw "Ubigeo '$code' falta o difiere en Collantes; no se alteraron ubicaciones ya asociadas a clientes/proveedores."
        }
    }
    Write-Host "Verificado: ubigeos existentes por código, descripción y padre (sin duplicar filas)."
}

$sourceRows = @{}
foreach ($definition in $catalogos) {
    $rows = @(Get-MySqlJsonRows $definition.Tabla $definition.Columnas | Sort-Object { [int]$_.PSObject.Properties[$definition.Clave].Value })
    if ($rows.Count -ne $definition.Esperadas) {
        throw "El recuento de kipu.$($definition.Tabla) cambió: esperado=$($definition.Esperadas), actual=$($rows.Count). Revisa el origen antes de importar."
    }
    $sourceRows[$definition.Tabla] = $rows
}

$connection = [System.Data.SqlClient.SqlConnection]::new($SqlConnectionString)
$connection.Open()
$transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
try {
    Reconcile-ExistingCatalogs $connection $transaction
    foreach ($definition in $catalogos) { Import-Catalog $connection $transaction $definition $sourceRows[$definition.Tabla] ([bool]$VerifyOnly) }
    if ($VerifyOnly) { $transaction.Rollback(); Write-Host 'Verificación completada; no se escribieron cambios.' }
    else { $transaction.Commit(); Write-Host 'Importación de catálogos SUNAT confirmada en una sola transacción.' }
} catch {
    if ($transaction) { $transaction.Rollback() }
    throw
} finally {
    if ($transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
