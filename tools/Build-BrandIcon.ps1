$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = Join-Path $workspace 'images'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$bitmap = New-Object Drawing.Bitmap 1024,1024
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.ScaleTransform(2,2)
$ink = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#101010'))
$white = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#f4f4f4'))
$pen = New-Object Drawing.Pen $white,16
$pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
$paths = [Collections.Generic.List[Drawing.Drawing2D.GraphicsPath]]::new()
try {
    $background = [Drawing.Drawing2D.GraphicsPath]::new(); $paths.Add($background)
    foreach ($corner in @(@(16,16,180),@(296,16,270),@(296,296,0),@(16,296,90))) { $background.AddArc($corner[0],$corner[1],200,200,$corner[2],90) }
    $background.CloseFigure(); $graphics.FillPath($ink,$background)
    $moon = [Drawing.Drawing2D.GraphicsPath]::new(); $paths.Add($moon)
    $moon.AddBezier(328,78,234,52,132,119,132,214)
    $moon.AddBezier(132,214,132,296,194,358,272,363)
    $moon.AddBezier(272,363,220,334,194,291,194,238)
    $moon.AddBezier(194,238,194,164,248,104,328,78)
    $moon.CloseFigure(); $graphics.FillPath($white,$moon)
    $star = [Drawing.PointF[]]@([Drawing.PointF]::new(358,122),[Drawing.PointF]::new(368,147),[Drawing.PointF]::new(393,157),[Drawing.PointF]::new(368,167),[Drawing.PointF]::new(358,192),[Drawing.PointF]::new(348,167),[Drawing.PointF]::new(323,157),[Drawing.PointF]::new(348,147))
    $graphics.FillPolygon($white,$star)
    $lid = [Drawing.Drawing2D.GraphicsPath]::new(); $paths.Add($lid)
    $lid.AddLine(202,279,202,261); $lid.AddBezier(202,261,202,233,223,212,250,212)
    $lid.AddLine(250,212,354,212); $lid.AddBezier(354,212,381,212,402,233,402,261)
    $lid.AddLine(402,261,402,279); $lid.CloseFigure()
    $graphics.FillPath($ink,$lid); $graphics.DrawPath($pen,$lid)
    $body = [Drawing.Drawing2D.GraphicsPath]::new(); $paths.Add($body)
    $body.AddLine(202,279,402,279); $body.AddLine(402,279,402,366)
    $body.AddBezier(402,366,402,376,396,382,386,382); $body.AddLine(386,382,218,382)
    $body.AddBezier(218,382,208,382,202,376,202,366); $body.CloseFigure()
    $graphics.FillPath($ink,$body); $graphics.DrawPath($pen,$body)
    $graphics.FillRectangle($white,290,262,24,48); $graphics.FillEllipse($ink,297,280,10,10)
    foreach ($size in @(512,64)) {
        $output = New-Object Drawing.Bitmap $size,$size
        $canvas = [Drawing.Graphics]::FromImage($output)
        try {
            $canvas.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $canvas.DrawImage($bitmap,0,0,$size,$size)
            $name = if ($size -eq 512) { 'icon.png' } else { 'icon-preview-64.png' }
            $output.Save((Join-Path $destination $name),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $canvas.Dispose(); $output.Dispose() }
    }
} finally { foreach ($path in $paths) { $path.Dispose() }; $pen.Dispose(); $white.Dispose(); $ink.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
Write-Output (Join-Path $destination 'icon.png')
