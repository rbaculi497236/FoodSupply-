Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root 'Documentation'
$imageFolder = Join-Path $output 'BackendSourceScreenshots'
New-Item -ItemType Directory -Force -Path $imageFolder | Out-Null

function Get-Lines([string]$relativePath, [int]$start, [int]$end) {
    $lines = Get-Content (Join-Path $root $relativePath)
    return ($lines[($start - 1)..($end - 1)] -join [Environment]::NewLine)
}

function New-CodeScreenshot([string]$fileName, [string]$caption, [string]$code) {
    $font = [System.Drawing.Font]::new('Consolas', 10)
    $titleFont = [System.Drawing.Font]::new('Segoe UI Semibold', 12)
    $lineHeight = 17
    $codeLines = $code -split "`r?`n"
    $width = 1500
    $height = 85 + (($codeLines.Count + 1) * $lineHeight)
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
    $headerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(45, 45, 48))
    $graphics.FillRectangle($headerBrush, 0, 0, $width, 46)
    $graphics.DrawString($caption, $titleFont, [System.Drawing.Brushes]::White, 22, 13)
    $numberBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(133, 153, 0))
    $codeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(220, 220, 220))
    $lineNumber = 1
    $y = 60
    foreach ($line in $codeLines) {
        $graphics.DrawString($lineNumber.ToString().PadLeft(3), $font, $numberBrush, 12, $y)
        $graphics.DrawString($line, $font, $codeBrush, 62, $y)
        $y += $lineHeight
        $lineNumber++
    }
    $path = Join-Path $imageFolder $fileName
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $headerBrush.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $font.Dispose(); $titleFont.Dispose()
    return $path
}

$sections = @(
    [pscustomobject]@{ Title = 'ApplicationDbContext'; Description = 'Entity Framework Core database context and DbSet declarations that connect the FoodSupply models to the database.'; Caption = 'Data/ApplicationDbContext.cs'; Code = (Get-Content (Join-Path $root 'Data/ApplicationDbContext.cs') -Raw) },
    [pscustomobject]@{ Title = 'Models'; Description = 'Entity models define validation rules, fields, and relationships used by the system.'; Caption = 'Models/Inventory.cs'; Code = (Get-Content (Join-Path $root 'Models/Inventory.cs') -Raw) },
    [pscustomobject]@{ Title = 'Controllers'; Description = 'Controllers handle CRUD actions, validation, database queries, and business workflows.'; Caption = 'Controllers/SalesOrdersController.cs — order validation and inventory check'; Code = (Get-Lines 'Controllers/SalesOrdersController.cs' 70 128) },
    [pscustomobject]@{ Title = 'Migrations / Database Integration'; Description = 'EF Core migration code creates database tables and foreign-key relationships. Apply migrations with: dotnet ef database update'; Caption = 'Migrations/20260908024527_InitialCreate.cs'; Code = (Get-Lines 'Migrations/20260908024527_InitialCreate.cs' 1 60) },
    [pscustomobject]@{ Title = 'Entity Framework Core Database Operations'; Description = 'Asynchronous EF Core queries retrieve records, update stock levels, and persist the sales order in one workflow.'; Caption = 'Controllers/SalesOrdersController.cs — EF Core operations'; Code = (Get-Lines 'Controllers/SalesOrdersController.cs' 91 140) },
    [pscustomobject]@{ Title = 'MySQL/MariaDB Integration'; Description = 'Pomelo Entity Framework Core provider is configured in Program.cs to use the DefaultConnection string and auto-detect the server version.'; Caption = 'Program.cs — Pomelo MySQL configuration'; Code = (Get-Lines 'Program.cs' 1 30) },
    [pscustomobject]@{ Title = 'Transaction Workflow'; Description = 'A sales order validates products and stock, records order items, deducts inventory, updates the stock status, then saves the workflow.'; Caption = 'Controllers/SalesOrdersController.cs — sales order workflow'; Code = (Get-Lines 'Controllers/SalesOrdersController.cs' 148 207) },
    [pscustomobject]@{ Title = 'Inventory Monitoring'; Description = 'Inventory tracks quantity, reorder threshold, status, expiry date, spoiled quantity, and damaged quantity.'; Caption = 'Models/Inventory.cs'; Code = (Get-Content (Join-Path $root 'Models/Inventory.cs') -Raw) },
    [pscustomobject]@{ Title = 'Authentication'; Description = 'Cookie authentication is configured with login and access-denied paths. The account controller verifies credentials and issues the signed-in principal.'; Caption = 'Program.cs — cookie authentication'; Code = (Get-Lines 'Program.cs' 27 68) },
    [pscustomobject]@{ Title = 'Authorization and Role-Based Access Control'; Description = 'Controller attributes restrict module access to the named roles.'; Caption = 'Controllers/SuppliersController.cs — role authorization'; Code = (Get-Lines 'Controllers/SuppliersController.cs' 1 42) },
    [pscustomobject]@{ Title = 'Password Hashing'; Description = 'The login and reset-password workflow uses ASP.NET Core PasswordHasher to verify and create password hashes.'; Caption = 'Controllers/AccountController.cs — password verification and rehash'; Code = (Get-Lines 'Controllers/AccountController.cs' 70 135) },
    [pscustomobject]@{ Title = 'Input Validation'; Description = 'Data annotations define required fields and numeric ranges, while controllers use ModelState before writing records.'; Caption = 'Models/User.cs — validation attributes'; Code = (Get-Content (Join-Path $root 'Models/User.cs') -Raw) }
)

function Escape-Xml([string]$value) {
    return [System.Security.SecurityElement]::Escape($value)
}

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("FoodSupplyDocx_" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path (Join-Path $staging '_rels'), (Join-Path $staging 'word'), (Join-Path $staging 'word\_rels'), (Join-Path $staging 'word\media') | Out-Null
$body = New-Object System.Text.StringBuilder
[void]$body.Append('<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val="36"/></w:rPr><w:t>FoodSupply ERP System - Backend Prototype</w:t></w:r></w:p>')
[void]$body.Append('<w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:sz w:val="24"/></w:rPr><w:t>Source-code screenshots and feature documentation</w:t></w:r></w:p>')
[void]$body.Append('<w:p><w:r><w:t>Generated from the current FoodSupply ASP.NET Core MVC project.</w:t></w:r></w:p>')
$relationships = New-Object System.Text.StringBuilder
$imageIndex = 1

foreach ($section in $sections) {
    $imageName = (($section.Title -replace '[^a-zA-Z0-9]', '_') + '.png')
    $imagePath = New-CodeScreenshot $imageName $section.Caption $section.Code.TrimEnd()
    Copy-Item $imagePath (Join-Path $staging ("word\media\image" + $imageIndex + '.png'))
    [void]$relationships.Append('<Relationship Id="rId' + $imageIndex + '" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/image' + $imageIndex + '.png"/>')
    [void]$body.Append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
    [void]$body.Append('<w:p><w:r><w:rPr><w:b/><w:sz w:val="28"/></w:rPr><w:t>' + (Escape-Xml $section.Title) + '</w:t></w:r></w:p>')
    [void]$body.Append('<w:p><w:r><w:t>' + (Escape-Xml $section.Description) + '</w:t></w:r></w:p>')
    $image = [System.Drawing.Image]::FromFile($imagePath)
    $imageHeight = [int](5943600 * $image.Height / $image.Width)
    $image.Dispose()
    $drawing = '<w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="5943600" cy="' + $imageHeight + '"/><wp:docPr id="' + $imageIndex + '" name="Source screenshot"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="source.png"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="rId' + $imageIndex + '"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="5943600" cy="' + $imageHeight + '"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing>'
    [void]$body.Append('<w:p><w:r>' + $drawing + '</w:r></w:p>')
    $imageIndex++
}
$note = 'Password hashing is used by AccountController for login verification and password reset. UsersController currently contains a temporary plain-text assignment when creating a user; replace that assignment with PasswordHasher.HashPassword before treating password hashing as fully enforced for every user-creation path.'
[void]$body.Append('<w:p><w:r><w:br w:type="page"/></w:r></w:p><w:p><w:r><w:rPr><w:b/><w:sz w:val="28"/></w:rPr><w:t>Implementation Note</w:t></w:r></w:p><w:p><w:r><w:t>' + (Escape-Xml $note) + '</w:t></w:r></w:p>')
$contentTypes = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>'
$rootRels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>'
$docRels = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' + $relationships + '</Relationships>'
$docXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>' + $body + '<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="720" w:right="720" w:bottom="720" w:left="720"/></w:sectPr></w:body></w:document>'
[System.IO.File]::WriteAllText((Join-Path $staging '[Content_Types].xml'), $contentTypes)
[System.IO.File]::WriteAllText((Join-Path $staging '_rels\.rels'), $rootRels)
[System.IO.File]::WriteAllText((Join-Path $staging 'word\_rels\document.xml.rels'), $docRels)
[System.IO.File]::WriteAllText((Join-Path $staging 'word\document.xml'), $docXml)
$documentPath = Join-Path $output 'FoodSupply_Backend_Prototype.docx'
if (Test-Path $documentPath) { Remove-Item -LiteralPath $documentPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::Open($documentPath, [System.IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem -Path $staging -File -Recurse | ForEach-Object {
    $entryName = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName) | Out-Null
}
$archive.Dispose()
Write-Output $documentPath
